using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SPKBultenAnaliz.Core.Interfaces;
using UglyToad.PdfPig.Content;
using Microsoft.AspNetCore.Mvc;
using SPKBultenAnaliz.Core.Interfaces;

namespace SPKBultenAnaliz.API.Controllers
{
    /// <summary>
    /// SPK'nın resmi Web Servisi üzerinden işlem yasaklı kişi/şirket
    /// listelerini sunan uç noktalar. Herkese açıktır (okuma amaçlı, admin
    /// girişi gerektirmez) — tıpkı SPK'nın kendi kamuya açık sayfası gibi.
    /// </summary>
    [ApiController]
    [Route("api/islem-yasaklari")]
    public class IslemYasaklariController : ControllerBase
    {
        private readonly IIslemYasaklariService _service;
        private readonly ILogger<IslemYasaklariController> _logger;

        public IslemYasaklariController(IIslemYasaklariService service, ILogger<IslemYasaklariController> logger)
        {
            _service = service;
            _logger = logger;
        }

        /// <summary>Tüm işlem yasaklı gerçek kişileri döner (6 saat önbellekli).</summary>
        [HttpGet("kisiler")]
        public async Task<IActionResult> Kisiler(CancellationToken cancellationToken)
        {
            var sonuc = await _service.TumKisileriGetirAsync(cancellationToken);
            return Ok(sonuc);
        }

        /// <summary>Tüm işlem yasaklı şirketleri/tüzel kişileri döner (6 saat önbellekli).</summary>
        [HttpGet("sirketler")]
        public async Task<IActionResult> Sirketler(CancellationToken cancellationToken)
        {
            var sonuc = await _service.TumSirketleriGetirAsync(cancellationToken);
            return Ok(sonuc);
        }

        /// <summary>Kişi ve şirket listelerini tek seferde, birlikte döner (dashboard için pratik).</summary>
        [HttpGet("tumu")]
        public async Task<IActionResult> Tumu(CancellationToken cancellationToken)
        {
            var kisiler = await _service.TumKisileriGetirAsync(cancellationToken);
            var sirketler = await _service.TumSirketleriGetirAsync(cancellationToken);

            return Ok(new
            {
                kisiler,
                sirketler,
                toplamKisi = kisiler.Count,
                toplamSirket = sirketler.Count,
                cekilmeZamani = DateTime.UtcNow
            });
        }
    }
}


