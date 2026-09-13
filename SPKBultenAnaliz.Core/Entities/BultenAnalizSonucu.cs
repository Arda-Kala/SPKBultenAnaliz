using System;

namespace SPKBultenAnaliz.Core.Entities
{
    /// <summary>
    /// Gemini API'nin bir bülten hakkında ürettiği piyasa etki analiz sonucu.
    /// Bir bültenin birden fazla analiz sonucu (etkilenen unsur) olabilir.
    /// </summary>
    public class BultenAnalizSonucu
    {
        public int Id { get; set; }

        public int BultenId { get; set; }

        /// <summary>Etkilenen şirket, sektör veya finansal araç adı.</summary>
        public string EtkilenenUnsur { get; set; } = string.Empty;

        /// <summary>
        /// Etkilenen unsurun ait olduğu genel sektör (örn. "Bankacılık", "Enerji",
        /// "Teknoloji", "Perakende"). Grafik/filtreleme özelliği için eklenmiştir.
        /// Eski kayıtlarda null olabilir (migration öncesi veriler).
        /// </summary>
        public string? Sektor { get; set; }

        /// <summary>Piyasa yönü: "Pozitif", "Olumsuz" veya "Notr".</summary>
        public string DuyguDurumu { get; set; } = string.Empty;

        /// <summary>Kararın piyasayı etkileme derecesi (1-100 arası).</summary>
        public int EtkiSkoru { get; set; }

        /// <summary>
        /// Etkinin öngörülen zaman ufku: "Kısa Vadeli" (günler/haftalar),
        /// "Orta Vadeli" (aylar) veya "Uzun Vadeli" (çeyrekler/yıllar).
        /// </summary>
        public string? Vade { get; set; }

        /// <summary>
        /// Etkilenen unsur borsada işlem gören bir şirketse BIST hisse kodu
        /// (örn. "GARAN", "THYAO"). Kamu kurumu/genel piyasa gibi borsada
        /// karşılığı olmayan unsurlarda null/boş bırakılır.
        /// </summary>
        public string? HisseKodu { get; set; }

        /// <summary>Yapay zekanın finansal gerekçe ve yorum raporu.</summary>
        public string AiGerekceYorumu { get; set; } = string.Empty;

        public DateTime OlusturmaTarihi { get; set; } = DateTime.UtcNow;

        public virtual Bulten? Bulten { get; set; }
    }
}
