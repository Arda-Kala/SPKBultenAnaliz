using System.Threading;
using System.Threading.Tasks;
using SPKBultenAnaliz.Core.DTOs;

namespace SPKBultenAnaliz.Core.Interfaces
{
    /// <summary>
    /// Yapay zeka analiz sağlayıcısının soyutlaması (arayüzü).
    /// ÖNEMLİ: Bu arayüz kasıtlı olarak "Gemini" adını taşımaz — çünkü yarın
    /// başka bir sağlayıcıya (örn. Claude, OpenAI) geçilirse, sadece bu
    /// arayüzü uygulayan yeni bir somut sınıf yazılır; Business/API katmanları
    /// hiç değişmez (Dependency Inversion Principle / Open-Closed Principle).
    /// </summary>
    public interface IAiAnalysisService
    {
        /// <summary>
        /// Verilen ham bülten metnini analiz ederek yönetici özeti ve
        /// piyasa etki sonuçlarını üretir.
        /// </summary>
        Task<GeminiAnalizSonucu> AnalizEtAsync(string hamMetin, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sadece kısa bir yönetici özeti üretir (haber/şirket bazlı etki
        /// analizine dokunmadan). Admin panelinde "sadece genel özeti yenile"
        /// eylemi için kullanılır — tam analize göre çok daha ucuz/hızlıdır.
        /// </summary>
        Task<string> GenelOzetUretAsync(string hamMetin, CancellationToken cancellationToken = default);

        /// <summary>
        /// Ham bülten metni içinde, ADI VERİLEN tek bir unsuru (şirket/sektör)
        /// yeniden değerlendirir. Admin panelinde "tek bir haberi yeniden
        /// analiz et" eylemi için kullanılır.
        /// </summary>
        Task<GeminiEtkiUnsuru> TekUnsuruAnalizEtAsync(string hamMetin, string unsurAdi, CancellationToken cancellationToken = default);

        Task<ChatCevapDto> SoruSorAsync(string soru, string bultenContext, CancellationToken cancellationToken = default);
    }
}
