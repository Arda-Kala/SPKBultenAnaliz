using SPKBultenAnaliz.Core.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SPKBultenAnaliz.Core.Entities;

namespace SPKBultenAnaliz.Core.Interfaces
{
    public interface IBultenRepository
    {
        Task<Bulten> EkleAsync(Bulten bulten);
        Task<Bulten?> IdyeGoreGetirAsync(int bultenId);
        Task<List<Bulten>> TumunuGetirAsync();
        Task<List<Bulten>> TariheGoreGetirAsync(DateTime baslangic, DateTime bitis);
        Task<Bulten> GuncelleAsync(Bulten bulten);
        Task SilAsync(int bultenId);
        Task<bool> PdfUrlVarMiAsync(string pdfUrl);

        /// <summary>
        /// PdfUrl'e göre bülteni getirir. "DogrulamaBekleniyor" durumundaki
        /// bültenleri yeniden analiz kararında kullanılır.
        /// </summary>
        Task<Bulten?> PdfUrlIleGetirAsync(string pdfUrl);

        /// <summary>Belirtilen durumdaki tüm bültenleri getirir.</summary>
        Task<List<Bulten>> DurumaGoreGetirAsync(string durum);

        // ---------------------------------------------------------------
        // ADMIN PANELİ: Tekil analiz sonucu (haber/şirket satırı) yönetimi
        // ---------------------------------------------------------------

        /// <summary>Tek bir analiz sonucunu (haberi), ait olduğu bülten bilgisiyle birlikte getirir.</summary>
        Task<BultenAnalizSonucu?> AnalizSonucuGetirAsync(int analizId);

        /// <summary>Var olan bir analiz sonucunu (admin manuel düzenlemesi veya yeniden analiz sonrası) günceller.</summary>
        Task AnalizSonucuGuncelleAsync(BultenAnalizSonucu analiz);

        /// <summary>Tek bir analiz sonucunu (haberi) siler.</summary>
        Task AnalizSonucuSilAsync(int analizId);
    }
}
