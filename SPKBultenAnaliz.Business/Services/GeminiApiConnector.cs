using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using SPKBultenAnaliz.Business.Configuration;
using SPKBultenAnaliz.Core.DTOs;
using SPKBultenAnaliz.Core.Interfaces;

namespace SPKBultenAnaliz.Business.Services
{
    /// <summary>
    /// IAiAnalysisService'in Google Gemini API somut uygulaması.
    /// </summary>
    public class GeminiApiConnector : IAiAnalysisService
    {
        private readonly HttpClient _httpClient;
        private readonly GeminiOptions _options;
        private readonly ILogger<GeminiApiConnector> _logger;
        private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;

        private const string TamAnalizSistemPromptu = @"Sen Sermaye Piyasası Kurulu (SPK) kararlarını, hukuki dokümanları ve
finansal bültenleri analiz eden kıdemli bir finans analistisin. Sana
verilen SPK bülten metin bloklarını dikkatlice oku. Metinde doğrudan
veya dolaylı olarak etkilenen şirketleri, sektörleri veya genel piyasa
enstrümanlarını tespit et. Her bir unsur için şu analizleri gerçekleştir:

1. Etkilenen unsurun adını net yaz.
2. Bu unsurun ait olduğu genel sektörü belirle. Şu standart sektör
   isimlerinden EN UYGUN olanını seç: ""Bankacılık"", ""Sigorta"",
   ""Enerji"", ""Gayrimenkul (GYO)"", ""Perakende"", ""Teknoloji"",
   ""Sanayi/Üretim"", ""Ulaştırma/Lojistik"", ""Tarım/Gıda"",
   ""Holding/Yatırım"", ""Aracı Kurum/Portföy Yönetimi"", ""Kamu/Diğer"".
   Hiçbiri net uymuyorsa ""Diğer"" yaz.
3. Bu durumun piyasa/hisse yönünü ""Pozitif"", ""Olumsuz"" veya ""Notr"" olarak belirle.
4. Piyasaya etki gücünü 1-100 arasında puanla (100 = çok güçlü etki, 1 = ihmal edilebilir).
5. Etkinin zaman ufkunu (vade) belirle. Şu üç değerden EN UYGUN olanını seç:
   ""Kısa Vadeli"" (etki günler/haftalar içinde piyasaya yansır, örn. geçici
   işlem durdurma, tedbir kararı), ""Orta Vadeli"" (etki aylar içinde
   yansır, örn. sermaye artırımı, birleşme süreci), ""Uzun Vadeli"" (etki
   çeyreklerce/yıllarca sürer, örn. yapısal düzenleme, lisans iptali).
6. Eğer etkilenen unsur Borsa İstanbul'da işlem gören bir şirketse, resmi
   BIST hisse kodunu (örn. ""GARAN"", ""THYAO"", ""ASELS"") yaz. Şirket
   borsada işlem görmüyorsa, unsur bir sektör/kurum/genel piyasa ise veya
   hisse kodunu kesin bilmiyorsan bu alanı boş string ("""") bırak — ASLA
   tahmini veya uydurma bir kod yazma.
7. Bu analizin finansal gerekçesini ve yorumunu Türkçe olarak açıkla.

Yanıtını, tanımlanan JSON şemasına birebir uyacak şekilde döndür.";

        private const string OzetSistemPromptu = @"Sen Sermaye Piyasası Kurulu (SPK) bültenlerini analiz eden kıdemli bir
finans analistisin. Sana verilen SPK bülten metnini oku ve 3-5 cümlelik,
Türkçe, yönetici özetine uygun kısa ve öz bir genel değerlendirme yaz.
Bu özet, bültendeki en önemli kararları ve genel piyasa etkisini
özetlemelidir. Sadece özet metnini üret, başka açıklama ekleme.";

        private const string TekUnsurSistemPromptuSablonu = @"Sen Sermaye Piyasası Kurulu (SPK) bültenlerini analiz eden kıdemli bir
finans analistisin. Sana verilen SPK bülten metninde, SADECE '{0}' isimli
unsura (şirket/sektör/kurum) odaklan ve onu yeniden değerlendir:

1. Bu unsurun ait olduğu genel sektörü belirle. Şu standart sektör
   isimlerinden EN UYGUN olanını seç: ""Bankacılık"", ""Sigorta"",
   ""Enerji"", ""Gayrimenkul (GYO)"", ""Perakende"", ""Teknoloji"",
   ""Sanayi/Üretim"", ""Ulaştırma/Lojistik"", ""Tarım/Gıda"",
   ""Holding/Yatırım"", ""Aracı Kurum/Portföy Yönetimi"", ""Kamu/Diğer"".
   Hiçbiri net uymuyorsa ""Diğer"" yaz.
2. Piyasa/hisse yönünü ""Pozitif"", ""Olumsuz"" veya ""Notr"" olarak belirle.
3. Piyasaya etki gücünü 1-100 arasında puanla.
4. Etkinin zaman ufkunu (vade) belirle: ""Kısa Vadeli"", ""Orta Vadeli""
   veya ""Uzun Vadeli"".
5. Borsa İstanbul'da işlem gören bir şirketse resmi BIST hisse kodunu yaz;
   değilse veya emin değilsen boş string ("""") bırak — ASLA uydurma.
6. Finansal gerekçeni Türkçe olarak açıkla.

Yanıtını, tanımlanan JSON şemasına birebir uyacak, TEK BİR NESNE (dizi değil)
olarak döndür. 'etkilenenUnsur' alanına tam olarak '{0}' yaz.";

        private const string ChatbotSistemPromptu = @"Sen Sermaye Piyasası Kurulu (SPK) bültenlerini analiz eden kıdemli bir finans asistansı ve soru-cevap uzmanısın.
Sana verilen SPK bülten verilerini (tüm tarih aralığını kapsayan veri setini) dikkatlice incele.
Kullanıcının sorduğu soruya verilen bülten verilerinin TAMAMINA dayanarak Türkçe, kurumsal, detaylı ve net bir cevap ver.
Cevabının altında, kullanıcının konuyla ilgili sorabileceği alakalı 2 adet takip sorusu öner.";


        public GeminiApiConnector(HttpClient httpClient, IOptions<GeminiOptions> options, ILogger<GeminiApiConnector> logger)
        {
            _httpClient = httpClient;
            _options = options.Value;
            _logger = logger;

            // Güvenlik: API Key'i URL parametresi yerine Header olarak ekliyoruz
            if (!_httpClient.DefaultRequestHeaders.Contains("x-goog-api-key") && !string.IsNullOrEmpty(_options.ApiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("x-goog-api-key", _options.ApiKey);
            }

            // Free Tier 429 ve 5xx hatalarında Google'ın ~60sn ceza süresine uyumlu bekleme (10s, 30s, 60s)
            _retryPolicy = Policy
                .HandleResult<HttpResponseMessage>(r =>
                    (int)r.StatusCode == 429 || (int)r.StatusCode >= 500)
                .WaitAndRetryAsync(
                    retryCount: _options.MaksimumYenidenDeneme > 0 ? _options.MaksimumYenidenDeneme : 3,
                    sleepDurationProvider: attempt => attempt switch
                    {
                        1 => TimeSpan.FromSeconds(10),
                        2 => TimeSpan.FromSeconds(30),
                        _ => TimeSpan.FromSeconds(60)
                    },
                    onRetry: (outcome, delay, attempt, _) =>
                        _logger.LogWarning(
                            "Gemini API isteği başarısız (durum: {StatusCode}). Kota/Sunucu koruması için {Attempt}. deneme {Delay} saniye sonra yapılacak.",
                            outcome.Result?.StatusCode, attempt, delay.TotalSeconds));
        }

        public async Task<ChatCevapDto> SoruSorAsync(string soru, string bultenContext, CancellationToken cancellationToken = default)
        {
            var kullaniciIcerigi = $@"Aşağıda veritabanındaki güncel SPK bülten bilgileri yer almaktadır:

--- BÜLTEN VERİLERİ ---
{bultenContext}
----------------------

Kullanıcı Sorusu: {soru}";

            var metinIcerigi = await IstekGonderVeMetinAlAsync(
                ChatbotSistemPromptu, kullaniciIcerigi, GeminiSemaTanimi.ChatbotSemasi(), cancellationToken);

            try
            {
                // Gemini markdown formatında (```json ... ```) döndüyse temizle
                var temizMetin = metinIcerigi.Trim();
                if (temizMetin.StartsWith("```json"))
                    temizMetin = temizMetin.Substring(7);
                if (temizMetin.StartsWith("```"))
                    temizMetin = temizMetin.Substring(3);
                if (temizMetin.EndsWith("```"))
                    temizMetin = temizMetin.Substring(0, temizMetin.Length - 3);

                temizMetin = temizMetin.Trim();

                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var sonuc = JsonSerializer.Deserialize<ChatCevapDto>(temizMetin, jsonOptions);
                if (sonuc == null)
                    throw new JsonException("Deserialize sonucu null döndü.");

                return sonuc;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Gemini Chatbot yanıtı beklenen JSON şemasına uymuyor: {Icerik}", metinIcerigi);
                throw new GeminiServisException("Gemini chatbot yanıtı beklenen formatta değil.", ex);
            }
        }

        public async Task<GeminiAnalizSonucu> AnalizEtAsync(string hamMetin, CancellationToken cancellationToken = default)
        {
            var metinIcerigi = await IstekGonderVeMetinAlAsync(
                TamAnalizSistemPromptu, hamMetin, GeminiSemaTanimi.TamAnalizSemasi(), cancellationToken);

            try
            {
                var sonuc = JsonSerializer.Deserialize<GeminiAnalizSonucu>(metinIcerigi);
                if (sonuc == null)
                    throw new JsonException("Deserialize sonucu null döndü.");

                return sonuc;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Gemini yanıtı beklenen JSON şemasına uymuyor: {Icerik}", metinIcerigi);
                throw new GeminiServisException("Gemini yanıtı beklenen formatta değil (bozuk JSON).", ex);
            }
        }

        public async Task<string> GenelOzetUretAsync(string hamMetin, CancellationToken cancellationToken = default)
        {
            var metinIcerigi = await IstekGonderVeMetinAlAsync(
                OzetSistemPromptu, hamMetin, GeminiSemaTanimi.OzetSemasi(), cancellationToken);

            try
            {
                using var doc = JsonDocument.Parse(metinIcerigi);
                if (doc.RootElement.TryGetProperty("ozet", out var ozetElemani))
                    return ozetElemani.GetString() ?? string.Empty;

                throw new JsonException("'ozet' alanı bulunamadı.");
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Gemini özet yanıtı beklenen JSON şemasına uymuyor: {Icerik}", metinIcerigi);
                throw new GeminiServisException("Gemini özet yanıtı beklenen formatta değil (bozuk JSON).", ex);
            }
        }

        public async Task<GeminiEtkiUnsuru> TekUnsuruAnalizEtAsync(string hamMetin, string unsurAdi, CancellationToken cancellationToken = default)
        {
            var sistemPromptu = string.Format(TekUnsurSistemPromptuSablonu, unsurAdi);

            var metinIcerigi = await IstekGonderVeMetinAlAsync(
                sistemPromptu, hamMetin, GeminiSemaTanimi.TekUnsurSemasi(), cancellationToken);

            try
            {
                var sonuc = JsonSerializer.Deserialize<GeminiEtkiUnsuru>(metinIcerigi);
                if (sonuc == null)
                    throw new JsonException("Deserialize sonucu null döndü.");

                return sonuc;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Gemini tekil unsur yanıtı beklenen JSON şemasına uymuyor: {Icerik}", metinIcerigi);
                throw new GeminiServisException("Gemini yanıtı beklenen formatta değil (bozuk JSON).", ex);
            }
        }

        /// <summary>
        /// Ortak düşük seviyeli çağrı: sistem promptu + kullanıcı içeriği +
        /// şemayı Gemini'ye gönderir, retry/timeout/hata yönetimini yapar ve
        /// modelin ürettiği ham JSON metnini döner. Deserialize işlemi,
        /// beklenen tipe göre her bir üst seviye metotta ayrı yapılır.
        /// </summary>
        private async Task<string> IstekGonderVeMetinAlAsync(
            string sistemPromptu, string kullaniciIcerigi, object sema, CancellationToken cancellationToken)
        {
            var url = $"{_options.BaseUrl}/{_options.Model}:generateContent";

            var istek = new GeminiRequest
            {
                SystemInstruction = new GeminiSystemInstruction
                {
                    Parts = new List<GeminiPart> { new() { Text = sistemPromptu } }
                },
                Contents = new List<GeminiContent>
                {
                    new()
                    {
                        Role = "user",
                        Parts = new List<GeminiPart> { new() { Text = kullaniciIcerigi } }
                    }
                },
                GenerationConfig = new GeminiGenerationConfig
                {
                    ResponseMimeType = "application/json",
                    Temperature = 0.2,
                    ResponseSchema = sema
                }
            };

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var timeoutSaniye = _options.TimeoutSaniye >= 120 ? _options.TimeoutSaniye : 180;
            cts.CancelAfter(TimeSpan.FromSeconds(timeoutSaniye));

            HttpResponseMessage yanit;
            try
            {
                yanit = await _retryPolicy.ExecuteAsync(
                    ct => _httpClient.PostAsJsonAsync(url, istek, ct), cts.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.LogError("Gemini API isteği zaman aşımına uğradı ({Timeout} sn).", timeoutSaniye);
                throw new GeminiServisException($"Gemini API zaman aşımına uğradı ({timeoutSaniye} sn). Lütfen daha sonra tekrar deneyin.");
            }

            if (!yanit.IsSuccessStatusCode)
            {
                var hataIcerigi = await yanit.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Gemini API hata döndürdü: {StatusCode} - {Icerik}", yanit.StatusCode, hataIcerigi);
                throw new GeminiServisException($"Gemini API hatası: {yanit.StatusCode}");
            }

            var gemniYaniti = await yanit.Content.ReadFromJsonAsync<GeminiResponse>(cancellationToken: cancellationToken);
            var metinIcerigi = gemniYaniti?.Candidates?.Count > 0
                ? gemniYaniti.Candidates[0].Content?.Parts?.Count > 0
                    ? gemniYaniti.Candidates[0].Content!.Parts[0].Text
                    : null
                : null;

            if (string.IsNullOrWhiteSpace(metinIcerigi))
            {
                _logger.LogError("Gemini API boş yanıt döndürdü.");
                throw new GeminiServisException("Gemini API'den geçerli bir içerik alınamadı.");
            }

            return metinIcerigi;
        }
    }

    public class GeminiServisException : Exception
    {
        public GeminiServisException(string message) : base(message) { }
        public GeminiServisException(string message, Exception inner) : base(message, inner) { }
    }

    public static class GeminiSemaTanimi
    {
        private static object EtkiUnsuruAlanlari() => new
        {
            etkilenenUnsur = new { type = "STRING" },
            sektor = new { type = "STRING" },
            duyguDurumu = new { type = "STRING" },
            etkiSkoru = new { type = "INTEGER" },
            vade = new { type = "STRING" },
            hisseKodu = new { type = "STRING" },
            aiGerekceYorumu = new { type = "STRING" }
        };

        private static readonly string[] EtkiUnsuruZorunluAlanlar =
            { "etkilenenUnsur", "sektor", "duyguDurumu", "etkiSkoru", "vade", "hisseKodu", "aiGerekceYorumu" };

        public static object TamAnalizSemasi() => new
        {
            type = "OBJECT",
            properties = new
            {
                bultenGenelOzet = new { type = "STRING" },
                analizSonuclari = new
                {
                    type = "ARRAY",
                    items = new
                    {
                        type = "OBJECT",
                        properties = EtkiUnsuruAlanlari(),
                        required = EtkiUnsuruZorunluAlanlar
                    }
                }
            },
            required = new[] { "bultenGenelOzet", "analizSonuclari" }
        };

        /// <summary>Sadece "sadece genel özeti yenile" eylemi için kullanılan minimal şema.</summary>
        public static object OzetSemasi() => new
        {
            type = "OBJECT",
            properties = new { ozet = new { type = "STRING" } },
            required = new[] { "ozet" }
        };

        /// <summary>Tek bir unsurun (haberin) yeniden analizi için — dizi değil, tek nesne döner.</summary>
        public static object TekUnsurSemasi() => new
        {
            type = "OBJECT",
            properties = EtkiUnsuruAlanlari(),
            required = EtkiUnsuruZorunluAlanlar
        };
        /// <summary>Chatbot soru-cevap işlemi için yanıt şeması.</summary>
        public static object ChatbotSemasi() => new
        {
            type = "OBJECT",
            properties = new
            {
                cevap = new { type = "STRING" },
                onerilenSorular = new
                {
                    type = "ARRAY",
                    items = new { type = "STRING" }
                }
            },
            required = new[] { "cevap", "onerilenSorular" }
        };

    }
}
