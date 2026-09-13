using System.Collections.Generic;
using System.Threading.Tasks;
using SPKBultenAnaliz.Core.DTOs;
using SPKBultenAnaliz.Core.Entities;

namespace SPKBultenAnaliz.Core.Interfaces
{
    /// <summary>
    /// Kimlik doğrulama (authentication) ve kullanıcı yönetimi sözleşmesi.
    /// </summary>
    public interface IAuthService
    {
        /// <summary>Kullanıcı adı/şifre doğrularsa bir JWT üretir; aksi halde null döner.</summary>
        Task<GirisResponse?> GirisYapAsync(string kullaniciAdi, string sifre);

        Task<List<KullaniciDto>> TumKullanicilariGetirAsync();

        Task<KullaniciDto> KullaniciEkleAsync(KullaniciEkleRequest istek);

        /// <summary>Son kalan admin kullanıcısı silinemez (sistemin kilitlenmesini önler).</summary>
        Task SifreDegistirAsync(int kullaniciId, string eskiSifre, string yeniSifre);

        Task KullaniciSilAsync(int kullaniciId, int silenKullaniciId);
    }
}
