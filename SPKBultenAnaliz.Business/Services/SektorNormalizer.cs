using System.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SPKBultenAnaliz.Business.Services
{
    /// <summary>
    /// Gemini'nin ürettiği sektör metnini, sistemin kullandığı standart
    /// (kanonik) sektör isimlerinden birine normalize eder.
    ///
    /// SORUN: Gemini bazen aynı sektörü farklı yazımlarla döndürebiliyor
    /// (örn. "Diğer" vs "Diger", "diğer" vs "DİĞER", baştaki/sondaki
    /// boşluklar). Bunlar veritabanında BİRBİRİNDEN FARKLI string olarak
    /// saklandığı için, filtreleme ekranında "Diğer" ve "Diger" iki ayrı
    /// seçenek gibi görünüyordu — aslında aynı sektörü temsil etseler de.
    ///
    /// ÇÖZÜM: Kaydetmeden ÖNCE, gelen metni Türkçe karakterlerden arındırıp
    /// (ğ→g, ı→i, ş→s, ç→c, ö→o, ü→u) küçük harfe çevirerek karşılaştırıyoruz
    /// ve standart listedeki karşılığını buluyoruz. Eşleşme yoksa "Diğer"e
    /// düşüyoruz. Bu, "yazım farkı yüzünden filtre seçenekleri çoğalması"
    /// probleminin kök nedenini (kaydetme anında) çözer.
    /// </summary>
    public static class SektorNormalizer
    {
        /// <summary>Sistemde kullanılan resmi (kanonik) sektör listesi — Gemini prompt'undaki liste ile birebir aynı olmalıdır.</summary>
        public static readonly string[] KanonikSektorler =
        {
            "Bankacılık", "Sigorta", "Enerji", "Gayrimenkul (GYO)", "Perakende",
            "Teknoloji", "Sanayi/Üretim", "Ulaştırma/Lojistik", "Tarım/Gıda",
            "Holding/Yatırım", "Aracı Kurum/Portföy Yönetimi", "Kamu/Diğer", "Diğer"
        };

        private static readonly Dictionary<string, string> AramaTablosu =
            KanonikSektorler.ToDictionary(AsciiyeIndirVeKucultVeTrim, s => s);

        /// <summary>Verilen ham sektör metnini kanonik forma çevirir. Eşleşme bulunamazsa "Diğer" döner.</summary>
        public static string Normalize(string? hamSektor)
        {
            if (string.IsNullOrWhiteSpace(hamSektor))
                return "Diğer";

            var anahtar = AsciiyeIndirVeKucultVeTrim(hamSektor);
            return AramaTablosu.TryGetValue(anahtar, out var kanonik) ? kanonik : "Diğer";
        }

        /// <summary>
        /// Türkçe'ye özgü karakterleri ASCII karşılıklarına çevirir ve küçük
        /// harfe indirir — böylece "Diğer", "Diger", "DİĞER", " diğer " gibi
        /// varyasyonların hepsi aynı anahtara ("diger") eşlenir.
        /// </summary>
        private static string AsciiyeIndirVeKucultVeTrim(string s)
        {
            var sb = new StringBuilder(s.Trim());
            sb.Replace('ğ', 'g').Replace('Ğ', 'g')
              .Replace('ı', 'i').Replace('I', 'i').Replace('İ', 'i')
              .Replace('ş', 's').Replace('Ş', 's')
              .Replace('ç', 'c').Replace('Ç', 'c')
              .Replace('ö', 'o').Replace('Ö', 'o')
              .Replace('ü', 'u').Replace('Ü', 'u');

            return Regex.Replace(sb.ToString().ToLowerInvariant(), @"\s+", " ").Trim();
        }
    }
}

