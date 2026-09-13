using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SPKBultenAnaliz.Business.Services;
using SPKBultenAnaliz.Core.DTOs;
using SPKBultenAnaliz.Core.Interfaces;

namespace SPKBultenAnaliz.API.Controllers
{
    /// <summary>
    /// SPK bültenleri ile ilgili tüm HTTP uç noktalarını (endpoint) sunan Controller.
    /// Bu katman kasıtlı olarak "ince" (thin) tutulur: iş mantığı burada değil,
    /// IBultenService içinde yaşar (Single Responsibility Principle).
    ///
    /// YETKİLENDİRME: Okuma (GET) uç noktaları herkese açıktır (public dashboard).
    /// Veri değiştiren TÜM uç noktalar (tarama, silme, düzenleme, yeniden analiz)
    /// [Authorize] ile korunur — geçerli bir admin JWT'si gerektirir.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class BultenlerController : ControllerBase
    {
        private readonly IBultenService _bultenService;
        private readonly ILogger<BultenlerController> _logger;
        private readonly IServiceProvider _serviceProvider;

        public BultenlerController(IBultenService bultenService, ILogger<BultenlerController> logger, IServiceProvider serviceProvider)
        {
            _bultenService = bultenService;
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        // =====================================================================
        // OKUMA UÇLARI (herkese açık)
        // =====================================================================

        /// <summary>Tüm bültenlerin özetlerini listeler (Dashboard'un ana veri kaynağı).</summary>
        [HttpGet]
        [ProducesResponseType(typeof(List<BultenOzetDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> TumBultenleriGetir()
        {
            var ozetler = await _bultenService.TumOzetleriGetirAsync();
            return Ok(ozetler);
        }

        /// <summary>Tek bir bültenin özetini ve tüm etki analiz sonuçlarını getirir.</summary>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(BultenOzetDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> BultenGetir(int id)
        {
            var ozet = await _bultenService.OzetGetirAsync(id);
            if (ozet == null)
                return NotFound(new { mesaj = $"'{id}' numaralı bülten bulunamadı." });

            return Ok(ozet);
        }

        // =====================================================================
        // YAZMA UÇLARI (Admin girişi gerektirir)
        // =====================================================================

        /// <summary>SPK sitesini manuel olarak taratır.</summary>
        [HttpPost("tara")]
        [Authorize]
        [ProducesResponseType(typeof(object), StatusCodes.Status202Accepted)]
        public IActionResult ManuelTara()
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var bultenService = scope.ServiceProvider.GetRequiredService<IBultenService>();
                    await bultenService.YeniBultenleriIsleAsync(CancellationToken.None);
                }
                catch (Exception ex)
                {
                    var logger = _serviceProvider.GetRequiredService<ILogger<BultenlerController>>();
                    logger.LogError(ex, "Arka planda bülten tarama/analiz çalışırken hata oluştu.");
                }
            });

            return Accepted(new { mesaj = "Bülten tarama ve analiz işlemi arka planda başlatıldı. İlerlemeyi konsol loglarından takip edebilirsiniz." });
        }

        /// <summary>Var olan bir bülteni Gemini ile TÜMÜYLE yeniden analiz eder (özet + tüm haberler yeniden üretilir).</summary>
        [HttpPost("{id:int}/analiz")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status502BadGateway)]
        public async Task<IActionResult> YenidenAnalizEt(int id, CancellationToken cancellationToken)
        {
            try
            {
                var bulten = await _bultenService.BulteniAnalizEtAsync(id, cancellationToken);
                return Ok(new { mesaj = "Analiz tamamlandı.", durum = bulten.Durum });
            }
            catch (InvalidOperationException)
            {
                return NotFound(new { mesaj = $"'{id}' numaralı bülten bulunamadı." });
            }
            catch (GeminiServisException ex)
            {
                _logger.LogError(ex, "Gemini analizi başarısız oldu (bülten {Id})", id);
                return StatusCode(StatusCodes.Status502BadGateway, new
                {
                    mesaj = "Analiz motoru şu an yoğun, lütfen daha sonra tekrar deneyiniz."
                });
            }
        }

        /// <summary>Sadece bültenin genel yönetici özetini yeniler; haber/şirket analizlerine dokunmaz (daha hızlı/ucuz).</summary>
        [HttpPost("{id:int}/ozet-yenile")]
        [Authorize]
        [ProducesResponseType(typeof(BultenOzetDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status502BadGateway)]
        public async Task<IActionResult> OzetYenile(int id, CancellationToken cancellationToken)
        {
            try
            {
                var guncelOzet = await _bultenService.SadeceGenelOzetiYenileAsync(id, cancellationToken);
                return Ok(guncelOzet);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { mesaj = ex.Message });
            }
            catch (GeminiServisException ex)
            {
                _logger.LogError(ex, "Gemini özet yenileme başarısız oldu (bülten {Id})", id);
                return StatusCode(StatusCodes.Status502BadGateway, new { mesaj = "Analiz motoru şu an yoğun, lütfen daha sonra tekrar deneyiniz." });
            }
        }

        /// <summary>
        /// Durumu "DogrulamaBekleniyor" veya "Beklemede" olan tüm bültenleri
        /// Gemini ile yeniden analiz eder.
        /// </summary>
        [HttpPost("yeniden-isle")]
        [Authorize]
        [ProducesResponseType(typeof(object), StatusCodes.Status202Accepted)]
        public IActionResult BasarisizlariYenidenIsle()
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var bultenService = scope.ServiceProvider.GetRequiredService<IBultenService>();
                    var sayi = await bultenService.BasarisizBultenleriYenidenIsleAsync(CancellationToken.None);
                    _logger.LogInformation("{Sayi} başarısız bülten yeniden işlendi.", sayi);
                }
                catch (Exception ex)
                {
                    var logger = _serviceProvider.GetRequiredService<ILogger<BultenlerController>>();
                    logger.LogError(ex, "Başarısız bültenler yeniden işlenirken hata oluştu.");
                }
            });

            return Accepted(new { mesaj = "Başarısız/eksik bültenler için yeniden analiz arka planda başlatıldı." });
        }

        /// <summary>Bir bülteni ve ilişkili tüm verilerini (metin blokları, analiz sonuçları) siler.</summary>
        [HttpDelete("{id:int}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> Sil(int id)
        {
            await _bultenService.SilAsync(id);
            return NoContent();
        }

        // =====================================================================
        // TEKİL HABER (analiz sonucu) YÖNETİMİ — Admin girişi gerektirir
        // =====================================================================

        /// <summary>Tek bir haberi (şirket/sektör analiz satırını) Gemini ile yeniden değerlendirir.</summary>
        [HttpPost("{bultenId:int}/analizler/{analizId:int}/yeniden-analiz")]
        [Authorize]
        [ProducesResponseType(typeof(AnalizSonucuDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status502BadGateway)]
        public async Task<IActionResult> HaberiYenidenAnalizEt(int bultenId, int analizId, CancellationToken cancellationToken)
        {
            try
            {
                var sonuc = await _bultenService.TekHaberiYenidenAnalizEtAsync(bultenId, analizId, cancellationToken);
                return Ok(sonuc);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { mesaj = ex.Message });
            }
            catch (GeminiServisException ex)
            {
                _logger.LogError(ex, "Gemini tekil haber analizi başarısız oldu (bülten {BultenId}, haber {AnalizId})", bultenId, analizId);
                return StatusCode(StatusCodes.Status502BadGateway, new { mesaj = "Analiz motoru şu an yoğun, lütfen daha sonra tekrar deneyiniz." });
            }
        }

        /// <summary>Admin tarafından bir haberin alanlarını (Sektör/Yön/Etki/Vade/Hisse/Gerekçe) manuel düzenler.</summary>
        [HttpPut("{bultenId:int}/analizler/{analizId:int}")]
        [Authorize]
        [ProducesResponseType(typeof(AnalizSonucuDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> HaberiGuncelle(int bultenId, int analizId, [FromBody] AnalizGuncelleRequest istek)
        {
            try
            {
                var sonuc = await _bultenService.HaberiManuelGuncelleAsync(bultenId, analizId, istek);
                return Ok(sonuc);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { mesaj = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { mesaj = ex.Message });
            }
        }

        /// <summary>Tek bir haberi (analiz sonucu satırını) siler.</summary>
        [HttpDelete("{bultenId:int}/analizler/{analizId:int}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> HaberiSil(int bultenId, int analizId)
        {
            try
            {
                await _bultenService.HaberiSilAsync(bultenId, analizId);
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { mesaj = ex.Message });
            }
        }
    }
}
