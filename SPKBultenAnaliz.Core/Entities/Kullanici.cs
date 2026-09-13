using System;

namespace SPKBultenAnaliz.Core.Entities
{
    /// <summary>
    /// Admin paneline giriş yapabilen kullanıcı. Bu proje kapsamında tek rol
    /// (Admin) yeterlidir; ileride rol bazlı yetkilendirme (örn. "Editor",
    /// "Görüntüleyici") gerekirse Rol alanı üzerinden genişletilebilir.
    /// </summary>
    public class Kullanici
    {
        public int Id { get; set; }

        public string KullaniciAdi { get; set; } = string.Empty;

        /// <summary>
        /// Şifrenin KENDİSİ ASLA saklanmaz — PBKDF2 (Rfc2898DeriveBytes) ile
        /// tuzlanmış (salted) tek yönlü özeti (hash) saklanır. Format:
        /// "{iterasyonSayisi}.{tuzBase64}.{hashBase64}" — bkz. SifreHashService.
        /// </summary>
        public string SifreHash { get; set; } = string.Empty;

        public string Rol { get; set; } = "Admin";

        public DateTime OlusturmaTarihi { get; set; } = DateTime.UtcNow;

        public DateTime? SonGirisTarihi { get; set; }

        public bool Aktif { get; set; } = true;
    }
}
