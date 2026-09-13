using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SPKBultenAnaliz.Business.Services;
using SPKBultenAnaliz.Core.DTOs;
using SPKBultenAnaliz.Core.Entities;
using SPKBultenAnaliz.Core.Interfaces;
using Xunit;

namespace SPKBultenAnaliz.Tests
{
    /// <summary>
    /// BultenService, dört farklı bağımlılığı (Repository, Scraper, PdfParser,
    /// AiAnalysis) doğru sırada çağıran bir "orkestratör" olduğu için, Moq ile
    /// bu bağımlılıkları taklit ederek (mock) davranışını izole test ederiz.
    /// Bu yaklaşım, gerçek bir veritabanına veya Gemini API'ye ihtiyaç duymaz.
    /// </summary>
    public class BultenServiceTests
    {
        private readonly Mock<IBultenRepository> _repositoryMock = new();
        private readonly Mock<ISpkScraperService> _scraperMock = new();
        private readonly Mock<IPdfParserService> _pdfParserMock = new();
        private readonly Mock<IAiAnalysisService> _aiAnalysisMock = new();
        private readonly BultenService _sut;

        public BultenServiceTests()
        {
            _sut = new BultenService(
                _repositoryMock.Object,
                _scraperMock.Object,
                _pdfParserMock.Object,
                _aiAnalysisMock.Object,
                NullLogger<BultenService>.Instance);
        }

        [Fact]
        public async Task YeniBultenleriIsleAsync_MukerrerPdfUrlVarsa_Atlanmalidir()
        {
            _scraperMock.Setup(s => s.YeniBultenleriTespitEtAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<TespitEdilenBulten>
                {
                    new() { BultenAdi = "Test Bülteni", PdfUrl = "https://ornek.com/bulten.pdf", YayinTarihi = System.DateTime.UtcNow }
                });

            _repositoryMock.Setup(r => r.PdfUrlVarMiAsync("https://ornek.com/bulten.pdf"))
                .ReturnsAsync(true); // zaten işlenmiş

            var islenenSayisi = await _sut.YeniBultenleriIsleAsync();

            Assert.Equal(0, islenenSayisi);
            _repositoryMock.Verify(r => r.EkleAsync(It.IsAny<Bulten>()), Times.Never);
        }

        [Fact]
        public async Task YeniBultenleriIsleAsync_YeniBulten_IndirilirParcalanirVeKaydedilir()
        {
            var tespit = new TespitEdilenBulten
            {
                BultenAdi = "SPK Bülteni 2026/42",
                PdfUrl = "https://ornek.com/2026-42.pdf",
                YayinTarihi = System.DateTime.UtcNow
            };

            _scraperMock.Setup(s => s.YeniBultenleriTespitEtAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<TespitEdilenBulten> { tespit });

            _repositoryMock.Setup(r => r.PdfUrlVarMiAsync(tespit.PdfUrl)).ReturnsAsync(false);

            var sahtePdfBytes = new byte[] { 1, 2, 3 };
            _scraperMock.Setup(s => s.PdfIndirAsync(tespit.PdfUrl, It.IsAny<CancellationToken>()))
                .ReturnsAsync(sahtePdfBytes);

            _pdfParserMock.Setup(p => p.MetniAyikla(sahtePdfBytes)).Returns("örnek ham metin");
            _pdfParserMock.Setup(p => p.BloklaraBol("örnek ham metin", It.IsAny<int>()))
                .Returns(new List<string> { "blok 1", "blok 2" });

            Bulten? kaydedilenBulten = null;
            _repositoryMock.Setup(r => r.EkleAsync(It.IsAny<Bulten>()))
                .Callback<Bulten>(b => { b.Id = 42; kaydedilenBulten = b; })
                .ReturnsAsync(() => kaydedilenBulten!);

            _repositoryMock.Setup(r => r.IdyeGoreGetirAsync(42))
                .ReturnsAsync(() => kaydedilenBulten);

            _aiAnalysisMock.Setup(a => a.AnalizEtAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GeminiAnalizSonucu
                {
                    BultenGenelOzet = "Test özeti",
                    AnalizSonuclari = new List<GeminiEtkiUnsuru>
                    {
                        new() { EtkilenenUnsur = "ABC Holding", DuyguDurumu = "Pozitif", EtkiSkoru = 70, AiGerekceYorumu = "Test gerekçe" }
                    }
                });

            var islenenSayisi = await _sut.YeniBultenleriIsleAsync();

            Assert.Equal(1, islenenSayisi);
            Assert.NotNull(kaydedilenBulten);
            Assert.Equal(2, kaydedilenBulten!.MetinBloklari.Count);
            _repositoryMock.Verify(r => r.EkleAsync(It.IsAny<Bulten>()), Times.Once);
            _aiAnalysisMock.Verify(a => a.AnalizEtAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task BulteniAnalizEtAsync_GeminiHataDonerse_DurumDogrulamaBekleniyorOlarakGuncellenir()
        {
            var bulten = new Bulten { Id = 7, BultenAdi = "Test" };
            bulten.MetinBloklari.Add(new BultenMetinBlogu { BlokSiraNo = 0, HamMetinIcerigi = "metin" });

            _repositoryMock.Setup(r => r.IdyeGoreGetirAsync(7)).ReturnsAsync(bulten);
            _aiAnalysisMock.Setup(a => a.AnalizEtAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new GeminiServisException("Gemini yanıtı beklenen formatta değil (bozuk JSON)."));

            await Assert.ThrowsAsync<GeminiServisException>(() => _sut.BulteniAnalizEtAsync(7));

            Assert.Equal("DogrulamaBekleniyor", bulten.Durum);
            _repositoryMock.Verify(r => r.GuncelleAsync(It.Is<Bulten>(b => b.Durum == "DogrulamaBekleniyor")), Times.Once);
        }
    }
}
