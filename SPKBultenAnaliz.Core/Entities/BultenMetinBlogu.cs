using System;

namespace SPKBultenAnaliz.Core.Entities
{
    /// <summary>
    /// SPK bülteninin PDF'ten ayıklanmış, Gemini'nin bağlam penceresine uygun
    /// büyüklükte parçalara (chunk) bölünmüş metin bloğu.
    /// </summary>
    public class BultenMetinBlogu
    {
        public int Id { get; set; }

        public int BultenId { get; set; }

        /// <summary>Bu bloğun dokümandaki sırası (0'dan başlar).</summary>
        public int BlokSiraNo { get; set; }

        /// <summary>Ham metin içeriği — Gemini'ye gönderilecek olan bölüm.</summary>
        public string HamMetinIcerigi { get; set; } = string.Empty;

        public DateTime KayitTarihi { get; set; } = DateTime.UtcNow;

        public virtual Bulten? Bulten { get; set; }
    }
}
