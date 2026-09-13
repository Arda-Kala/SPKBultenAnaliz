using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SPKBultenAnaliz.Core.Entities;
using SPKBultenAnaliz.Core.Interfaces;

namespace SPKBultenAnaliz.DataAccess.Repositories
{
    public class EfKullaniciRepository : IKullaniciRepository
    {
        private readonly BultenDbContext _context;

        public EfKullaniciRepository(BultenDbContext context)
        {
            _context = context;
        }

        public async Task<Kullanici?> KullaniciAdiIleGetirAsync(string kullaniciAdi)
        {
            return await _context.Kullanicilar
                .FirstOrDefaultAsync(k => k.KullaniciAdi.ToLower() == kullaniciAdi.ToLower());
        }

        public async Task<Kullanici?> IdIleGetirAsync(int id)
        {
            return await _context.Kullanicilar.FindAsync(id);
        }

        public async Task<List<Kullanici>> TumunuGetirAsync()
        {
            return await _context.Kullanicilar.OrderBy(k => k.KullaniciAdi).ToListAsync();
        }

        public async Task<Kullanici> EkleAsync(Kullanici kullanici)
        {
            _context.Kullanicilar.Add(kullanici);
            await _context.SaveChangesAsync();
            return kullanici;
        }

        public async Task GuncelleAsync(Kullanici kullanici)
        {
            _context.Kullanicilar.Update(kullanici);
            await _context.SaveChangesAsync();
        }

        public async Task SilAsync(int id)
        {
            var kullanici = await _context.Kullanicilar.FindAsync(id);
            if (kullanici != null)
            {
                _context.Kullanicilar.Remove(kullanici);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<int> AktifKullaniciSayisiAsync()
        {
            return await _context.Kullanicilar.CountAsync(k => k.Aktif);
        }
    }
}
