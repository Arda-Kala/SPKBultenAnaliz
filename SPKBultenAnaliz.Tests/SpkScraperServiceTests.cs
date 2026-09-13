using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using SPKBultenAnaliz.Business.Services;
using SPKBultenAnaliz.Core.Interfaces;
using Xunit;

namespace SPKBultenAnaliz.Tests
{
    /// <summary>
    /// SpkScraperService birim testleri. SPK web sitesinin bülten liste
    /// sayfalarını HTML olarak tarayan, bülten satırlarını regex ile
    /// ayrıştıran, sayfalamayı takip eden ve hata durumlarında akışı
    /// güvenli şekilde sonlandıran mantığı test eder. Gerçek ağ çağrısı
    /// yapılmaz; HttpClient'ın altına sahte (fake) bir HttpMessageHandler
    /// yerleştirilir ve istenen URL'ye göre önceden tanımlı HTML/hata
    /// yanıtları döndürülür.
    /// </summary>
    public class SpkScraperServiceTests
    {
        /// <summary>
        /// Test amaçlı, istek URL'sine göre yanıt üreten (veya istisna
        /// fırlatan) sahte HttpMessageHandler. Gerçek SPK sunucusuna hiçbir
        /// zaman gitmez.
        /// </summary>
        private class FakeHttpMessageHandler : HttpMessageHandler
        {
            private readonly Dictionary<string, Func<HttpResponseMessage>> _yanitlar = new();
            private readonly Dictionary<string, Exception> _hatalar = new();
            public List<string> IstenenUrller { get; } = new();

            public FakeHttpMessageHandler YanitEkle(string url, string html)
            {
                _yanitlar[url] = () => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(html, Encoding.UTF8, "text/html")
                };
                return this;
            }

            public FakeHttpMessageHandler ByteYanitEkle(string url, byte[] bytes)
            {
                _yanitlar[url] = () => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(bytes)
                };
                return this;
            }

            public FakeHttpMessageHandler HataEkle(string url, Exception ex)
            {
                _hatalar[url] = ex;
                return this;
            }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var url = request.RequestUri!.ToString();
                IstenenUrller.Add(url);

                if (_hatalar.TryGetValue(url, out var hata))
                    throw hata;

                if (_yanitlar.TryGetValue(url, out var yanitUretici))
                    return Task.FromResult(yanitUretici());

                // Eşleşmeyen URL'ler için 404 döndür (sonraki sayfaların taramayı durdurması için).
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("<html><body></body></html>", Encoding.UTF8, "text/html")
                });
            }
        }

        private static SpkScraperService OlusturSut(FakeHttpMessageHandler handler)
        {
            var httpClient = new HttpClient(handler);
            return new SpkScraperService(httpClient, NullLogger<SpkScraperService>.Instance);
        }

        private static int GuncelYil => DateTime.UtcNow.Year;

        private static string Sayfa1Url => $"https://spk.gov.tr/spk-bultenleri/{GuncelYil}-yili-spk-bultenleri";
        private static string Sayfa2Url => $"https://spk.gov.tr/spk-bultenleri/{GuncelYil}-yili-spk-bultenleri?s=2";
        private static string Sayfa3Url => $"https://spk.gov.tr/spk-bultenleri/{GuncelYil}-yili-spk-bultenleri?s=3";

        private static string TekBultenliHtml(string bultenNo, string gun, string ayAdi, string yil, string pdfHref, bool sonrakiSayfaVar)
        {
            var pagination = sonrakiSayfaVar
                ? @"<div class=""spk-pagination""><a href=""?s=2"">&gt;</a></div>"
                : @"<div class=""spk-pagination""><a href=""#"" class=""disabled"">&gt;</a></div>";

            return $@"
<html><body>
<div class=""liste"">
    <a href=""{pdfHref}"">
        <div class=""liste-baslik"">SPK Bülteni</div>
        <div class=""liste-icerik"">Bülten No : {bultenNo} Yayımlanma : {gun} {ayAdi} {yil}</div>
    </a>
</div>
{pagination}
</body></html>";
        }

        #region YeniBultenleriTespitEtAsync - Temel Ayrıştırma

        [Fact]
        public async Task YeniBultenleriTespitEtAsync_GecerliHtml_BultenTespitEder()
        {
            // Arrange
            var html = TekBultenliHtml("2026/42", "15", "Ocak", GuncelYil.ToString(),
                "https://spk.gov.tr/Sayfalar/dosya/2026-42.pdf", sonrakiSayfaVar: false);

            var handler = new FakeHttpMessageHandler().YanitEkle(Sayfa1Url, html);
            var sut = OlusturSut(handler);

            // Act
            var sonuc = await sut.YeniBultenleriTespitEtAsync();

            // Assert
            Assert.Single(sonuc);
            Assert.Equal("SPK Bülteni 2026/42", sonuc[0].BultenAdi);
            Assert.Equal("https://spk.gov.tr/Sayfalar/dosya/2026-42.pdf", sonuc[0].PdfUrl);
            Assert.Equal(15, sonuc[0].YayinTarihi.Day);
            Assert.Equal(1, sonuc[0].YayinTarihi.Month); // Ocak
        }

        [Fact]
        public async Task YeniBultenleriTespitEtAsync_ListeDivYoksa_BosListeDoner()
        {
            // Arrange
            var html = "<html><body><div class=\"baska-bir-yapi\">İçerik yok</div></body></html>";
            var handler = new FakeHttpMessageHandler().YanitEkle(Sayfa1Url, html);
            var sut = OlusturSut(handler);

            // Act
            var sonuc = await sut.YeniBultenleriTespitEtAsync();

            // Assert
            Assert.Empty(sonuc);
        }

        [Fact]
        public async Task YeniBultenleriTespitEtAsync_PdfLinkiYoksa_BosListeDoner()
        {
            // Arrange
            var html = @"<html><body><div class=""liste""><a href=""/baska-sayfa.html"">İlgisiz link</a></div></body></html>";
            var handler = new FakeHttpMessageHandler().YanitEkle(Sayfa1Url, html);
            var sut = OlusturSut(handler);

            // Act
            var sonuc = await sut.YeniBultenleriTespitEtAsync();

            // Assert
            Assert.Empty(sonuc);
        }

        [Fact]
        public async Task YeniBultenleriTespitEtAsync_FormatiUymayanSatir_Atlanir()
        {
            // Arrange - "Bülten No" deseniyle eşleşmeyen bir metin
            var html = @"
<html><body>
<div class=""liste"">
    <a href=""https://spk.gov.tr/dosya/duyuru.pdf"">
        <div class=""liste-baslik"">Genel Duyuru</div>
        <div class=""liste-icerik"">Bu bir bülten değildir, farklı bir duyurudur.</div>
    </a>
</div>
</body></html>";
            var handler = new FakeHttpMessageHandler().YanitEkle(Sayfa1Url, html);
            var sut = OlusturSut(handler);

            // Act
            var sonuc = await sut.YeniBultenleriTespitEtAsync();

            // Assert
            Assert.Empty(sonuc);
        }

        [Fact]
        public async Task YeniBultenleriTespitEtAsync_BaslikIcerikDivleriYoksa_LinkMetniniKullanir()
        {
            // Arrange - liste-baslik/liste-icerik div'leri olmadan, doğrudan link metni
            var html = @"
<html><body>
<div class=""liste"">
    <a href=""https://spk.gov.tr/dosya/2026-10.pdf"">Bülten No : 2026/10 Yayımlanma : 3 Mart 2026</a>
</div>
</body></html>";
            var handler = new FakeHttpMessageHandler().YanitEkle(Sayfa1Url, html);
            var sut = OlusturSut(handler);

            // Act
            var sonuc = await sut.YeniBultenleriTespitEtAsync();

            // Assert
            Assert.Single(sonuc);
            Assert.Equal("SPK Bülteni 2026/10", sonuc[0].BultenAdi);
        }

        [Fact]
        public async Task YeniBultenleriTespitEtAsync_BirdenFazlaBulten_HepsiTespitEdilir()
        {
            // Arrange
            var html = $@"
<html><body>
<div class=""liste"">
    <a href=""https://spk.gov.tr/dosya/2026-1.pdf"">
        <div class=""liste-baslik"">SPK Bülteni</div>
        <div class=""liste-icerik"">Bülten No : {GuncelYil}/1 Yayımlanma : 2 Ocak {GuncelYil}</div>
    </a>
    <a href=""https://spk.gov.tr/dosya/2026-2.pdf"">
        <div class=""liste-baslik"">SPK Bülteni</div>
        <div class=""liste-icerik"">Bülten No : {GuncelYil}/2 Yayımlanma : 9 Ocak {GuncelYil}</div>
    </a>
</div>
</body></html>";
            var handler = new FakeHttpMessageHandler().YanitEkle(Sayfa1Url, html);
            var sut = OlusturSut(handler);

            // Act
            var sonuc = await sut.YeniBultenleriTespitEtAsync();

            // Assert
            Assert.Equal(2, sonuc.Count);
            Assert.Contains(sonuc, b => b.BultenAdi == $"SPK Bülteni {GuncelYil}/1");
            Assert.Contains(sonuc, b => b.BultenAdi == $"SPK Bülteni {GuncelYil}/2");
        }

        [Fact]
        public async Task YeniBultenleriTespitEtAsync_HrefBosSa_Atlanir()
        {
            // Arrange - href boş bir link
            var html = @"
<html><body>
<div class=""liste"">
    <a href="""">
        <div class=""liste-baslik"">SPK Bülteni</div>
        <div class=""liste-icerik"">Bülten No : 2026/5 Yayımlanma : 1 Mayıs 2026</div>
    </a>
</div>
</body></html>";
            var handler = new FakeHttpMessageHandler().YanitEkle(Sayfa1Url, html);
            var sut = OlusturSut(handler);

            // Act
            var sonuc = await sut.YeniBultenleriTespitEtAsync();

            // Assert
            Assert.Empty(sonuc);
        }

        #endregion

        #region YeniBultenleriTespitEtAsync - Sayfalama (Pagination)

        [Fact]
        public async Task YeniBultenleriTespitEtAsync_SonrakiSayfaLinkiVarsa_IkinciSayfayiDaTarar()
        {
            // Arrange
            var sayfa1Html = TekBultenliHtml($"{GuncelYil}/1", "2", "Ocak", GuncelYil.ToString(),
                "https://spk.gov.tr/dosya/2026-1.pdf", sonrakiSayfaVar: true);
            var sayfa2Html = TekBultenliHtml($"{GuncelYil}/2", "9", "Ocak", GuncelYil.ToString(),
                "https://spk.gov.tr/dosya/2026-2.pdf", sonrakiSayfaVar: false);

            var handler = new FakeHttpMessageHandler()
                .YanitEkle(Sayfa1Url, sayfa1Html)
                .YanitEkle(Sayfa2Url, sayfa2Html);
            var sut = OlusturSut(handler);

            // Act
            var sonuc = await sut.YeniBultenleriTespitEtAsync();

            // Assert
            Assert.Equal(2, sonuc.Count);
            Assert.Contains(handler.IstenenUrller, u => u == Sayfa1Url);
            Assert.Contains(handler.IstenenUrller, u => u == Sayfa2Url);
        }

        [Fact]
        public async Task YeniBultenleriTespitEtAsync_SonrakiSayfaLinkiYoksa_TekSayfadaDurur()
        {
            // Arrange
            var sayfa1Html = TekBultenliHtml($"{GuncelYil}/1", "2", "Ocak", GuncelYil.ToString(),
                "https://spk.gov.tr/dosya/2026-1.pdf", sonrakiSayfaVar: false);

            var handler = new FakeHttpMessageHandler().YanitEkle(Sayfa1Url, sayfa1Html);
            var sut = OlusturSut(handler);

            // Act
            var sonuc = await sut.YeniBultenleriTespitEtAsync();

            // Assert
            Assert.Single(sonuc);
            Assert.DoesNotContain(handler.IstenenUrller, u => u == Sayfa2Url);
        }

        [Fact]
        public async Task YeniBultenleriTespitEtAsync_IkinciSayfaBosSa_TaramaDurur()
        {
            // Arrange - sayfa1 "sonraki sayfa var" diyor ama sayfa2'de bülten yok
            var sayfa1Html = TekBultenliHtml($"{GuncelYil}/1", "2", "Ocak", GuncelYil.ToString(),
                "https://spk.gov.tr/dosya/2026-1.pdf", sonrakiSayfaVar: true);
            var sayfa2Html = "<html><body><div class=\"liste\"></div></body></html>";

            var handler = new FakeHttpMessageHandler()
                .YanitEkle(Sayfa1Url, sayfa1Html)
                .YanitEkle(Sayfa2Url, sayfa2Html);
            var sut = OlusturSut(handler);

            // Act
            var sonuc = await sut.YeniBultenleriTespitEtAsync();

            // Assert
            Assert.Single(sonuc);
            Assert.DoesNotContain(handler.IstenenUrller, u => u == Sayfa3Url);
        }

        [Fact]
        public async Task YeniBultenleriTespitEtAsync_PaginationDisabledSinifli_SonrakiSayfayaGitmez()
        {
            // Arrange - '>' linki var ama "disabled" class'ına sahip
            var html = @"
<html><body>
<div class=""liste"">
    <a href=""https://spk.gov.tr/dosya/2026-1.pdf"">
        <div class=""liste-baslik"">SPK Bülteni</div>
        <div class=""liste-icerik"">Bülten No : 2026/1 Yayımlanma : 2 Ocak 2026</div>
    </a>
</div>
<div class=""spk-pagination"">
    <a href=""?s=2"" class=""disabled"">&gt;</a>
</div>
</body></html>";

            var handler = new FakeHttpMessageHandler().YanitEkle(Sayfa1Url, html);
            var sut = OlusturSut(handler);

            // Act
            await sut.YeniBultenleriTespitEtAsync();

            // Assert
            Assert.DoesNotContain(handler.IstenenUrller, u => u == Sayfa2Url);
        }

        #endregion

        #region YeniBultenleriTespitEtAsync - Hata Yönetimi

        [Fact]
        public async Task YeniBultenleriTespitEtAsync_HttpRequestException_GuvenliSekildeDurur()
        {
            // Arrange
            var handler = new FakeHttpMessageHandler()
                .HataEkle(Sayfa1Url, new HttpRequestException("Bağlantı hatası"));
            var sut = OlusturSut(handler);

            // Act
            var sonuc = await sut.YeniBultenleriTespitEtAsync();

            // Assert - exception fırlatılmamalı, boş liste dönmeli
            Assert.NotNull(sonuc);
            Assert.Empty(sonuc);
        }

        [Fact]
        public async Task YeniBultenleriTespitEtAsync_TaskCanceledException_GuvenliSekildeDurur()
        {
            // Arrange
            var handler = new FakeHttpMessageHandler()
                .HataEkle(Sayfa1Url, new TaskCanceledException("Zaman aşımı"));
            var sut = OlusturSut(handler);

            // Act
            var sonuc = await sut.YeniBultenleriTespitEtAsync();

            // Assert
            Assert.NotNull(sonuc);
            Assert.Empty(sonuc);
        }

        [Fact]
        public async Task YeniBultenleriTespitEtAsync_IkinciSayfaHataVerirse_IlkSayfaSonuclariKorunur()
        {
            // Arrange - sayfa1 başarılı ve sonraki sayfa olduğunu söylüyor, sayfa2 hata veriyor
            var sayfa1Html = TekBultenliHtml($"{GuncelYil}/1", "2", "Ocak", GuncelYil.ToString(),
                "https://spk.gov.tr/dosya/2026-1.pdf", sonrakiSayfaVar: true);

            var handler = new FakeHttpMessageHandler()
                .YanitEkle(Sayfa1Url, sayfa1Html)
                .HataEkle(Sayfa2Url, new HttpRequestException("Sunucu kısıtlaması"));
            var sut = OlusturSut(handler);

            // Act
            var sonuc = await sut.YeniBultenleriTespitEtAsync();

            // Assert - ilk sayfadaki sonuç korunmalı, exception fırlatılmamalı
            Assert.Single(sonuc);
        }

        #endregion

        #region YeniBultenleriTespitEtAsync - Tarih Ayrıştırma

        [Theory]
        [InlineData("Ocak", 1)]
        [InlineData("Şubat", 2)]
        [InlineData("Mart", 3)]
        [InlineData("Nisan", 4)]
        [InlineData("Mayıs", 5)]
        [InlineData("Haziran", 6)]
        [InlineData("Temmuz", 7)]
        [InlineData("Ağustos", 8)]
        [InlineData("Eylül", 9)]
        [InlineData("Ekim", 10)]
        [InlineData("Kasım", 11)]
        [InlineData("Aralık", 12)]
        public async Task YeniBultenleriTespitEtAsync_TumTurkceAylar_DogruAyristirilir(string ayAdi, int beklenenAy)
        {
            // Arrange
            var html = TekBultenliHtml($"{GuncelYil}/1", "10", ayAdi, GuncelYil.ToString(),
                "https://spk.gov.tr/dosya/x.pdf", sonrakiSayfaVar: false);

            var handler = new FakeHttpMessageHandler().YanitEkle(Sayfa1Url, html);
            var sut = OlusturSut(handler);

            // Act
            var sonuc = await sut.YeniBultenleriTespitEtAsync();

            // Assert
            Assert.Single(sonuc);
            Assert.Equal(beklenenAy, sonuc[0].YayinTarihi.Month);
            Assert.Equal(10, sonuc[0].YayinTarihi.Day);
        }

        [Fact]
        public async Task YeniBultenleriTespitEtAsync_GecersizTarih_BugununTarihiKullanilir()
        {
            // Arrange - geçersiz gün/ay kombinasyonu (31 Şubat yok)
            var html = TekBultenliHtml($"{GuncelYil}/1", "31", "Şubat", GuncelYil.ToString(),
                "https://spk.gov.tr/dosya/x.pdf", sonrakiSayfaVar: false);

            var handler = new FakeHttpMessageHandler().YanitEkle(Sayfa1Url, html);
            var sut = OlusturSut(handler);

            // Act
            var sonuc = await sut.YeniBultenleriTespitEtAsync();

            // Assert
            Assert.Single(sonuc);
            Assert.Equal(DateTime.UtcNow.Date, sonuc[0].YayinTarihi.Date);
        }

        #endregion

        #region PdfIndirAsync

        [Fact]
        public async Task PdfIndirAsync_GecerliUrl_PdfByteDizisiniDoner()
        {
            // Arrange
            var pdfUrl = "https://spk.gov.tr/dosya/ornek.pdf";
            var sahtePdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46 }; // "%PDF" imzası

            var handler = new FakeHttpMessageHandler().ByteYanitEkle(pdfUrl, sahtePdfBytes);
            var sut = OlusturSut(handler);

            // Act
            var sonuc = await sut.PdfIndirAsync(pdfUrl);

            // Assert
            Assert.Equal(sahtePdfBytes, sonuc);
        }

        [Fact]
        public async Task PdfIndirAsync_DogruUrlyeIstekAtar()
        {
            // Arrange
            var pdfUrl = "https://spk.gov.tr/dosya/spesifik-bulten.pdf";
            var handler = new FakeHttpMessageHandler().ByteYanitEkle(pdfUrl, new byte[] { 1, 2, 3 });
            var sut = OlusturSut(handler);

            // Act
            await sut.PdfIndirAsync(pdfUrl);

            // Assert
            Assert.Contains(pdfUrl, handler.IstenenUrller);
        }

        #endregion

        #region TespitEdilenBulten DTO

        [Fact]
        public void TespitEdilenBulten_VarsayilanDegerler_BosStringVeMinTarih()
        {
            // Act
            var bulten = new TespitEdilenBulten();

            // Assert
            Assert.Equal(string.Empty, bulten.BultenAdi);
            Assert.Equal(string.Empty, bulten.PdfUrl);
            Assert.Equal(default, bulten.YayinTarihi);
        }

        #endregion
    }
}
