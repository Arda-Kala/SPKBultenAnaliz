using System;
using System.Collections.Generic;

namespace SPKBultenAnaliz.Core.DTOs
{
    /// <summary>
    /// API'nin dışarı verdiği bülten özeti (Entity DEĞİL, DTO).
    /// Neden ayrı bir sınıf? Çünkü client'e iç veritabanı yapımızı
    /// (navigation property'ler, EF Core özel alanları vb.) sızdırmak istemeyiz.
    /// </summary>
    public class BultenOzetDto
    {
        public int Id { get; set; }
        public string BultenAdi { get; set; } = string.Empty;
        public DateTime YayinTarihi { get; set; }
        public string PdfUrl { get; set; } = string.Empty;
        public string? GenelYoneticiOzeti { get; set; }
        public string Durum { get; set; } = string.Empty;
        public List<AnalizSonucuDto> AnalizSonuclari { get; set; } = new();
    }

    public class AnalizSonucuDto
    {
        public int Id { get; set; }
        public string EtkilenenUnsur { get; set; } = string.Empty;
        public string? Sektor { get; set; }
        public string DuyguDurumu { get; set; } = string.Empty;
        public int EtkiSkoru { get; set; }

        /// <summary>Etkinin öngörülen zaman ufku: "Kısa Vadeli", "Orta Vadeli" veya "Uzun Vadeli".</summary>
        public string? Vade { get; set; }

        /// <summary>İlgiliyse BIST hisse kodu (örn. "GARAN", "THYAO"). Uygun değilse boş.</summary>
        public string? HisseKodu { get; set; }

        public string AiGerekceYorumu { get; set; } = string.Empty;
    }

    /// <summary>Yeni bir bülteni manuel olarak sisteme eklemek için kullanılan istek modeli.</summary>
    public class BultenEkleRequest
    {
        public string BultenAdi { get; set; } = string.Empty;
        public DateTime YayinTarihi { get; set; }
        public string PdfUrl { get; set; } = string.Empty;
    }

    /// <summary>
    /// Admin panelinden bir haberin (analiz sonucunun) alanlarını manuel
    /// düzenlemek için kullanılan istek modeli. Tüm alanlar opsiyoneldir —
    /// null bırakılan alan değiştirilmez (kısmi güncelleme/PATCH mantığı).
    /// </summary>
    public class AnalizGuncelleRequest
    {
        public string? EtkilenenUnsur { get; set; }
        public string? Sektor { get; set; }
        public string? DuyguDurumu { get; set; }
        public int? EtkiSkoru { get; set; }
        public string? Vade { get; set; }
        public string? HisseKodu { get; set; }
        public string? AiGerekceYorumu { get; set; }
    }
}
