using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SPKBultenAnaliz.Core.Entities;

namespace SPKBultenAnaliz.DataAccess
{
    /// <summary>
    /// Entity Framework Core DbContext — veritabanı yapısını, ilişkileri ve
    /// kısıtları (constraint) Code-First yaklaşımıyla tanımlar.
    /// </summary>
    public class BultenDbContext : DbContext
    {
        public BultenDbContext(DbContextOptions<BultenDbContext> options) : base(options)
        {
        }

        public DbSet<Bulten> Bultenler => Set<Bulten>();
        public DbSet<BultenMetinBlogu> BultenMetinBloklari => Set<BultenMetinBlogu>();
        public DbSet<BultenAnalizSonucu> BultenAnalizSonuclari => Set<BultenAnalizSonucu>();
        public DbSet<Kullanici> Kullanicilar => Set<Kullanici>();

        /// <summary>
        /// ÖĞRENME NOTU: SQL Server'ın "datetime2" kolon tipi zaman dilimi
        /// (timezone) bilgisi TUTMAZ. Biz veritabanına her zaman DateTime.UtcNow
        /// yazsak da, EF Core bunu geri OKURKEN .NET'e "DateTimeKind.Unspecified"
        /// olarak döner — yani "bu UTC mi yerel saat mi?" bilgisi kaybolur.
        ///
        /// Bu, ciddi bir hataya yol açar: DTO'lar JSON'a çevrilirken Kind=Unspecified
        /// olan bir DateTime, ISO 8601 formatında "Z" (UTC işareti) OLMADAN gider
        /// (örn. "2026-08-03T08:15:00"). Tarayıcıdaki JavaScript bu string'i
        /// `new Date(...)` ile parse ederken, "Z" yoksa bunu YEREL SAAT sanır —
        /// böylece örneğin Türkiye'de (UTC+3) gerçek saatten 3 saat farklı,
        /// yanlış bir zaman gösterilir.
        ///
        /// Çözüm: ConfigureConventions ile, veritabanından okunan HER DateTime
        /// değerine otomatik olarak "bu bir UTC değeridir" (Kind=Utc) etiketini
        /// vuruyoruz. Bu sayede JSON'a her zaman doğru "Z" son ekiyle gider ve
        /// tarayıcı doğru şekilde kullanıcının yerel saatine çevirir. Bu, projede
        /// "önce doğru DateTime.UtcNow yazmak" kadar önemli bir adımdır — sadece
        /// yazarken değil, OKURKEN de UTC olduğunu netleştirmek gerekir.
        /// </summary>
        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
            configurationBuilder.Properties<DateTime?>().HaveConversion<UtcNullableDateTimeConverter>();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Bulten>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.BultenAdi)
                    .IsRequired()
                    .HasMaxLength(250);

                entity.Property(e => e.PdfUrl)
                    .IsRequired()
                    .HasMaxLength(500);

                entity.Property(e => e.GenelYoneticiOzeti)
                    .HasColumnType("nvarchar(max)");

                entity.Property(e => e.Durum)
                    .IsRequired()
                    .HasMaxLength(30)
                    .HasDefaultValue("Beklemede");

                entity.HasIndex(e => e.YayinTarihi);
                entity.HasIndex(e => e.PdfUrl).IsUnique();

                entity.HasMany(e => e.MetinBloklari)
                    .WithOne(m => m.Bulten)
                    .HasForeignKey(m => m.BultenId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(e => e.AnalizSonuclari)
                    .WithOne(a => a.Bulten)
                    .HasForeignKey(a => a.BultenId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<BultenMetinBlogu>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.HamMetinIcerigi)
                    .IsRequired()
                    .HasColumnType("nvarchar(max)");

                entity.HasIndex(e => new { e.BultenId, e.BlokSiraNo });
            });

            modelBuilder.Entity<BultenAnalizSonucu>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.EtkilenenUnsur)
                    .IsRequired()
                    .HasMaxLength(150);

                entity.Property(e => e.Sektor)
                    .HasMaxLength(100);

                entity.Property(e => e.DuyguDurumu)
                    .IsRequired()
                    .HasMaxLength(10);

                entity.Property(e => e.Vade)
                    .HasMaxLength(20);

                entity.Property(e => e.HisseKodu)
                    .HasMaxLength(10);

                entity.Property(e => e.AiGerekceYorumu)
                    .HasColumnType("nvarchar(max)");

                entity.HasIndex(e => e.BultenId);
                entity.HasIndex(e => e.Sektor);
                entity.HasIndex(e => e.HisseKodu);
            });

            modelBuilder.Entity<Kullanici>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.KullaniciAdi)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(e => e.SifreHash)
                    .IsRequired()
                    .HasMaxLength(300);

                entity.Property(e => e.Rol)
                    .IsRequired()
                    .HasMaxLength(20)
                    .HasDefaultValue("Admin");

                entity.Property(e => e.Aktif)
                    .HasDefaultValue(true);

                entity.HasIndex(e => e.KullaniciAdi).IsUnique();
            });
        }
    }
}