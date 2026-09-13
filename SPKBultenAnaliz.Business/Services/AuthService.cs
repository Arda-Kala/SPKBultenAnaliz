using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SPKBultenAnaliz.Business.Configuration;
using SPKBultenAnaliz.Business.Security;
using SPKBultenAnaliz.Core.DTOs;
using SPKBultenAnaliz.Core.Entities;
using SPKBultenAnaliz.Core.Interfaces;

namespace SPKBultenAnaliz.Business.Services
{
    /// <summary>
    /// IAuthService'in somut uygulaması: kullanıcı doğrulama, JWT üretimi ve
    /// temel kullanıcı yönetimi (ekleme/silme/şifre değiştirme).
    /// </summary>
    public class AuthService : IAuthService
    {
        private readonly IKullaniciRepository _repository;
        private readonly JwtOptions _jwtOptions;
        private readonly ILogger<AuthService> _logger;

        public AuthService(IKullaniciRepository repository, IOptions<JwtOptions> jwtOptions, ILogger<AuthService> logger)
        {
            _repository = repository;
            _jwtOptions = jwtOptions.Value;
            _logger = logger;
        }

        public async Task<GirisResponse?> GirisYapAsync(string kullaniciAdi, string sifre)
        {
            var kullanici = await _repository.KullaniciAdiIleGetirAsync(kullaniciAdi);

            if (kullanici == null || !kullanici.Aktif)
            {
                // Kullanıcı adı hatalıysa da şifre yanlışmış gibi genel bir
                // hata döneriz (controller katmanında) — böylece bir saldırgan
                // "bu kullanıcı adı var mı yok mu" bilgisini çıkaramaz
                // (user enumeration saldırısına karşı önlem).
                _logger.LogWarning("Başarısız giriş denemesi: kullanıcı bulunamadı veya pasif ({KullaniciAdi}).", kullaniciAdi);
                return null;
            }

            if (!SifreHashService.Dogrula(sifre, kullanici.SifreHash))
            {
                _logger.LogWarning("Başarısız giriş denemesi: hatalı şifre ({KullaniciAdi}).", kullaniciAdi);
                return null;
            }

            kullanici.SonGirisTarihi = DateTime.UtcNow;
            await _repository.GuncelleAsync(kullanici);

            var (token, sonaErme) = TokenUret(kullanici);

            _logger.LogInformation("Başarılı giriş: {KullaniciAdi}.", kullaniciAdi);

            return new GirisResponse
            {
                Token = token,
                KullaniciAdi = kullanici.KullaniciAdi,
                Rol = kullanici.Rol,
                SonaErmeTarihi = sonaErme
            };
        }

        private (string Token, DateTime SonaErme) TokenUret(Kullanici kullanici)
        {
            var sonaErme = DateTime.UtcNow.AddMinutes(_jwtOptions.SureDakika);

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, kullanici.Id.ToString()),
                new(ClaimTypes.Name, kullanici.KullaniciAdi),
                new(ClaimTypes.Role, kullanici.Rol)
            };

            var anahtar = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Key));
            var kimlikBilgisi = new SigningCredentials(anahtar, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _jwtOptions.Issuer,
                audience: _jwtOptions.Audience,
                claims: claims,
                expires: sonaErme,
                signingCredentials: kimlikBilgisi);

            return (new JwtSecurityTokenHandler().WriteToken(token), sonaErme);
        }

        public async Task<List<KullaniciDto>> TumKullanicilariGetirAsync()
        {
            var kullanicilar = await _repository.TumunuGetirAsync();
            return kullanicilar.Select(Eslestir).ToList();
        }

        public async Task<KullaniciDto> KullaniciEkleAsync(KullaniciEkleRequest istek)
        {
            if (string.IsNullOrWhiteSpace(istek.KullaniciAdi) || istek.KullaniciAdi.Trim().Length < 3)
                throw new ArgumentException("Kullanıcı adı en az 3 karakter olmalıdır.");

            if (string.IsNullOrWhiteSpace(istek.Sifre) || istek.Sifre.Length < 6)
                throw new ArgumentException("Şifre en az 6 karakter olmalıdır.");

            var mevcut = await _repository.KullaniciAdiIleGetirAsync(istek.KullaniciAdi.Trim());
            if (mevcut != null)
                throw new InvalidOperationException("Bu kullanıcı adı zaten kullanılıyor.");

            var kullanici = new Kullanici
            {
                KullaniciAdi = istek.KullaniciAdi.Trim(),
                SifreHash = SifreHashService.Hashle(istek.Sifre),
                Rol = string.IsNullOrWhiteSpace(istek.Rol) ? "Admin" : istek.Rol
            };

            kullanici = await _repository.EkleAsync(kullanici);
            return Eslestir(kullanici);
        }

        public async Task SifreDegistirAsync(int kullaniciId, string eskiSifre, string yeniSifre)
        {
            var kullanici = await _repository.IdIleGetirAsync(kullaniciId)
                ?? throw new InvalidOperationException("Kullanıcı bulunamadı.");

            if (!SifreHashService.Dogrula(eskiSifre, kullanici.SifreHash))
                throw new UnauthorizedAccessException("Mevcut şifre hatalı.");

            if (string.IsNullOrWhiteSpace(yeniSifre) || yeniSifre.Length < 6)
                throw new ArgumentException("Yeni şifre en az 6 karakter olmalıdır.");

            kullanici.SifreHash = SifreHashService.Hashle(yeniSifre);
            await _repository.GuncelleAsync(kullanici);
        }

        public async Task KullaniciSilAsync(int kullaniciId, int silenKullaniciId)
        {
            if (kullaniciId == silenKullaniciId)
                throw new InvalidOperationException("Kendi hesabınızı silemezsiniz.");

            var aktifSayi = await _repository.AktifKullaniciSayisiAsync();
            if (aktifSayi <= 1)
                throw new InvalidOperationException("Sistemdeki son kullanıcı silinemez (sistemin kilitlenmesini önlemek için).");

            await _repository.SilAsync(kullaniciId);
        }

        /// <summary>
        /// SQL Server'dan okunan DateTime değerleri EF Core tarafından
        /// "Unspecified" Kind ile döner (UTC bilgisini kaybeder). Bu, JSON'a
        /// "Z" son eki eklenmeden gitmesine ve tarayıcının bu tarihi YANLIŞLIKLA
        /// yerel saatmiş gibi yorumlamasına sebep olur (örn. Türkiye'de 3 saat
        /// hatalı gösterim). Veritabanına her zaman UtcNow yazdığımız için,
        /// dışarı DTO olarak çıkarken Kind'ı açıkça Utc olarak işaretliyoruz.
        /// </summary>
        private static DateTime? UtcOlarakIsaretle(DateTime? deger) =>
            deger.HasValue ? DateTime.SpecifyKind(deger.Value, DateTimeKind.Utc) : null;

        private static DateTime UtcOlarakIsaretle(DateTime deger) =>
            DateTime.SpecifyKind(deger, DateTimeKind.Utc);

        private static KullaniciDto Eslestir(Kullanici k) => new()
        {
            Id = k.Id,
            KullaniciAdi = k.KullaniciAdi,
            Rol = k.Rol,
            OlusturmaTarihi = UtcOlarakIsaretle(k.OlusturmaTarihi),
            SonGirisTarihi = UtcOlarakIsaretle(k.SonGirisTarihi),
            Aktif = k.Aktif
        };
    }
}