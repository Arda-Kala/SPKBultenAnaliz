using System.Linq;
using SPKBultenAnaliz.Business.Services;
using Xunit;

namespace SPKBultenAnaliz.Tests
{
    /// <summary>
    /// Use Case 3.3 - Unit Testing. Bu test sınıfı, chunking algoritmasının
    /// (BloklaraBol) doğru çalıştığını PDF dosyasına ihtiyaç duymadan doğrular
    /// çünkü metot saf (pure) bir fonksiyon olarak tasarlanmıştır.
    /// </summary>
    public class PdfParserServiceTests
    {
        private readonly PdfParserService _sut; // "sut" = System Under Test

        public PdfParserServiceTests()
        {
            _sut = new PdfParserService();
        }

        [Fact]
        public void BosMetinVerilirse_BosListeDoner()
        {
            var sonuc = _sut.BloklaraBol("", maksimumBlokBoyutu: 100);
            Assert.Empty(sonuc);
        }

        [Fact]
        public void KisaMetin_TekBlokOlarakDoner()
        {
            var metin = "Bu kısa bir SPK bülten metnidir.";
            var sonuc = _sut.BloklaraBol(metin, maksimumBlokBoyutu: 1000);

            Assert.Single(sonuc);
            Assert.Equal(metin, sonuc[0]);
        }

        [Fact]
        public void UzunMetin_SinirAsilmayacakSekildeBirdenFazlaBlogaBolunur()
        {
            var paragraf1 = new string('A', 50);
            var paragraf2 = new string('B', 50);
            var paragraf3 = new string('C', 50);
            var metin = string.Join("\n\n", paragraf1, paragraf2, paragraf3);

            var sonuc = _sut.BloklaraBol(metin, maksimumBlokBoyutu: 80);

            Assert.True(sonuc.Count > 1, "Metin, sınırı aştığı için birden fazla bloğa bölünmeliydi.");
            Assert.All(sonuc, blok => Assert.True(blok.Length <= 80));
        }

        [Fact]
        public void HerBlok_MaksimumBoyutuAsmaz_UzunTekParagrafDurumunda()
        {
            // Tek bir paragraf, sınırdan çok daha büyük -> cümlelere bölünmeli.
            var cumleler = Enumerable.Range(1, 20).Select(i => $"Bu {i}. cümledir.");
            var uzunParagraf = string.Join(" ", cumleler);

            var sonuc = _sut.BloklaraBol(uzunParagraf, maksimumBlokBoyutu: 60);

            Assert.True(sonuc.Count > 1);
            Assert.All(sonuc, blok => Assert.True(blok.Length <= 60,
                $"Blok {blok.Length} karakter, 60 sınırını aşıyor: '{blok}'"));
        }

        [Fact]
        public void GecersizMaksimumBoyut_ExceptionFirlatir()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                _sut.BloklaraBol("herhangi bir metin", maksimumBlokBoyutu: 0));
        }
    }
}
