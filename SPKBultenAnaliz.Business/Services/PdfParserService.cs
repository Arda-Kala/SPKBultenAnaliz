using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using SPKBultenAnaliz.Core.Interfaces;

namespace SPKBultenAnaliz.Business.Services
{
    /// <summary>
    /// IPdfParserService'in somut uygulaması. UglyToad.PdfPig ile PDF'ten metin
    /// ayıklar ve metni Gemini'nin bağlam penceresine uygun bloklara böler.
    ///
    /// Use Case 1.3: Akıllı Metin Bölümleme (Chunking) Servisi.
    /// </summary>
    public class PdfParserService : IPdfParserService
    {
        public string MetniAyikla(byte[] pdfIcerik)
        {
            using var stream = new MemoryStream(pdfIcerik);
            using var document = PdfDocument.Open(stream);

            var sb = new StringBuilder();
            foreach (var page in document.GetPages())
            {
                sb.AppendLine(page.Text);
                sb.AppendLine(); // sayfalar arası boşluk
            }

            return TemizleMetin(sb.ToString());
        }

        /// <summary>
        /// PDF'ten çıkan metindeki fazladan boşlukları ve satır kırılmalarını sadeleştirir.
        /// </summary>
        private static string TemizleMetin(string ham)
        {
            var tekSatir = Regex.Replace(ham, @"[ \t]+", " ");
            var fazlaBosSatir = Regex.Replace(tekSatir, @"(\r?\n){3,}", "\n\n");
            return fazlaBosSatir.Trim();
        }

        /// <summary>
        /// Uzun metni, verilen maksimum karakter sınırını aşmayan bloklara böler.
        /// Önce paragraf sınırlarına (\n\n), bulamazsa cümle sınırlarına (". ") saygı gösterir.
        /// Bu metot saf (pure) bir fonksiyondur — dış bağımlılığı olmadığı için
        /// birim testte doğrudan çağrılıp doğrulanabilir (bkz. Tests projesi).
        /// </summary>
        public List<string> BloklaraBol(string tamMetin, int maksimumBlokBoyutu = 4000)
        {
            if (string.IsNullOrWhiteSpace(tamMetin))
                return new List<string>();

            if (maksimumBlokBoyutu <= 0)
                throw new ArgumentOutOfRangeException(nameof(maksimumBlokBoyutu));

            var paragraflar = tamMetin
                .Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => p.Length > 0)
                .ToList();

            var bloklar = new List<string>();
            var mevcutBlok = new StringBuilder();

            foreach (var paragraf in paragraflar)
            {
                // Tek bir paragraf bile sınırdan büyükse, cümlelere böl.
                if (paragraf.Length > maksimumBlokBoyutu)
                {
                    if (mevcutBlok.Length > 0)
                    {
                        bloklar.Add(mevcutBlok.ToString().Trim());
                        mevcutBlok.Clear();
                    }

                    foreach (var parca in BuyukParagrafiBol(paragraf, maksimumBlokBoyutu))
                        bloklar.Add(parca);

                    continue;
                }

                // Mevcut bloğa eklenirse sınırı aşacak mı? (Sadece mevcutBlok'ta
                // gerçekten bir içerik varsa flush et — boşken flush edersek
                // sonuca boş bir "" blok eklenir, bu da Gemini'ye boş içerik
                // gönderilmesine yol açar.)
                if (mevcutBlok.Length > 0 && mevcutBlok.Length + paragraf.Length + 2 > maksimumBlokBoyutu)
                {
                    bloklar.Add(mevcutBlok.ToString().Trim());
                    mevcutBlok.Clear();
                }

                if (mevcutBlok.Length > 0)
                    mevcutBlok.Append("\n\n");

                mevcutBlok.Append(paragraf);
            }

            if (mevcutBlok.Length > 0)
                bloklar.Add(mevcutBlok.ToString().Trim());

            return bloklar;
        }

        /// <summary>Tek bir paragraf sınırdan büyükse, cümle sınırlarına göre böler.</summary>
        private static IEnumerable<string> BuyukParagrafiBol(string paragraf, int maksimumBlokBoyutu)
        {
            var cumleler = Regex.Split(paragraf, @"(?<=[.!?])\s+");
            var mevcut = new StringBuilder();

            foreach (var cumle in cumleler)
            {
                if (mevcut.Length + cumle.Length + 1 > maksimumBlokBoyutu)
                {
                    if (mevcut.Length > 0)
                    {
                        yield return mevcut.ToString().Trim();
                        mevcut.Clear();
                    }

                    // Tek bir cümle bile sınırdan büyükse, sert şekilde kes (nadir durum).
                    if (cumle.Length > maksimumBlokBoyutu)
                    {
                        for (int i = 0; i < cumle.Length; i += maksimumBlokBoyutu)
                            yield return cumle.Substring(i, Math.Min(maksimumBlokBoyutu, cumle.Length - i));
                        continue;
                    }
                }

                if (mevcut.Length > 0)
                    mevcut.Append(' ');

                mevcut.Append(cumle);
            }

            if (mevcut.Length > 0)
                yield return mevcut.ToString().Trim();
        }
    }
}
