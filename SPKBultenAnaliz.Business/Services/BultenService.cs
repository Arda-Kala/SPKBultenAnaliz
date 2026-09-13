using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SPKBultenAnaliz.Core.DTOs;
using SPKBultenAnaliz.Core.Entities;
using SPKBultenAnaliz.Core.Interfaces;

namespace SPKBultenAnaliz.Business.Services
{
    /// <summary>
    /// IBultenService'in somut uygulaması. Scraping, PDF ayrıştırma, chunking
    /// ve Gemini analizini uçtan uca yöneten orkestrasyon (koordinasyon) servisi.
    /// </summary>
    public class BultenService : IBultenService
    {
        private readonly IBultenRepository _repository;
        private readonly ISpkScraperService _scraperService;
        private readonly IPdfParserService _pdfParserService;
        private readonly IAiAnalysisService _aiAnalysisService;
        private readonly ILogger<BultenService> _logger;

        private const int MaksimumBlokBoyutu = 4000;
        private const int FreeTierBeklemeSuresiMs = 5000; // 429 engeline takılmamak için istekler arası 5sn bekleme

        // Uygulama genelinde TEK bir tarama işleminin aynı anda çalışmasını
        // garanti eden kilit. BultenService Scoped ömürlü olduğu için (her
        // istek/scope'ta yeni bir örnek oluşur) bu alanın static olması şart —
        // aksi halde kullanıcı "Tara" butonuna art arda bassa veya arka plan
        // servisiyle manuel tarama çakışsa, SPK sitesine EŞ ZAMANLI birden
        // fazla tarama isteği gider. Bu hem gereksiz yük hem de IP engeli
        // (rate limit / ban) riskini artırır — tam da yaşanan sorunun kaynağı.
        private static readonly SemaphoreSlim TaramaKilidi = new(1, 1);

        public BultenService(
            IBultenRepository repository,
            ISpkScraperService scraperService,
            IPdfParserService pdfParserService,
            IAiAnalysisService aiAnalysisService,
            ILogger<BultenService> logger)
        {
            _repository = repository;
            _scraperService = scraperService;
            _pdfParserService = pdfParserService;
            _aiAnalysisService = aiAnalysisService;
            _logger = logger;
        }

        public async Task<int> YeniBultenleriIsleAsync(CancellationToken cancellationToken = default)
        {
            // Kilidi hemen almayı dene; başka bir tarama zaten çalışıyorsa
            // bekleme, sadece uyar ve çık (kuyruğa girip birikmesin).
            if (!await TaramaKilidi.WaitAsync(0, cancellationToken))
            {
                _logger.LogWarning("Bir tarama işlemi zaten sürüyor; bu istek atlandı (SPK sitesine eş zamanlı yük binmesini önlemek için).");
                return 0;
            }

            try
            {
                return await YeniBultenleriIsleInternalAsync(cancellationToken);
            }
            finally
            {
                TaramaKilidi.Release();
            }
        }

        private async Task<int> YeniBultenleriIsleInternalAsync(CancellationToken cancellationToken)
        {
            var tespitEdilenler = await _scraperService.YeniBultenleriTespitEtAsync(cancellationToken);
            var islenenSayisi = 0;

            foreach (var tespit in tespitEdilenler)
            {
                var mevcutBulten = await _repository.PdfUrlIleGetirAsync(tespit.PdfUrl);

                if (mevcutBulten != null)
                {
                    if (mevcutBulten.Durum == "IslendiOK" && !string.IsNullOrEmpty(mevcutBulten.GenelYoneticiOzeti))
                    {
                        _logger.LogInformation("Bülten zaten başarıyla işlenmiş, atlanıyor: {PdfUrl}", tespit.PdfUrl);
                        continue;
                    }

                    _logger.LogWarning("Bülten DB'de mevcut fakat analizi tamamlanmamış (Durum: {Durum}). Yeniden analiz ediliyor: {PdfUrl}", mevcutBulten.Durum, tespit.PdfUrl);

                    try
                    {
                        await BulteniAnalizEtAsync(mevcutBulten.Id, cancellationToken);
                        islenenSayisi++;

                        // Free Tier Quota Throttling (Her başarılı API çağrısı sonrası bekleme)
                        await Task.Delay(FreeTierBeklemeSuresiMs, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Mevcut bülten yeniden analiz edilirken hata: {PdfUrl}", tespit.PdfUrl);
                    }

                    continue;
                }

                // Tamamen YENİ Bülten Akışı
                try
                {
                    var bulten = new Bulten
                    {
                        BultenAdi = tespit.BultenAdi,
                        YayinTarihi = tespit.YayinTarihi,
                        PdfUrl = tespit.PdfUrl,
                        Durum = "Beklemede"
                    };

                    // 1) PDF indir ve metni ayıkla
                    var pdfBytes = await _scraperService.PdfIndirAsync(tespit.PdfUrl, cancellationToken);
                    var tamMetin = _pdfParserService.MetniAyikla(pdfBytes);
                    var bloklar = _pdfParserService.BloklaraBol(tamMetin, MaksimumBlokBoyutu);

                    for (int i = 0; i < bloklar.Count; i++)
                    {
                        bulten.MetinBloklari.Add(new BultenMetinBlogu
                        {
                            BlokSiraNo = i,
                            HamMetinIcerigi = bloklar[i]
                        });
                    }

                    bulten = await _repository.EkleAsync(bulten);

                    _logger.LogInformation(
                        "Yeni bülten kaydoldu: {BultenAdi} ({BlokSayisi} blok). Gemini analizine geçiliyor.",
                        bulten.BultenAdi, bloklar.Count);

                    // 2) Gemini ile analiz et
                    await BulteniAnalizEtAsync(bulten.Id, cancellationToken);
                    islenenSayisi++;

                    // Free Tier Quota Throttling
                    await Task.Delay(FreeTierBeklemeSuresiMs, cancellationToken);
                }
                catch (GeminiServisException ex)
                {
                    _logger.LogError(ex, "Bülten analizi başarısız oldu: {PdfUrl}", tespit.PdfUrl);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Bülten işlenirken beklenmeyen hata: {PdfUrl}", tespit.PdfUrl);
                }
            }

            return islenenSayisi;
        }

        public async Task<int> BasarisizBultenleriYenidenIsleAsync(CancellationToken cancellationToken = default)
        {
            var basarisizlar = await _repository.DurumaGoreGetirAsync("DogrulamaBekleniyor");
            var beklemedekiler = await _repository.DurumaGoreGetirAsync("Beklemede");

            var Islenecekler = basarisizlar.Concat(beklemedekiler).ToList();
            _logger.LogInformation("Yeniden işlenecek toplam {Sayi} adet bülten bulundu.", Islenecekler.Count);

            int basariliSayisi = 0;

            foreach (var bulten in Islenecekler)
            {
                try
                {
                    await BulteniAnalizEtAsync(bulten.Id, cancellationToken);
                    basariliSayisi++;

                    // Free Tier Quota Throttling
                    await Task.Delay(FreeTierBeklemeSuresiMs, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Bülten ID: {Id} yeniden analiz edilirken hata oluştu.", bulten.Id);
                }
            }

            return basariliSayisi;
        }

        public async Task<Bulten> BulteniAnalizEtAsync(int bultenId, CancellationToken cancellationToken = default)
        {
            var bulten = await _repository.IdyeGoreGetirAsync(bultenId)
                ?? throw new InvalidOperationException($"Bülten bulunamadı: {bultenId}");

            if (bulten.MetinBloklari == null || bulten.MetinBloklari.Count == 0)
            {
                _logger.LogWarning("Bültenin hiç metin bloğu yok, analiz atlanıyor: {BultenId}", bultenId);
                return bulten;
            }

            try
            {
                // 1. BATCHING: Tüm metin bloklarını tek bir metinde birleştiriyoruz (1 Bülten = 1 API İsteği)
                var birlesikMetin = string.Join("\n\n--- BÖLÜM SEPERATÖRÜ ---\n\n",
                    bulten.MetinBloklari.OrderBy(b => b.BlokSiraNo).Select(b => b.HamMetinIcerigi));

                _logger.LogInformation("Bülten ID: {Id} için {Count} adet blok birleştirildi, Gemini API'ye gönderiliyor...", bulten.Id, bulten.MetinBloklari.Count);

                // 2. Gemini 2.0 Flash bağlam kapasitesi yüksek olduğu için tüm bülteni tek seferde analiz eder
                var analizSonucu = await _aiAnalysisService.AnalizEtAsync(birlesikMetin, cancellationToken);

                bulten.GenelYoneticiOzeti = analizSonucu.BultenGenelOzet;
                bulten.Durum = "IslendiOK";

                bulten.AnalizSonuclari.Clear();

                foreach (var etki in analizSonucu.AnalizSonuclari)
                {
                    bulten.AnalizSonuclari.Add(new BultenAnalizSonucu
                    {
                        EtkilenenUnsur = etki.EtkilenenUnsur,
                        Sektor = string.IsNullOrWhiteSpace(etki.Sektor) ? "Diğer" : etki.Sektor,
                        
                        DuyguDurumu = etki.DuyguDurumu,
                        EtkiSkoru = Math.Clamp(etki.EtkiSkoru, 1, 100),
                        Vade = string.IsNullOrWhiteSpace(etki.Vade) ? "Kısa Vadeli" : etki.Vade,
                        HisseKodu = string.IsNullOrWhiteSpace(etki.HisseKodu) ? null : etki.HisseKodu.Trim().ToUpperInvariant(),
                        AiGerekceYorumu = etki.AiGerekceYorumu
                    });
                }

                await _repository.GuncelleAsync(bulten);
                _logger.LogInformation("Bülten ID: {Id} başarıyla analiz edildi ve kaydedildi.", bulten.Id);
            }
            catch (GeminiServisException ex)
            {
                _logger.LogError(ex, "Bülten ID: {Id} analiz edilirken hata oluştu.", bulten.Id);
                bulten.Durum = "DogrulamaBekleniyor";
                await _repository.GuncelleAsync(bulten);
                throw;
            }

            return bulten;
        }

        public async Task<BultenOzetDto?> OzetGetirAsync(int bultenId)
        {
            var bulten = await _repository.IdyeGoreGetirAsync(bultenId);
            return bulten == null ? null : Eslestir(bulten);
        }

        public async Task<List<BultenOzetDto>> TumOzetleriGetirAsync()
        {
            var bultenler = await _repository.TumunuGetirAsync();
            return bultenler.Select(Eslestir).ToList();
        }

        public async Task SilAsync(int bultenId)
        {
            await _repository.SilAsync(bultenId);
        }

        // =====================================================================
        // ADMIN PANELİ: Manuel düzenleme, tekil yeniden analiz, tekil silme
        // =====================================================================

        public async Task<BultenOzetDto> SadeceGenelOzetiYenileAsync(int bultenId, CancellationToken cancellationToken = default)
        {
            var bulten = await _repository.IdyeGoreGetirAsync(bultenId)
                ?? throw new InvalidOperationException($"Bülten bulunamadı: {bultenId}");

            if (bulten.MetinBloklari == null || bulten.MetinBloklari.Count == 0)
                throw new InvalidOperationException("Bültenin metin bloğu yok; önce tam analiz çalıştırılmalı.");

            var birlesikMetin = string.Join("\n\n--- BÖLÜM SEPERATÖRÜ ---\n\n",
                bulten.MetinBloklari.OrderBy(b => b.BlokSiraNo).Select(b => b.HamMetinIcerigi));

            _logger.LogInformation("Bülten ID: {Id} için SADECE genel özet yenileniyor.", bultenId);

            var yeniOzet = await _aiAnalysisService.GenelOzetUretAsync(birlesikMetin, cancellationToken);

            bulten.GenelYoneticiOzeti = yeniOzet;
            await _repository.GuncelleAsync(bulten);

            _logger.LogInformation("Bülten ID: {Id} genel özeti başarıyla yenilendi.", bultenId);

            return Eslestir(bulten);
        }

        public async Task<AnalizSonucuDto> TekHaberiYenidenAnalizEtAsync(int bultenId, int analizId, CancellationToken cancellationToken = default)
        {
            var bulten = await _repository.IdyeGoreGetirAsync(bultenId)
                ?? throw new InvalidOperationException($"Bülten bulunamadı: {bultenId}");

            var mevcutHaber = bulten.AnalizSonuclari.FirstOrDefault(a => a.Id == analizId)
                ?? throw new InvalidOperationException($"Haber bulunamadı: {analizId}");

            if (bulten.MetinBloklari == null || bulten.MetinBloklari.Count == 0)
                throw new InvalidOperationException("Bültenin metin bloğu yok; yeniden analiz edilemiyor.");

            var birlesikMetin = string.Join("\n\n--- BÖLÜM SEPERATÖRÜ ---\n\n",
                bulten.MetinBloklari.OrderBy(b => b.BlokSiraNo).Select(b => b.HamMetinIcerigi));

            _logger.LogInformation("Bülten ID: {BultenId}, Haber ID: {AnalizId} ('{Unsur}') yeniden analiz ediliyor.",
                bultenId, analizId, mevcutHaber.EtkilenenUnsur);

            var yeniSonuc = await _aiAnalysisService.TekUnsuruAnalizEtAsync(birlesikMetin, mevcutHaber.EtkilenenUnsur, cancellationToken);
            mevcutHaber.Sektor = string.IsNullOrWhiteSpace(yeniSonuc.Sektor) ? "Diğer" : yeniSonuc.Sektor;
            mevcutHaber.DuyguDurumu = string.IsNullOrWhiteSpace(yeniSonuc.DuyguDurumu) ? mevcutHaber.DuyguDurumu : yeniSonuc.DuyguDurumu;
            mevcutHaber.EtkiSkoru = yeniSonuc.EtkiSkoru > 0 ? Math.Clamp(yeniSonuc.EtkiSkoru, 1, 100) : mevcutHaber.EtkiSkoru;
            mevcutHaber.Vade = string.IsNullOrWhiteSpace(yeniSonuc.Vade) ? "Kısa Vadeli" : yeniSonuc.Vade;
            mevcutHaber.HisseKodu = string.IsNullOrWhiteSpace(yeniSonuc.HisseKodu) ? null : yeniSonuc.HisseKodu.Trim().ToUpperInvariant();
            mevcutHaber.AiGerekceYorumu = string.IsNullOrWhiteSpace(yeniSonuc.AiGerekceYorumu) ? mevcutHaber.AiGerekceYorumu : yeniSonuc.AiGerekceYorumu;

            await _repository.AnalizSonucuGuncelleAsync(mevcutHaber);

            _logger.LogInformation("Bülten ID: {BultenId}, Haber ID: {AnalizId} başarıyla yeniden analiz edildi.", bultenId, analizId);

            return HaberEslestir(mevcutHaber);
        }

        public async Task<AnalizSonucuDto> HaberiManuelGuncelleAsync(int bultenId, int analizId, AnalizGuncelleRequest istek)
        {
            var haber = await _repository.AnalizSonucuGetirAsync(analizId)
                ?? throw new InvalidOperationException($"Haber bulunamadı: {analizId}");

            if (haber.BultenId != bultenId)
                throw new InvalidOperationException("Bu haber, belirtilen bültene ait değil.");

            // Kısmi güncelleme: sadece istek gövdesinde DOLU gönderilen alanlar değiştirilir.
            if (!string.IsNullOrWhiteSpace(istek.EtkilenenUnsur))
                haber.EtkilenenUnsur = istek.EtkilenenUnsur.Trim();

            if (!string.IsNullOrWhiteSpace(istek.Sektor))
                haber.Sektor = istek.Sektor.Trim();

            if (!string.IsNullOrWhiteSpace(istek.DuyguDurumu))
            {
                var gecerliDegerler = new[] { "Pozitif", "Olumsuz", "Notr" };
                if (!gecerliDegerler.Contains(istek.DuyguDurumu, StringComparer.OrdinalIgnoreCase))
                    throw new ArgumentException("Duygu durumu 'Pozitif', 'Olumsuz' veya 'Notr' olmalıdır.");
                haber.DuyguDurumu = istek.DuyguDurumu.Trim();
            }

            if (istek.EtkiSkoru.HasValue)
            {
                if (istek.EtkiSkoru < 1 || istek.EtkiSkoru > 100)
                    throw new ArgumentException("Etki skoru 1 ile 100 arasında olmalıdır.");
                haber.EtkiSkoru = istek.EtkiSkoru.Value;
            }

            if (!string.IsNullOrWhiteSpace(istek.Vade))
            {
                var gecerliVadeler = new[] { "Kısa Vadeli", "Orta Vadeli", "Uzun Vadeli" };
                if (!gecerliVadeler.Contains(istek.Vade, StringComparer.OrdinalIgnoreCase))
                    throw new ArgumentException("Vade 'Kısa Vadeli', 'Orta Vadeli' veya 'Uzun Vadeli' olmalıdır.");
                haber.Vade = istek.Vade.Trim();
            }

            // Hisse kodu bilinçli olarak boş bırakılmak istenebilir (örn. yanlış
            // girilmiş bir kodu temizlemek için), bu yüzden boş string'i de kabul
            // ederiz — sadece istek alanı hiç gönderilmemişse (null) dokunmayız.
            if (istek.HisseKodu != null)
                haber.HisseKodu = string.IsNullOrWhiteSpace(istek.HisseKodu) ? null : istek.HisseKodu.Trim().ToUpperInvariant();

            if (!string.IsNullOrWhiteSpace(istek.AiGerekceYorumu))
                haber.AiGerekceYorumu = istek.AiGerekceYorumu.Trim();

            await _repository.AnalizSonucuGuncelleAsync(haber);

            _logger.LogInformation("Bülten ID: {BultenId}, Haber ID: {AnalizId} admin tarafından manuel güncellendi.", bultenId, analizId);

            return HaberEslestir(haber);
        }

        public async Task HaberiSilAsync(int bultenId, int analizId)
        {
            var haber = await _repository.AnalizSonucuGetirAsync(analizId)
                ?? throw new InvalidOperationException($"Haber bulunamadı: {analizId}");

            if (haber.BultenId != bultenId)
                throw new InvalidOperationException("Bu haber, belirtilen bültene ait değil.");

            await _repository.AnalizSonucuSilAsync(analizId);

            _logger.LogInformation("Bülten ID: {BultenId}, Haber ID: {AnalizId} silindi.", bultenId, analizId);
        }

        private static AnalizSonucuDto HaberEslestir(BultenAnalizSonucu a) => new()
        {
            Id = a.Id,
            EtkilenenUnsur = a.EtkilenenUnsur,
            Sektor = a.Sektor,
            DuyguDurumu = a.DuyguDurumu,
            EtkiSkoru = a.EtkiSkoru,
            Vade = a.Vade,
            HisseKodu = a.HisseKodu,
            AiGerekceYorumu = a.AiGerekceYorumu
        };

        private static BultenOzetDto Eslestir(Bulten bulten) => new()
        {
            Id = bulten.Id,
            BultenAdi = bulten.BultenAdi,
            YayinTarihi = bulten.YayinTarihi,
            PdfUrl = bulten.PdfUrl,
            GenelYoneticiOzeti = bulten.GenelYoneticiOzeti,
            Durum = bulten.Durum,
            AnalizSonuclari = bulten.AnalizSonuclari.Select(HaberEslestir).ToList()
        };
    }
}