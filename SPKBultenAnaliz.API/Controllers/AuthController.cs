using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SPKBultenAnaliz.Core.DTOs;
using SPKBultenAnaliz.Core.Interfaces;

namespace SPKBultenAnaliz.API.Controllers
{
    /// <summary>
    /// Kimlik doğrulama ve admin kullanıcı yönetimi uç noktaları.
    /// Giriş dışındaki tüm uç noktalar [Authorize] ile korunur — yani geçerli
    /// bir JWT (Authorization: Bearer &lt;token&gt; header'ında) gerektirir.
    /// </summary>
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IAuthService authService, ILogger<AuthController> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        /// <summary>Kullanıcı adı/şifre ile giriş yapar, başarılıysa bir JWT döner.</summary>
        [HttpPost("giris")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(GirisResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Giris([FromBody] GirisRequest istek)
        {
            if (string.IsNullOrWhiteSpace(istek.KullaniciAdi) || string.IsNullOrWhiteSpace(istek.Sifre))
                return BadRequest(new { mesaj = "Kullanıcı adı ve şifre gereklidir." });

            var sonuc = await _authService.GirisYapAsync(istek.KullaniciAdi.Trim(), istek.Sifre);

            if (sonuc == null)
                return Unauthorized(new { mesaj = "Kullanıcı adı veya şifre hatalı." });

            return Ok(sonuc);
        }

        /// <summary>Geçerli JWT ile giriş yapmış kullanıcının bilgisini döner (token'ın hâlâ geçerli olduğunu doğrulamak için kullanılabilir).</summary>
        [HttpGet("ben")]
        [Authorize]
        public IActionResult Ben()
        {
            return Ok(new
            {
                kullaniciAdi = User.Identity?.Name,
                rol = User.IsInRole("Admin") ? "Admin" : "Bilinmiyor"
            });
        }

        /// <summary>Sistemdeki tüm admin kullanıcılarını listeler (şifre hash'leri asla dönmez).</summary>
        [HttpGet("kullanicilar")]
        [Authorize]
        [ProducesResponseType(typeof(List<KullaniciDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Kullanicilar()
        {
            return Ok(await _authService.TumKullanicilariGetirAsync());
        }

        /// <summary>Yeni bir admin kullanıcısı ekler.</summary>
        [HttpPost("kullanicilar")]
        [Authorize]
        [ProducesResponseType(typeof(KullaniciDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> KullaniciEkle([FromBody] KullaniciEkleRequest istek)
        {
            try
            {
                var yeniKullanici = await _authService.KullaniciEkleAsync(istek);
                return CreatedAtAction(nameof(Kullanicilar), new { id = yeniKullanici.Id }, yeniKullanici);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return BadRequest(new { mesaj = ex.Message });
            }
        }

        /// <summary>Giriş yapmış kullanıcının kendi şifresini değiştirmesini sağlar.</summary>
        [HttpPost("sifre-degistir")]
        [Authorize]
        public async Task<IActionResult> SifreDegistir([FromBody] SifreDegistirRequest istek)
        {
            var kullaniciId = GecerliKullaniciId();
            if (kullaniciId == null)
                return Unauthorized();

            try
            {
                await _authService.SifreDegistirAsync(kullaniciId.Value, istek.EskiSifre, istek.YeniSifre);
                return Ok(new { mesaj = "Şifre başarıyla değiştirildi." });
            }
            catch (UnauthorizedAccessException ex)
            {
                return BadRequest(new { mesaj = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { mesaj = ex.Message });
            }
        }

        /// <summary>Bir admin kullanıcısını siler (kendini veya son kullanıcıyı silmeye izin verilmez).</summary>
        [HttpDelete("kullanicilar/{id:int}")]
        [Authorize]
        public async Task<IActionResult> KullaniciSil(int id)
        {
            var kullaniciId = GecerliKullaniciId();
            if (kullaniciId == null)
                return Unauthorized();

            try
            {
                await _authService.KullaniciSilAsync(id, kullaniciId.Value);
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { mesaj = ex.Message });
            }
        }

        private int? GecerliKullaniciId()
        {
            var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(claim, out var id) ? id : null;
        }
    }
}
