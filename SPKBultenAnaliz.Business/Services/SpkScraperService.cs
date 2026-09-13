using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using SPKBultenAnaliz.Core.Interfaces;

namespace SPKBultenAnaliz.Business.Services
{
    /// <summary>
    /// ISpkScraperService'in somut uygulaması. SPK'nın resmi bülten listesi
    /// sayfalarını (?s=1, ?s=2, ...) HtmlAgilityPack ile sırayla tarar.
    ///
    /// GÜNCELLEME (sayfalama + kota koruması):
    /// - Artık tek sayfa değil, "sonraki sayfa" linki kalmayana veya
    ///   MaksimumSayfaSayisi'na ulaşana kadar tüm sayfalar taranır.
    /// - Sayfalar arasına ve PDF indirmeleri arasına kısa bir bekleme
    ///   (istekler arası gecikme) eklendi.
    /// - User-Agent, sıradan bir tarayıcı User-Agent'ına çevrildi (bkz. Program.cs).
    /// - Sayfalama tespiti artık gerçek "spk-pagination" konteynerine göre yapılıyor.
    /// </summary>
    public class SpkScraperService : ISpkScraperService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<SpkScraperService> _logger;

        private const int MaksimumSayfaSayisi = 6;
        private static readonly TimeSpan IstekArasiGecikme = TimeSpan.FromSeconds(2.5);

        private static readonly Regex BultenSatiriDeseni = new(
            @"Bülten\s*No\s*:\s*(?<no>[\d]{4}/[\d]+)\s*Yayımlanma\s*:\s*(?<gun>\d{1,2})\s+(?<ay>[A-Za-zÇçĞğİıÖöŞşÜü]+)\s+(?<yil>\d{4})",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        private static readonly CultureInfo TurkceKultur = new("tr-TR");

        public SpkScraperService(HttpClient httpClient, ILogger<SpkScraperService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        private static string BultenListesiUrl(int yil, int sayfa) =>
            sayfa <= 1
                ? $"https://spk.gov.tr/spk-bultenleri/{yil}-yili-spk-bultenleri"
                : $"https://spk.gov.tr/spk-bultenleri/{yil}-yili-spk-bultenleri?s={sayfa}";

        public async Task<List<TespitEdilenBulten>> YeniBultenleriTespitEtAsync(CancellationToken cancellationToken = default)
        {
            var simdikiYil = DateTime.UtcNow.Year;

            var sonuclar = await YilinTumSayfalariniTaraAsync(simdikiYil, cancellationToken);

            if (sonuclar.Count == 0 && DateTime.UtcNow.Month <= 2)
            {
                _logger.LogInformation("{Yil} yılı için sonuç bulunamadı, {OncekiYil} deneniyor.", simdikiYil, simdikiYil - 1);
                sonuclar = await YilinTumSayfalariniTaraAsync(simdikiYil - 1, cancellationToken);
            }

            return sonuclar;
        }

        private async Task<List<TespitEdilenBulten>> YilinTumSayfalariniTaraAsync(int yil, CancellationToken cancellationToken)
        {
            var tumSonuclar = new List<TespitEdilenBulten>();

            for (int sayfa = 1; sayfa <= MaksimumSayfaSayisi; sayfa++)
            {
                var url = BultenListesiUrl(yil, sayfa);
                List<TespitEdilenBulten> sayfaSonuclari;
                bool sonrakiSayfaVarMi;

                try
                {
                    (sayfaSonuclari, sonrakiSayfaVarMi) = await TekSayfayiTaraAsync(url, cancellationToken);
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogError(ex,
                        "SPK web sitesine erişilemedi (sayfa {Sayfa}, {Url}). Muhtemel sebepler: geçici ağ " +
                        "sorunu veya çok sık istek nedeniyle sunucu tarafında geçici erişim kısıtlaması. " +
                        "Bir süre bekleyip tekrar deneyin; sürekli tekrarlanan taramalar durumu kötüleştirebilir.",
                        sayfa, url);
                    break;
                }
                catch (TaskCanceledException ex)
                {
                    _logger.LogError(ex, "SPK web sitesi isteği zaman aşımına uğradı (sayfa {Sayfa}).", sayfa);
                    break;
                }

                if (sayfaSonuclari.Count == 0)
                {
                    _logger.LogInformation("Sayfa {Sayfa}'de bülten bulunamadı, tarama durduruluyor.", sayfa);
                    break;
                }

                tumSonuclar.AddRange(sayfaSonuclari);
                _logger.LogInformation("Sayfa {Sayfa}: {Sayi} bülten linki tespit edildi ({Url}).", sayfa, sayfaSonuclari.Count, url);

                if (!sonrakiSayfaVarMi)
                    break;

                if (sayfa < MaksimumSayfaSayisi)
                    await Task.Delay(IstekArasiGecikme, cancellationToken);
            }

            return tumSonuclar;
        }

        private async Task<(List<TespitEdilenBulten> Sonuclar, bool SonrakiSayfaVarMi)> TekSayfayiTaraAsync(
            string url, CancellationToken cancellationToken)
        {
            var sonuclar = new List<TespitEdilenBulten>();

            var html = await _httpClient.GetStringAsync(url, cancellationToken);

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var listeDiv = doc.DocumentNode.SelectSingleNode("//div[@class='liste']");

            if (listeDiv == null)
            {
                _logger.LogWarning("SPK bülten liste kapsayıcısı (.liste) bulunamadı ({Url}). HTML yapısı değişmiş olabilir.", url);
                return (sonuclar, false);
            }

            var linkler = listeDiv.SelectNodes(".//a[contains(@href, '.pdf')]");

            if (linkler != null)
            {
                foreach (var link in linkler)
                {
                    var href = link.GetAttributeValue("href", string.Empty);

                    var baslikNode = link.SelectSingleNode(".//div[contains(@class, 'liste-baslik')]");
                    var icerikNode = link.SelectSingleNode(".//div[contains(@class, 'liste-icerik')]");

                    string hamMetin;
                    if (baslikNode != null && icerikNode != null)
                    {
                        var baslik = HtmlEntity.DeEntitize(baslikNode.InnerText)?.Trim();
                        var icerik = HtmlEntity.DeEntitize(icerikNode.InnerText)?.Trim();
                        hamMetin = $"{baslik} {icerik}";
                    }
                    else
                    {
                        hamMetin = HtmlEntity.DeEntitize(link.InnerText)?.Trim() ?? string.Empty;
                    }

                    if (string.IsNullOrWhiteSpace(href))
                        continue;

                    var tespit = MetniAyristir(hamMetin, href);
                    if (tespit != null)
                        sonuclar.Add(tespit);
                }
            }

            // Sayfalama alanındaki "sonraki sayfa" (>) linkine bakarak daha ileri
            // sayfa olup olmadığını tespit et.
            var paginationNode = doc.DocumentNode.SelectSingleNode("//div[contains(@class,'spk-pagination')]");
            bool sonrakiSayfaVarMi = false;

            if (paginationNode != null)
            {
                var sonrakiLink = paginationNode.SelectNodes(".//a")
                    ?.FirstOrDefault(a =>
                        (a.InnerText.Contains(">") || a.InnerText.Contains("&gt;")) &&
                        !a.GetAttributeValue("class", "").Contains("disabled") &&
                        a.GetAttributeValue("href", "#") != "#");

                sonrakiSayfaVarMi = sonrakiLink != null;
            }

            return (sonuclar, sonrakiSayfaVarMi);
        }

        private TespitEdilenBulten? MetniAyristir(string linkMetni, string href)
        {
            var temizMetin = Regex.Replace(linkMetni, @"\s+", " ");
            var eslesme = BultenSatiriDeseni.Match(temizMetin);

            if (!eslesme.Success)
            {
                _logger.LogDebug("Bülten satırı beklenen formatla eşleşmedi, atlanıyor: '{Metin}'", temizMetin);
                return null;
            }

            var bultenNo = eslesme.Groups["no"].Value;
            var gun = int.Parse(eslesme.Groups["gun"].Value);
            var ayAdi = eslesme.Groups["ay"].Value;
            var yil = int.Parse(eslesme.Groups["yil"].Value);

            DateTime yayinTarihi;
            try
            {
                var tarihMetni = $"{gun} {ayAdi} {yil}";
                yayinTarihi = DateTime.Parse(tarihMetni, TurkceKultur, DateTimeStyles.None);
            }
            catch (FormatException)
            {
                _logger.LogWarning("Tarih ayrıştırılamadı: '{Gun} {Ay} {Yil}', bugünün tarihi kullanılacak.", gun, ayAdi, yil);
                yayinTarihi = DateTime.UtcNow.Date;
            }

            return new TespitEdilenBulten
            {
                BultenAdi = $"SPK Bülteni {bultenNo}",
                PdfUrl = href,
                YayinTarihi = yayinTarihi
            };
        }

        public async Task<byte[]> PdfIndirAsync(string pdfUrl, CancellationToken cancellationToken = default)
        {
            await Task.Delay(IstekArasiGecikme, cancellationToken);
            return await _httpClient.GetByteArrayAsync(pdfUrl, cancellationToken);
        }
    }
}
