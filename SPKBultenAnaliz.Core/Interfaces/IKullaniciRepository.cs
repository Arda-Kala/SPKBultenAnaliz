using System.Collections.Generic;
using System.Threading.Tasks;
using SPKBultenAnaliz.Core.Entities;

namespace SPKBultenAnaliz.Core.Interfaces
{
    public interface IKullaniciRepository
    {
        Task<Kullanici?> KullaniciAdiIleGetirAsync(string kullaniciAdi);
        Task<Kullanici?> IdIleGetirAsync(int id);
        Task<List<Kullanici>> TumunuGetirAsync();
        Task<Kullanici> EkleAsync(Kullanici kullanici);
        Task GuncelleAsync(Kullanici kullanici);
        Task SilAsync(int id);
        Task<int> AktifKullaniciSayisiAsync();
    }
}
