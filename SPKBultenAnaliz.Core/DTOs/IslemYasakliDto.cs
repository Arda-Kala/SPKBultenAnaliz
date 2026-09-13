using System.Collections.Generic;

namespace SPKBultenAnaliz.Core.DTOs
{
    /// <summary>
    /// SPK'nın resmi Web Servisi'nden (ws.spk.gov.tr/IdariYaptirimlar/api/IslemYasaklari)
    /// gelen bir işlem yasağı kaydının normalize edilmiş (bizim kendi alan
    /// adlarımıza çevrilmiş) hâli.
    ///
    /// ÖNEMLİ ÖĞRENME NOTU: Bu servisin GERÇEK JSON alan adları elle
    /// doğrulanamadı (bkz. IslemYasaklariService.cs içindeki açıklama).
    /// Bu yüzden "HamVeri" alanı, eşleştirilemeyen ham JSON'ı da saklar —
    /// böylece arayüz, bizim tahmin ettiğimiz alan adları tutmasa bile
    /// kullanıcıya en azından ham veriyi gösterebilir.
    /// </summary>
    public class IslemYasakliDto
    {
        public string Tip { get; set; } = string.Empty; // "Kisi" veya "Sirket"
        public string AdSoyadUnvan { get; set; } = string.Empty;
        public string? GercekTuzelKisi { get; set; }
        public string? MkkSicilNo { get; set; }
        public string? MersisNo { get; set; }
        public string? KararTarihi { get; set; }
        public string? KararNo { get; set; }
        public string? Pay { get; set; }
        public string? PayKodu { get; set; } // SPK'dan gelen payKodu (örn. DESPC, GSDDE)

        public string? YasakBaslangicTarihi { get; set; }
        public string? YasakBitisTarihi { get; set; }
        public string? Aciklama { get; set; }

        public Dictionary<string, string> HamVeri { get; set; } = new();
        public bool AlanlarEslesti { get; set; }
    }
}



