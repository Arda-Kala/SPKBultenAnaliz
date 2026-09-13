using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SPKBultenAnaliz.Core.DTOs;
using SPKBultenAnaliz.Core.Interfaces;

namespace SPKBultenAnaliz.Business.Services
{
    /// <summary>
    /// IIslemYasaklariService'in somut uygulaması. SPK'nın resmi Web Servisi
    /// (ws.spk.gov.tr) üzerinden işlem yasaklı kişi/şirket listelerini çeker.
    /// </summary>
    public class IslemYasaklariService : IIslemYasaklariService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly ILogger<IslemYasaklariService> _logger;

        // SPK API Uç Noktaları
        private const string IslemYasaklariUrl = "https://ws.spk.gov.tr/IdariYaptirimlar/api/IslemYasaklari";
        private const string KisilerUrl = "https://ws.spk.gov.tr/IdariYaptirimlar/api/IslemYasaklari/Kisiler";
        private const string SirketlerUrl = "https://ws.spk.gov.tr/IdariYaptirimlar/api/IslemYasaklari/Sirketler";

        private static readonly TimeSpan CacheSuresi = TimeSpan.FromHours(6);

        private static readonly Dictionary<string, string[]> AlanAdaylari = new()
        {
            ["AdSoyadUnvan"] = new[] { "unvan", "unvani", "sirketUnvan", "SirketUnvan", "unvanAdSoyad", "UnvanAdSoyad", "isimSoyisim", "AdSoyad", "adSoyad", "Unvan" },
            ["GercekTuzelKisi"] = new[] { "gercekTuzelKisi", "GercekTuzelKisi", "Tip", "tip", "KisiTipi" },
            ["MkkSicilNo"] = new[] { "mkkSicilNo", "MkkSicilNo", "MkkNo", "mkkNo" },
            ["MersisNo"] = new[] { "mersisNo", "MersisNo" },
            ["KararTarihi"] = new[] { "kurulKararTarihi", "KurulKararTarihi", "KararTarihi", "kararTarihi" },
            ["KararNo"] = new[] { "kurulKararNo", "KurulKararNo", "KararNo", "kararNo" },
            ["Pay"] = new[] { "pay", "Pay", "Hisse", "hisse", "payAdi" },
            ["PayKodu"] = new[] { "payKodu", "PayKodu", "hisseKodu", "HisseKodu" },
            ["YasakBaslangicTarihi"] = new[] { "YasakBaslangicTarihi", "yasakBaslangicTarihi", "BaslangicTarihi" },
            ["YasakBitisTarihi"] = new[] { "YasakBitisTarihi", "yasakBitisTarihi", "BitisTarihi" },
            ["Aciklama"] = new[] { "Aciklama", "aciklama", "Detay", "detay" }
        };

        public IslemYasaklariService(HttpClient httpClient, IMemoryCache cache, ILogger<IslemYasaklariService> logger)
        {
            _httpClient = httpClient;
            _cache = cache;
            _logger = logger;
        }

        public Task<List<IslemYasakliDto>> TumKisileriGetirAsync(CancellationToken cancellationToken = default) =>
            _cache.GetOrCreateAsync("islem-yasaklari:kisiler", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheSuresi;
                var liste = await VeriCekVeAyristirAsync(IslemYasaklariUrl, "Kisi", cancellationToken);
                if (liste.Count == 0)
                {
                    liste = await VeriCekVeAyristirAsync(KisilerUrl, "Kisi", cancellationToken);
                }
                else
                {
                    liste = liste.Where(x => x.GercekTuzelKisi == "Gerçek Kişi" || x.Tip == "Kisi").ToList();
                }
                return liste;
            })!;

        public Task<List<IslemYasakliDto>> TumSirketleriGetirAsync(CancellationToken cancellationToken = default) =>
            _cache.GetOrCreateAsync("islem-yasaklari:sirketler", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheSuresi;
                // Doğrudan şirketler API uç noktasından veri çeker
                return await VeriCekVeAyristirAsync(SirketlerUrl, "Sirket", cancellationToken);
            })!;

        private async Task<List<IslemYasakliDto>> VeriCekVeAyristirAsync(string url, string tip, CancellationToken cancellationToken)
        {
            var sonuc = new List<IslemYasakliDto>();

            try
            {
                using var istek = new HttpRequestMessage(HttpMethod.Get, url);
                istek.Headers.Accept.ParseAdd("text/plain");

                using var yanit = await _httpClient.SendAsync(istek, cancellationToken);

                if (!yanit.IsSuccessStatusCode)
                {
                    _logger.LogError("SPK İşlem Yasaklıları servisi hata döndürdü: {StatusCode} ({Url}).", yanit.StatusCode, url);
                    return sonuc;
                }

                var icerik = await yanit.Content.ReadAsStringAsync(cancellationToken);

                if (string.IsNullOrWhiteSpace(icerik))
                    return sonuc;

                using var doc = JsonDocument.Parse(icerik);

                JsonElement diziElemani = doc.RootElement;
                if (diziElemani.ValueKind == JsonValueKind.Object)
                {
                    foreach (var olasiSarmalayici in new[] { "data", "Data", "result", "Result", "Liste", "liste", "items", "Items" })
                    {
                        if (doc.RootElement.TryGetProperty(olasiSarmalayici, out var ic) && ic.ValueKind == JsonValueKind.Array)
                        {
                            diziElemani = ic;
                            break;
                        }
                    }
                }

                if (diziElemani.ValueKind != JsonValueKind.Array)
                {
                    _logger.LogWarning("SPK İşlem Yasaklıları yanıtı beklenen dizi formatında değil ({Url}).", url);
                    return sonuc;
                }

                foreach (var kayit in diziElemani.EnumerateArray())
                {
                    sonuc.Add(TekKayitEslestir(kayit, tip));
                }

                _logger.LogInformation("SPK'dan {Sayi} adet işlem yasaklı kaydı çekildi ({Url}).", sonuc.Count, url);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SPK İşlem Yasaklıları verisi çekilirken hata oluştu ({Url}).", url);
            }

            return sonuc;
        }

        private static IslemYasakliDto TekKayitEslestir(JsonElement kayit, string tip)
        {
            var dto = new IslemYasakliDto { Tip = tip };
            var eslesenAlanAdlari = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string? BulVeIsaretle(string hedefAlan)
            {
                if (!AlanAdaylari.TryGetValue(hedefAlan, out var adaylar)) return null;

                foreach (var aday in adaylar)
                {
                    if (kayit.TryGetProperty(aday, out var deger) && deger.ValueKind != JsonValueKind.Null)
                    {
                        eslesenAlanAdlari.Add(aday);
                        return deger.ValueKind == JsonValueKind.String ? deger.GetString() : deger.ToString();
                    }
                }
                return null;
            }

            dto.AdSoyadUnvan = BulVeIsaretle("AdSoyadUnvan") ?? string.Empty;
            dto.GercekTuzelKisi = BulVeIsaretle("GercekTuzelKisi") ?? (tip == "Kisi" ? "Gerçek Kişi" : "Tüzel Kişi");
            dto.MkkSicilNo = BulVeIsaretle("MkkSicilNo");
            dto.MersisNo = BulVeIsaretle("MersisNo");

            // Tarih formatlama
            var rawTarih = BulVeIsaretle("KararTarihi");
            if (!string.IsNullOrEmpty(rawTarih) && DateTime.TryParse(rawTarih, out var parsedDate))
            {
                dto.KararTarihi = parsedDate.ToString("dd.MM.yyyy");
            }
            else
            {
                dto.KararTarihi = rawTarih;
            }

            dto.KararNo = BulVeIsaretle("KararNo");
            dto.Pay = BulVeIsaretle("Pay");
            dto.PayKodu = BulVeIsaretle("PayKodu");
            dto.YasakBaslangicTarihi = BulVeIsaretle("YasakBaslangicTarihi");
            dto.YasakBitisTarihi = BulVeIsaretle("YasakBitisTarihi");
            dto.Aciklama = BulVeIsaretle("Aciklama");

            dto.AlanlarEslesti = !string.IsNullOrWhiteSpace(dto.AdSoyadUnvan);

            foreach (var alan in kayit.EnumerateObject())
            {
                if (!eslesenAlanAdlari.Contains(alan.Name))
                {
                    dto.HamVeri[alan.Name] = alan.Value.ValueKind == JsonValueKind.String
                        ? alan.Value.GetString() ?? string.Empty
                        : alan.Value.ToString();
                }
            }

            return dto;
        }
    }
}