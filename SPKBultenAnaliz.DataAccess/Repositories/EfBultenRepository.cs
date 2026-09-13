using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SPKBultenAnaliz.Core.Entities;
using SPKBultenAnaliz.Core.Interfaces;

namespace SPKBultenAnaliz.DataAccess.Repositories
{
    public class EfBultenRepository : IBultenRepository
    {
        private readonly BultenDbContext _context;

        public EfBultenRepository(BultenDbContext context)
        {
            _context = context;
        }

        public async Task<Bulten> EkleAsync(Bulten bulten)
        {
            _context.Bultenler.Add(bulten);
            await _context.SaveChangesAsync();
            return bulten;
        }

        public async Task<Bulten?> IdyeGoreGetirAsync(int bultenId)
        {
            return await _context.Bultenler
                .Include(b => b.MetinBloklari.OrderBy(m => m.BlokSiraNo))
                .Include(b => b.AnalizSonuclari)
                .FirstOrDefaultAsync(b => b.Id == bultenId);
        }

        public async Task<List<Bulten>> TumunuGetirAsync()
        {
            return await _context.Bultenler
                .Include(b => b.AnalizSonuclari)
                .OrderByDescending(b => b.YayinTarihi)
                .ToListAsync();
        }

        public async Task<List<Bulten>> TariheGoreGetirAsync(DateTime baslangic, DateTime bitis)
        {
            return await _context.Bultenler
                .Where(b => b.YayinTarihi >= baslangic && b.YayinTarihi <= bitis)
                .Include(b => b.AnalizSonuclari)
                .OrderByDescending(b => b.YayinTarihi)
                .ToListAsync();
        }

        public async Task<Bulten> GuncelleAsync(Bulten bulten)
        {
            _context.Bultenler.Update(bulten);
            await _context.SaveChangesAsync();
            return bulten;
        }

        public async Task SilAsync(int bultenId)
        {
            var bulten = await _context.Bultenler.FindAsync(bultenId);
            if (bulten != null)
            {
                _context.Bultenler.Remove(bulten);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> PdfUrlVarMiAsync(string pdfUrl)
        {
            return await _context.Bultenler.AnyAsync(b => b.PdfUrl == pdfUrl);
        }

        public async Task<Bulten?> PdfUrlIleGetirAsync(string pdfUrl)
        {
            return await _context.Bultenler
                .Include(b => b.MetinBloklari.OrderBy(m => m.BlokSiraNo))
                .Include(b => b.AnalizSonuclari)
                .FirstOrDefaultAsync(b => b.PdfUrl == pdfUrl);
        }

        public async Task<List<Bulten>> DurumaGoreGetirAsync(string durum)
        {
            return await _context.Bultenler
                .Include(b => b.MetinBloklari.OrderBy(m => m.BlokSiraNo))
                .Include(b => b.AnalizSonuclari)
                .Where(b => b.Durum == durum)
                .ToListAsync();
        }

        public async Task<BultenAnalizSonucu?> AnalizSonucuGetirAsync(int analizId)
        {
            return await _context.BultenAnalizSonuclari
                .Include(a => a.Bulten)
                .FirstOrDefaultAsync(a => a.Id == analizId);
        }

        public async Task AnalizSonucuGuncelleAsync(BultenAnalizSonucu analiz)
        {
            _context.BultenAnalizSonuclari.Update(analiz);
            await _context.SaveChangesAsync();
        }

        public async Task AnalizSonucuSilAsync(int analizId)
        {
            var analiz = await _context.BultenAnalizSonuclari.FindAsync(analizId);
            if (analiz != null)
            {
                _context.BultenAnalizSonuclari.Remove(analiz);
                await _context.SaveChangesAsync();
            }
        }
    }
}