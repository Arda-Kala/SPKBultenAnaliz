using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using SPKBultenAnaliz.Business.Services;
using Xunit;

namespace SPKBultenAnaliz.Tests
{
    /// <summary>
    /// IslemYasaklariService birim testleri. SPK Web Servisi'nden gelen
    /// JSON yanıtlarının ayrıştırılmasını (esnek alan eşleştirme dahil),
    /// hata durumlarını ve önbellekleme (caching) davranışını test eder.
    /// Gerçek ağ çağrısı yapmamak için sahte (fake) bir HttpMessageHandler kullanılır.
    /// </summary>
    public class IslemYasaklariServiceTests
    {
        /// <summary>Testlerde gerçek ağ çağrısı yapmadan önceden tanımlı bir yanıt döndüren sahte handler.</summary>
        private class FakeHttpMessageHandler : HttpMessageHandler
        {
            private readonly HttpStatusCode _statusCode;
            private readonly string _content;
            private readonly Exception? _throwException;
            public int CagriSayisi { get; private set; }

            public FakeHttpMessageHandler(HttpStatusCode statusCode, string content)
            {
                _statusCode = statusCode;
                _content = content;
            }

            public FakeHttpMessageHandler(Exception throwException)
            {
                _throwException = throwException;
                _statusCode = HttpStatusCode.OK;
                _content = string.Empty;
            }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                CagriSayisi++;

                if (_throwException != null)
                    throw _throwException;

                var response = new HttpResponseMessage(_statusCode)
                {
                    Content = new StringContent(_content)
                };
                return Task.FromResult(response);
            }
        }

        private static IslemYasaklariService OlusturSut(FakeHttpMessageHandler handler, out IMemoryCache cache)
        {
            var httpClient = new HttpClient(handler);
            cache = new MemoryCache(new MemoryCacheOptions());
            return new IslemYasaklariService(httpClient, cache, NullLogger<IslemYasaklariService>.Instance);
        }

        #region TumKisileriGetirAsync Tests

        [Fact]
        public async Task TumKisileriGetirAsync_BasariliYanit_KisileriDoner()
        {
            // Arrange
            var json = @"[
                { ""AdSoyad"": ""Ahmet Yılmaz"", ""GercekTuzelKisi"": ""Gerçek Kişi"", ""MkkSicilNo"": ""12345"" },
                { ""AdSoyad"": ""Mehmet Kaya"", ""GercekTuzelKisi"": ""Gerçek Kişi"", ""MkkSicilNo"": ""67890"" }
            ]";

            var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, json);
            var sut = OlusturSut(handler, out _);

            // Act
            var sonuc = await sut.TumKisileriGetirAsync();

            // Assert
            Assert.Equal(2, sonuc.Count);
            Assert.All(sonuc, k => Assert.Equal("Kisi", k.Tip));
            Assert.Contains(sonuc, k => k.AdSoyadUnvan == "Ahmet Yılmaz");
        }

        [Fact]
        public async Task TumKisileriGetirAsync_BosYanit_BosListeDoner()
        {
            // Arrange
            var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "[]");
            var sut = OlusturSut(handler, out _);

            // Act
            var sonuc = await sut.TumKisileriGetirAsync();

            // Assert
            Assert.Empty(sonuc);
        }

        [Fact]
        public async Task TumKisileriGetirAsync_HataliDurumKodu_BosListeDoner()
        {
            // Arrange
            var handler = new FakeHttpMessageHandler(HttpStatusCode.InternalServerError, "");
            var sut = OlusturSut(handler, out _);

            // Act
            var sonuc = await sut.TumKisileriGetirAsync();

            // Assert
            Assert.Empty(sonuc);
        }

        [Fact]
        public async Task TumKisileriGetirAsync_GecersizJson_BosListeDonerVeExceptionFirlatmaz()
        {
            // Arrange
            var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "{ bozuk json ");
            var sut = OlusturSut(handler, out _);

            // Act
            var sonuc = await sut.TumKisileriGetirAsync();

            // Assert
            Assert.Empty(sonuc);
        }

        [Fact]
        public async Task TumKisileriGetirAsync_AgHatasi_BosListeDonerVeExceptionFirlatmaz()
        {
            // Arrange
            var handler = new FakeHttpMessageHandler(new HttpRequestException("Ağ hatası"));
            var sut = OlusturSut(handler, out _);

            // Act
            var sonuc = await sut.TumKisileriGetirAsync();

            // Assert
            Assert.Empty(sonuc);
        }

        [Fact]
        public async Task TumKisileriGetirAsync_SarmalanmisData_DogruAyristirilir()
        {
            // Arrange - API "data" anahtarı ile sarmalanmış bir dizi dönebilir
            var json = @"{ ""data"": [
                { ""unvan"": ""Ali Veli"", ""tip"": ""Kisi"" }
            ]}";

            var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, json);
            var sut = OlusturSut(handler, out _);

            // Act
            var sonuc = await sut.TumKisileriGetirAsync();

            // Assert
            Assert.Single(sonuc);
            Assert.Equal("Ali Veli", sonuc[0].AdSoyadUnvan);
        }

        [Fact]
        public async Task TumKisileriGetirAsync_SonucCachelenir_IkinciCagriHttpYapmaz()
        {
            // Arrange
            var json = @"[{ ""AdSoyad"": ""Test Kişi"" }]";
            var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, json);
            var sut = OlusturSut(handler, out _);

            // Act
            await sut.TumKisileriGetirAsync();
            await sut.TumKisileriGetirAsync();
            await sut.TumKisileriGetirAsync();

            // Assert - Aynı sonuç önbellekten dönmeli, HTTP çağrısı tekrar yapılmamalı
            Assert.Equal(1, handler.CagriSayisi);
        }

        [Fact]
        public async Task TumKisileriGetirAsync_HamVeriEslesmeyenAlanlariSaklar()
        {
            // Arrange
            var json = @"[{ ""AdSoyad"": ""Test Kişi"", ""ozelAlan"": ""ozelDeger"" }]";
            var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, json);
            var sut = OlusturSut(handler, out _);

            // Act
            var sonuc = await sut.TumKisileriGetirAsync();

            // Assert
            Assert.Single(sonuc);
            Assert.True(sonuc[0].HamVeri.ContainsKey("ozelAlan"));
            Assert.Equal("ozelDeger", sonuc[0].HamVeri["ozelAlan"]);
        }

        [Fact]
        public async Task TumKisileriGetirAsync_AlanlarEslesmediyseAlanlarEslestiFalseOlur()
        {
            // Arrange - Ad/unvan alanı olmayan kayıt
            var json = @"[{ ""bilinmeyenAlan"": ""deger"" }]";
            var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, json);
            var sut = OlusturSut(handler, out _);

            // Act
            var sonuc = await sut.TumKisileriGetirAsync();

            // Assert
            Assert.Single(sonuc);
            Assert.False(sonuc[0].AlanlarEslesti);
        }

        [Fact]
        public async Task TumKisileriGetirAsync_KararTarihiGecerliTarihse_Formatlanir()
        {
            // Arrange
            var json = @"[{ ""AdSoyad"": ""Test Kişi"", ""kurulKararTarihi"": ""2026-01-15T00:00:00"" }]";
            var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, json);
            var sut = OlusturSut(handler, out _);

            // Act
            var sonuc = await sut.TumKisileriGetirAsync();

            // Assert
            Assert.Equal("15.01.2026", sonuc[0].KararTarihi);
        }

        #endregion

        #region TumSirketleriGetirAsync Tests

        [Fact]
        public async Task TumSirketleriGetirAsync_BasariliYanit_SirketleriDoner()
        {
            // Arrange
            var json = @"[{ ""AdSoyad"": ""ABC Holding A.Ş."" }]";
            var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, json);
            var sut = OlusturSut(handler, out _);

            // Act
            var sonuc = await sut.TumSirketleriGetirAsync();

            // Assert
            Assert.Single(sonuc);
            Assert.Equal("Sirket", sonuc[0].Tip);
        }

        [Fact]
        public async Task TumSirketleriGetirAsync_BosYanit_BosListeDoner()
        {
            // Arrange
            var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "");
            var sut = OlusturSut(handler, out _);

            // Act
            var sonuc = await sut.TumSirketleriGetirAsync();

            // Assert
            Assert.Empty(sonuc);
        }

        [Fact]
        public async Task TumSirketleriGetirAsync_Cachelenir_TekHttpCagrisiYapar()
        {
            // Arrange
            var json = @"[{ ""AdSoyad"": ""XYZ A.Ş."" }]";
            var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, json);
            var sut = OlusturSut(handler, out _);

            // Act
            await sut.TumSirketleriGetirAsync();
            await sut.TumSirketleriGetirAsync();

            // Assert
            Assert.Equal(1, handler.CagriSayisi);
        }

        [Fact]
        public async Task TumKisileriVeSirketleriGetirAsync_AyriCacheAnahtarlariKullanir()
        {
            // Arrange
            var json = @"[{ ""AdSoyad"": ""Test"" }]";
            var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, json);
            var sut = OlusturSut(handler, out _);

            // Act
            await sut.TumKisileriGetirAsync();
            await sut.TumSirketleriGetirAsync();

            // Assert - Farklı cache anahtarları için 2 ayrı HTTP çağrısı yapılmalı
            Assert.Equal(2, handler.CagriSayisi);
        }

        #endregion

        #region CancellationToken Tests

        [Fact]
        public async Task TumKisileriGetirAsync_CancellationTokenIletilir()
        {
            // Arrange
            var json = @"[]";
            var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, json);
            var sut = OlusturSut(handler, out _);
            using var cts = new CancellationTokenSource();

            // Act & Assert - Exception fırlatmadan tamamlanmalı
            var sonuc = await sut.TumKisileriGetirAsync(cts.Token);
            Assert.NotNull(sonuc);
        }

        #endregion
    }
}
