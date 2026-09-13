using System.Linq;
using SPKBultenAnaliz.Business.Services;
using Xunit;

namespace SPKBultenAnaliz.Tests
{
    /// <summary>
    /// PdfParserService.BloklaraBol için ek uç durum (edge case) testleri.
    /// Mevcut PdfParserServiceTests.cs dosyasını tamamlayan, daha derin
    /// senaryoları kapsayan ek test sınıfı.
    /// </summary>
    public class PdfParserServiceEdgeCaseTests
    {
        private readonly PdfParserService _sut = new();

        [Fact]
        public void BloklaraBol_NullMetin_BosListeDoner()
        {
            var sonuc = _sut.BloklaraBol(null!, maksimumBlokBoyutu: 100);
            Assert.Empty(sonuc);
        }

        [Fact]
        public void BloklaraBol_SadeceBosluklardanOlusanMetin_BosListeDoner()
        {
            var sonuc = _sut.BloklaraBol("   \n\n   \t  ", maksimumBlokBoyutu: 100);
            Assert.Empty(sonuc);
        }

        [Fact]
        public void BloklaraBol_NegatifMaksimumBoyut_ExceptionFirlatir()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                _sut.BloklaraBol("herhangi bir metin", maksimumBlokBoyutu: -5));
        }

        [Fact]
        public void BloklaraBol_TekCumleSinirdanCokBuyuk_SertKesilir()
        {
            // Nokta/boşluk olmayan, tek "cümle" sınırdan çok daha büyük.
            var tekKelime = new string('X', 500);

            var sonuc = _sut.BloklaraBol(tekKelime, maksimumBlokBoyutu: 100);

            Assert.True(sonuc.Count >= 5);
            Assert.All(sonuc, blok => Assert.True(blok.Length <= 100));
            // Birleştirildiğinde orijinal karakter sayısı korunmalı.
            Assert.Equal(tekKelime.Length, sonuc.Sum(b => b.Length));
        }

        [Fact]
        public void BloklaraBol_BirdenFazlaParagraf_TekBlogaSigiyorsaBirlestirilir()
        {
            var metin = "Paragraf bir.\n\nParagraf iki.\n\nParagraf üç.";

            var sonuc = _sut.BloklaraBol(metin, maksimumBlokBoyutu: 1000);

            Assert.Single(sonuc);
            Assert.Contains("Paragraf bir.", sonuc[0]);
            Assert.Contains("Paragraf iki.", sonuc[0]);
            Assert.Contains("Paragraf üç.", sonuc[0]);
        }

        [Fact]
        public void BloklaraBol_ParagraflarAyriBloklaraDusuyorsaSirasiKorunur()
        {
            var p1 = new string('A', 40);
            var p2 = new string('B', 40);
            var metin = $"{p1}\n\n{p2}";

            // Her iki paragraf birlikte sınırı aşacak, ama tek başına sığacak şekilde.
            var sonuc = _sut.BloklaraBol(metin, maksimumBlokBoyutu: 50);

            Assert.Equal(2, sonuc.Count);
            Assert.Equal(p1, sonuc[0]);
            Assert.Equal(p2, sonuc[1]);
        }

        [Fact]
        public void BloklaraBol_VarsayilanMaksimumBoyut_4000Kullanilir()
        {
            var kisaMetin = "Kısa bir SPK bülten metni.";

            var sonuc = _sut.BloklaraBol(kisaMetin);

            Assert.Single(sonuc);
            Assert.Equal(kisaMetin, sonuc[0]);
        }

        [Fact]
        public void BloklaraBol_UcuncuSinirDegeri_TamSinirdaOlanParagrafSigiyor()
        {
            var tamSinirdaMetin = new string('A', 100);

            var sonuc = _sut.BloklaraBol(tamSinirdaMetin, maksimumBlokBoyutu: 100);

            Assert.Single(sonuc);
            Assert.Equal(100, sonuc[0].Length);
        }

        [Fact]
        public void BloklaraBol_TurkceKarakterliMetin_DogruBolunur()
        {
            var metin = "Sermaye Piyasası Kurulu (SPK), şirketin İşlem Yasağı kararını açıkladı. " +
                        "Bu karar, Çarşamba günü yürürlüğe girecek ve öğrenciler dahi konuyu takip ediyor.";

            var sonuc = _sut.BloklaraBol(metin, maksimumBlokBoyutu: 80);

            Assert.True(sonuc.Count > 1);
            Assert.All(sonuc, blok => Assert.True(blok.Length <= 80));
        }

        [Fact]
        public void BloklaraBol_BosParagraflarAtlanmalidir()
        {
            var metin = "Paragraf bir.\n\n\n\nParagraf iki.";

            var sonuc = _sut.BloklaraBol(metin, maksimumBlokBoyutu: 1000);

            Assert.Single(sonuc);
            Assert.DoesNotContain("\n\n\n\n", sonuc[0]);
        }
    }
}
