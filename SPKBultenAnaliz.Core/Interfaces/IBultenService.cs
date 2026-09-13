using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SPKBultenAnaliz.Core.DTOs;
using SPKBultenAnaliz.Core.Entities;

namespace SPKBultenAnaliz.Core.Interfaces
{
    /// <summary>
    /// Business katmanının orkestrasyon servisi: scraping, parsing, chunking
    /// ve AI analizini uçtan uca yöneten servisin sözleşmesi.
    /// </summary>
    public interface IBultenService
    {
        /// <summary>SPK sitesini tarar, yeni bültenleri indirir, işler ve veritabanına kaydeder.</summary>
        Task<int> YeniBultenleriIsleAsync(CancellationToken cancellationToken = default);

        /// <summary>Var olan bir bülteni (chunk'ları üzerinden) Gemini ile yeniden analiz eder.</summary>
        Task<Bulten> BulteniAnalizEtAsync(int bultenId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Durumu 'DogrulamaBekleniyor' veya 'Hata' olan tüm bültenleri 
        /// tespit edip Gemini analizini tekrar tetikler.
        /// </summary>
        /// <returns>Yeniden başarıyla işlenen bülten sayısı</returns>
        Task<int> BasarisizBultenleriYenidenIsleAsync(CancellationToken cancellationToken = default);

        Task<BultenOzetDto?> OzetGetirAsync(int bultenId);
        Task<List<BultenOzetDto>> TumOzetleriGetirAsync();
        Task SilAsync(int bultenId);

        // ---------------------------------------------------------------
        // ADMIN PANELİ: Manuel düzenleme, tekil yeniden analiz, tekil silme
        // ---------------------------------------------------------------

        /// <summary>Sadece bültenin genel yönetici özetini (GenelYoneticiOzeti) Gemini ile yeniden üretir; mevcut haber/şirket analizlerine dokunmaz.</summary>
        Task<BultenOzetDto> SadeceGenelOzetiYenileAsync(int bultenId, CancellationToken cancellationToken = default);

        /// <summary>Tek bir haberi (analiz sonucu satırını) Gemini ile yeniden analiz edip günceller.</summary>
        Task<AnalizSonucuDto> TekHaberiYenidenAnalizEtAsync(int bultenId, int analizId, CancellationToken cancellationToken = default);

        /// <summary>Admin tarafından bir haberin alanlarını (Sektör/Yön/Etki/Vade/Hisse/Gerekçe) manuel düzenler.</summary>
        Task<AnalizSonucuDto> HaberiManuelGuncelleAsync(int bultenId, int analizId, AnalizGuncelleRequest istek);

        /// <summary>Tek bir haberi (analiz sonucu satırını) siler.</summary>
        Task HaberiSilAsync(int bultenId, int analizId);
    }
}