using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using SPKBultenAnaliz.Core.Entities;
using SPKBultenAnaliz.Core.Interfaces;

namespace SPKBultenAnaliz.DataAccess.Repositories
{
    public class CachedBultenRepository : IBultenRepository
    {
        private readonly EfBultenRepository _inner;
        private readonly IMemoryCache _cache;
        private static readonly TimeSpan CacheSuresi = TimeSpan.FromMinutes(5);

        public CachedBultenRepository(EfBultenRepository inner, IMemoryCache cache)
        {
            _inner = inner;
            _cache = cache;
        }

        private static string TumListeKey => "bultenler:tumu";
        private static string TekKey(int id) => $"bultenler:{id}";

        public async Task<Bulten> EkleAsync(Bulten bulten)
        {
            var sonuc = await _inner.EkleAsync(bulten);
            _cache.Remove(TumListeKey);
            return sonuc;
        }

        public async Task<Bulten?> IdyeGoreGetirAsync(int bultenId)
        {
            return await _cache.GetOrCreateAsync(TekKey(bultenId), async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheSuresi;
                return await _inner.IdyeGoreGetirAsync(bultenId);
            });
        }

        public async Task<List<Bulten>> TumunuGetirAsync()
        {
            var sonuc = await _cache.GetOrCreateAsync(TumListeKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheSuresi;
                return await _inner.TumunuGetirAsync();
            });
            return sonuc ?? new List<Bulten>();
        }

        public Task<List<Bulten>> TariheGoreGetirAsync(DateTime baslangic, DateTime bitis)
            => _inner.TariheGoreGetirAsync(baslangic, bitis);

        public async Task<Bulten> GuncelleAsync(Bulten bulten)
        {
            var sonuc = await _inner.GuncelleAsync(bulten);
            _cache.Remove(TekKey(bulten.Id));
            _cache.Remove(TumListeKey);
            return sonuc;
        }

        public async Task SilAsync(int bultenId)
        {
            await _inner.SilAsync(bultenId);
            _cache.Remove(TekKey(bultenId));
            _cache.Remove(TumListeKey);
        }

        public Task<bool> PdfUrlVarMiAsync(string pdfUrl) => _inner.PdfUrlVarMiAsync(pdfUrl);

        // Bu iki metot için önbellekleme yapmıyoruz — analiz tetikleyici akışlar
        // her zaman güncel veriye ihtiyaç duyar.
        public Task<Bulten?> PdfUrlIleGetirAsync(string pdfUrl) => _inner.PdfUrlIleGetirAsync(pdfUrl);
        public Task<List<Bulten>> DurumaGoreGetirAsync(string durum) => _inner.DurumaGoreGetirAsync(durum);

        public Task<BultenAnalizSonucu?> AnalizSonucuGetirAsync(int analizId) => _inner.AnalizSonucuGetirAsync(analizId);

        public async Task AnalizSonucuGuncelleAsync(BultenAnalizSonucu analiz)
        {
            await _inner.AnalizSonucuGuncelleAsync(analiz);
            // Bu haberin ait olduğu bültenin önbellekteki hem tekil hem toplu
            // görünümü artık bayat (stale) — ikisini de düşürüyoruz.
            _cache.Remove(TekKey(analiz.BultenId));
            _cache.Remove(TumListeKey);
        }

        public async Task AnalizSonucuSilAsync(int analizId)
        {
            var analiz = await _inner.AnalizSonucuGetirAsync(analizId);
            await _inner.AnalizSonucuSilAsync(analizId);
            if (analiz != null)
            {
                _cache.Remove(TekKey(analiz.BultenId));
                _cache.Remove(TumListeKey);
            }
        }
    }
}