using System;
using System.Collections.Generic;

namespace SPKBultenAnaliz.Core.Entities
{
    /// <summary>
    /// SPK bültenini temsil eden ana entity (kök agregat).
    /// Her bülten birden fazla metin bloğuna ve analiz sonucuna sahip olabilir.
    /// </summary>
    public class Bulten
    {
        public int Id { get; set; }

        /// <summary>Bültenin resmi adı (örn. "SPK Bülteni 2026/42").</summary>
        public string BultenAdi { get; set; } = string.Empty;

        /// <summary>Bültenin resmi yayın tarihi.</summary>
        public DateTime YayinTarihi { get; set; }

        /// <summary>Orijinal PDF dokümanının internet adresi.</summary>
        public string PdfUrl { get; set; } = string.Empty;

        /// <summary>Gemini API tarafından çıkarılan genel yönetici özeti.</summary>
        public string? GenelYoneticiOzeti { get; set; }

        /// <summary>Sisteme işlenme zaman damgası.</summary>
        public DateTime KayitTarihi { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// İşlenme durumu: Beklemede, IslendiOK, DogrulamaBekleniyor, Hata.
        /// Bkz. Risk Yönetimi bölümü — bozuk JSON çıktısı senaryosu.
        /// </summary>
        public string Durum { get; set; } = "Beklemede";

        public virtual ICollection<BultenMetinBlogu> MetinBloklari { get; set; } = new List<BultenMetinBlogu>();
        public virtual ICollection<BultenAnalizSonucu> AnalizSonuclari { get; set; } = new List<BultenAnalizSonucu>();
    }
}
