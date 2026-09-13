using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SPKBultenAnaliz.Core.Interfaces
{
    /// <summary>Bir SPK bülten ilanının web sitesinden tespit edilen özet bilgisi.</summary>
    public class TespitEdilenBulten
    {
        public string BultenAdi { get; set; } = string.Empty;
        public string PdfUrl { get; set; } = string.Empty;
        public System.DateTime YayinTarihi { get; set; }
    }

    /// <summary>
    /// SPK web sitesini periyodik olarak tarayıp yeni yayınlanan bültenleri
    /// tespit eden servisin sözleşmesi.
    /// </summary>
    public interface ISpkScraperService
    {
        Task<List<TespitEdilenBulten>> YeniBultenleriTespitEtAsync(CancellationToken cancellationToken = default);
        Task<byte[]> PdfIndirAsync(string pdfUrl, CancellationToken cancellationToken = default);
    }
}
