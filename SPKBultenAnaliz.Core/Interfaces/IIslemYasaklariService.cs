using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SPKBultenAnaliz.Core.DTOs;

namespace SPKBultenAnaliz.Core.Interfaces
{
    /// <summary>
    /// SPK'nın resmi Web Servisi (ws.spk.gov.tr) üzerinden işlem yasaklı
    /// kişi/şirket listelerini çeken servisin sözleşmesi.
    /// </summary>
    public interface IIslemYasaklariService
    {
        Task<List<IslemYasakliDto>> TumKisileriGetirAsync(CancellationToken cancellationToken = default);
        Task<List<IslemYasakliDto>> TumSirketleriGetirAsync(CancellationToken cancellationToken = default);
    }
}
