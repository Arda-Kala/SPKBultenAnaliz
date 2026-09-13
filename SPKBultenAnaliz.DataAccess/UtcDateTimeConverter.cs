using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace SPKBultenAnaliz.DataAccess
{
    /// <summary>
    /// SQL Server'dan okunan DateTime değerlerinin Kind bilgisini kaybetmesini
    /// telafi eden dönüştürücü. Veritabanına yazarken hiçbir değişiklik yapmaz
    /// (biz zaten her yerde DateTime.UtcNow kullanıyoruz); veritabanından
    /// okurken ise değeri açıkça DateTimeKind.Utc olarak işaretler.
    /// Bkz. BultenDbContext.ConfigureConventions içindeki ayrıntılı açıklama.
    /// </summary>
    public class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
    {
        public UtcDateTimeConverter() : base(
            yaziliyorken => yaziliyorken,
            okunuyorken => DateTime.SpecifyKind(okunuyorken, DateTimeKind.Utc))
        {
        }
    }

    /// <summary>Yukarıdaki UtcDateTimeConverter'ın nullable (DateTime?) sürümü.</summary>
    public class UtcNullableDateTimeConverter : ValueConverter<DateTime?, DateTime?>
    {
        public UtcNullableDateTimeConverter() : base(
            yaziliyorken => yaziliyorken,
            okunuyorken => okunuyorken.HasValue ? DateTime.SpecifyKind(okunuyorken.Value, DateTimeKind.Utc) : okunuyorken)
        {
        }
    }
}
