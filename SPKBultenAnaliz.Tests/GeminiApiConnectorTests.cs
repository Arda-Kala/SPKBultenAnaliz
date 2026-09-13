using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SPKBultenAnaliz.Business.Configuration;
using SPKBultenAnaliz.Business.Services;
using Xunit;

namespace SPKBultenAnaliz.Tests
{
    /// <summary>
    /// GeminiApiConnector birim testleri. Gemini API'den dönen yanıtların
    /// doğru şekilde deserialize edildiğini, hata durumlarında
    /// GeminiServisException fırlatıldığını doğrular.
    ///
    /// NOT: 429/5xx durum kodları Polly retry politikasını tetikler (10s/30s/60s
    /// bekleme süreleriyle) — bu nedenle testlerde YALNIZCA retry TETİKLEMEYEN
    /// durum kodları (200, 400 gibi) kullanılır; aksi halde testler dakikalarca sürer.
    ///
    /// Sahte Gemini yanıtları elle string birleştirmek yerine JsonSerializer ile
    /// üretilir; bu, kaçış (escaping) hatalarını önler ve testleri daha okunur kılar.
    /// </summary>
    public class GeminiApiConnectorTests
    {
        private class FakeHttpMessageHandler : HttpMessageHandler
        {
            private readonly HttpStatusCode _statusCode;
            private readonly string _content;

            public FakeHttpMessageHandler(HttpStatusCode statusCode, string content)
            {
                _statusCode = statusCode;
                _content = content;
            }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var response = new HttpResponseMessage(_statusCode)
                {
                    Content = new StringContent(_content)
                };
                return Task.FromResult(response);
            }
        }

        private static GeminiApiConnector OlusturSut(HttpStatusCode statusCode, string content)
        {
            var handler = new FakeHttpMessageHandler(statusCode, content);
            var httpClient = new HttpClient(handler);
            var options = Options.Create(new GeminiOptions
            {
                ApiKey = "test-key",
                Model = "gemini-test",
                BaseUrl = "https://example.com/models",
                TimeoutSaniye = 120,
                MaksimumYenidenDeneme = 3
            });

            return new GeminiApiConnector(httpClient, options, NullLogger<GeminiApiConnector>.Instance);
        }

        /// <summary>
        /// Verilen iç metni (modelin ürettiği JSON metni) Gemini API zarfına
        /// (candidates -> content -> parts -> text) sarıp geçerli JSON string üretir.
        /// </summary>
        private static string GeminiZarfiOlustur(string modelinUrettigiMetin)
        {
            var zarf = new
            {
                candidates = new object[]
                {
                    new
                    {
                        content = new
                        {
                            role = "model",
                            parts = new object[] { new { text = modelinUrettigiMetin } }
                        }
                    }
                }
            };

            return JsonSerializer.Serialize(zarf);
        }

        #region AnalizEtAsync Tests

        [Fact]
        public async Task AnalizEtAsync_BasariliYanit_DogruDeserializeEdilir()
        {
            // Arrange
            var innerObj = new
            {
                bultenGenelOzet = "Test özet metni",
                analizSonuclari = new object[]
                {
                    new
                    {
                        etkilenenUnsur = "ABC Holding",
                        sektor = "Holding/Yatırım",
                        duyguDurumu = "Pozitif",
                        etkiSkoru = 75,
                        vade = "Kısa Vadeli",
                        hisseKodu = "ABCH",
                        aiGerekceYorumu = "Test gerekçe"
                    }
                }
            };
            var innerJson = JsonSerializer.Serialize(innerObj);
            var apiResponse = GeminiZarfiOlustur(innerJson);

            var sut = OlusturSut(HttpStatusCode.OK, apiResponse);

            // Act
            var sonuc = await sut.AnalizEtAsync("örnek ham metin");

            // Assert
            Assert.Equal("Test özet metni", sonuc.BultenGenelOzet);
            Assert.Single(sonuc.AnalizSonuclari);
            Assert.Equal("ABC Holding", sonuc.AnalizSonuclari[0].EtkilenenUnsur);
            Assert.Equal(75, sonuc.AnalizSonuclari[0].EtkiSkoru);
        }

        [Fact]
        public async Task AnalizEtAsync_BozukJsonIcerik_GeminiServisExceptionFirlatir()
        {
            // Arrange - Modelin döndürdüğü metin geçerli JSON değil
            var apiResponse = GeminiZarfiOlustur("bozuk-json-degil");
            var sut = OlusturSut(HttpStatusCode.OK, apiResponse);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<GeminiServisException>(
                () => sut.AnalizEtAsync("herhangi bir metin"));
            Assert.Contains("bozuk JSON", ex.Message);
        }

        [Fact]
        public async Task AnalizEtAsync_BosCandidates_GeminiServisExceptionFirlatir()
        {
            // Arrange
            var apiResponse = JsonSerializer.Serialize(new { candidates = new object[0] });
            var sut = OlusturSut(HttpStatusCode.OK, apiResponse);

            // Act & Assert
            await Assert.ThrowsAsync<GeminiServisException>(
                () => sut.AnalizEtAsync("herhangi bir metin"));
        }

        [Fact]
        public async Task AnalizEtAsync_HataliIstek400_GeminiServisExceptionFirlatir()
        {
            // Arrange - 400 Bad Request retry'ı tetiklemez (sadece 429/5xx tetikler)
            var sut = OlusturSut(HttpStatusCode.BadRequest, JsonSerializer.Serialize(new { error = "invalid request" }));

            // Act & Assert
            var ex = await Assert.ThrowsAsync<GeminiServisException>(
                () => sut.AnalizEtAsync("herhangi bir metin"));
            Assert.Contains("Gemini API hatası", ex.Message);
        }

        [Fact]
        public async Task AnalizEtAsync_Unauthorized401_GeminiServisExceptionFirlatir()
        {
            // Arrange
            var sut = OlusturSut(HttpStatusCode.Unauthorized, JsonSerializer.Serialize(new { error = "unauthorized" }));

            // Act & Assert
            await Assert.ThrowsAsync<GeminiServisException>(
                () => sut.AnalizEtAsync("herhangi bir metin"));
        }

        #endregion

        #region GenelOzetUretAsync Tests

        [Fact]
        public async Task GenelOzetUretAsync_BasariliYanit_OzetiDoner()
        {
            // Arrange
            var innerJson = JsonSerializer.Serialize(new { ozet = "Bu bir test özetidir." });
            var apiResponse = GeminiZarfiOlustur(innerJson);
            var sut = OlusturSut(HttpStatusCode.OK, apiResponse);

            // Act
            var ozet = await sut.GenelOzetUretAsync("örnek metin");

            // Assert
            Assert.Equal("Bu bir test özetidir.", ozet);
        }

        [Fact]
        public async Task GenelOzetUretAsync_OzetAlaniYok_GeminiServisExceptionFirlatir()
        {
            // Arrange
            var innerJson = JsonSerializer.Serialize(new { baskaAlan = "deger" });
            var apiResponse = GeminiZarfiOlustur(innerJson);
            var sut = OlusturSut(HttpStatusCode.OK, apiResponse);

            // Act & Assert
            await Assert.ThrowsAsync<GeminiServisException>(
                () => sut.GenelOzetUretAsync("örnek metin"));
        }

        #endregion

        #region TekUnsuruAnalizEtAsync Tests

        [Fact]
        public async Task TekUnsuruAnalizEtAsync_BasariliYanit_TekNesneDoner()
        {
            // Arrange
            var innerObj = new
            {
                etkilenenUnsur = "XYZ A.Ş.",
                sektor = "Teknoloji",
                duyguDurumu = "Olumsuz",
                etkiSkoru = 40,
                vade = "Orta Vadeli",
                hisseKodu = "XYZAS",
                aiGerekceYorumu = "Gerekçe metni"
            };
            var innerJson = JsonSerializer.Serialize(innerObj);
            var apiResponse = GeminiZarfiOlustur(innerJson);
            var sut = OlusturSut(HttpStatusCode.OK, apiResponse);

            // Act
            var sonuc = await sut.TekUnsuruAnalizEtAsync("örnek metin", "XYZ A.Ş.");

            // Assert
            Assert.Equal("XYZ A.Ş.", sonuc.EtkilenenUnsur);
            Assert.Equal("Teknoloji", sonuc.Sektor);
            Assert.Equal(40, sonuc.EtkiSkoru);
        }

        [Fact]
        public async Task TekUnsuruAnalizEtAsync_BozukJson_GeminiServisExceptionFirlatir()
        {
            // Arrange
            var apiResponse = GeminiZarfiOlustur("gecersiz");
            var sut = OlusturSut(HttpStatusCode.OK, apiResponse);

            // Act & Assert
            await Assert.ThrowsAsync<GeminiServisException>(
                () => sut.TekUnsuruAnalizEtAsync("örnek metin", "Herhangi Unsur"));
        }

        #endregion

        #region GeminiServisException Tests

        [Fact]
        public void GeminiServisException_MessageOnly_MessageSetCorrectly()
        {
            // Act
            var ex = new GeminiServisException("Test hata mesajı");

            // Assert
            Assert.Equal("Test hata mesajı", ex.Message);
            Assert.Null(ex.InnerException);
        }

        [Fact]
        public void GeminiServisException_WithInnerException_InnerExceptionSetCorrectly()
        {
            // Arrange
            var innerEx = new JsonException("İç hata");

            // Act
            var ex = new GeminiServisException("Dış hata", innerEx);

            // Assert
            Assert.Equal("Dış hata", ex.Message);
            Assert.Same(innerEx, ex.InnerException);
        }

        #endregion

        #region GeminiSemaTanimi Tests

        [Fact]
        public void GeminiSemaTanimi_TamAnalizSemasi_NotNull()
        {
            var sema = GeminiSemaTanimi.TamAnalizSemasi();
            Assert.NotNull(sema);
        }

        [Fact]
        public void GeminiSemaTanimi_OzetSemasi_NotNull()
        {
            var sema = GeminiSemaTanimi.OzetSemasi();
            Assert.NotNull(sema);
        }

        [Fact]
        public void GeminiSemaTanimi_TekUnsurSemasi_NotNull()
        {
            var sema = GeminiSemaTanimi.TekUnsurSemasi();
            Assert.NotNull(sema);
        }

        #endregion
    }
}
