using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SPKBultenAnaliz.Core.DTOs;
using SPKBultenAnaliz.Core.Interfaces;

namespace SPKBultenAnaliz.API.Controllers
{
    [ApiController]
    [Route("api/chat")]
    public class ChatController : ControllerBase
    {
        private readonly IAiAnalysisService _aiAnalysisService;
        private readonly IBultenRepository _bultenRepository;
        private readonly ILogger<ChatController> _logger;

        // Dependency Injection: Gerekli servisler constructor üzerinden alınır
        public ChatController(
            IAiAnalysisService aiAnalysisService,
            IBultenRepository bultenRepository,
            ILogger<ChatController> logger)
        {
            _aiAnalysisService = aiAnalysisService;
            _bultenRepository = bultenRepository;
            _logger = logger;
        }
        [HttpPost("ask")]
        public async Task<IActionResult> Ask([FromBody] ChatSoruDto istek, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(istek?.Soru))
                return BadRequest(new { mesaj = "Lütfen geçerli bir soru girin." });

            try
            {
                // 1. Veritabanındaki TÜM bültenleri çek (.Take(10) kaldırıldı)
                var tumBultenler = await _bultenRepository.TumunuGetirAsync();

                var bultenler = tumBultenler?
                    .OrderByDescending(b => b.YayinTarihi)
                    .ToList();

                var jsonOptions = new System.Text.Json.JsonSerializerOptions
                {
                    ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
                };

                // 2. AI için tüm veritabanını kapsayan bağlam metnini oluştur
                string bultenContext = bultenler != null && bultenler.Any()
                    ? string.Join("\n\n", bultenler.Select(b =>
                        $"Bülten Adı: {b.BultenAdi}\n" +
                        $"Yayın Tarihi: {b.YayinTarihi:dd.MM.yyyy}\n" +
                        $"Genel Özet: {b.GenelYoneticiOzeti ?? "Özet bulunmuyor."}\n" +
                        $"Haberler/Analizler: {(b.AnalizSonuclari != null ? JsonSerializer.Serialize(b.AnalizSonuclari, jsonOptions) : "[]")}"))
                    : "Sistemde henüz kayıtlı SPK bülteni bulunmamaktadır.";

                // 3. Gemini servis çağrısını yap
                var cevap = await _aiAnalysisService.SoruSorAsync(istek.Soru, bultenContext, cancellationToken);
                return Ok(cevap);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Chatbot yanıt üretirken hata oluştu.");
                return StatusCode(500, new { mesaj = "AI yanıtı üretilirken teknik bir hata oluştu.", detay = ex.Message });
            }
        } 
    }
    }