using System.Net;
using System.Text.Json;
using SPKBultenAnaliz.Business.Services;

namespace SPKBultenAnaliz.API.Middleware
{
    /// <summary>
    /// Merkezi (global) hata yakalama middleware'i. Pipeline'daki EN DIŞ katmandır
    /// (Program.cs'te en başa eklenir) — kendinden SONRAKİ her şeyde (controller'lar,
    /// authentication, diğer middleware'ler) fırlatılan ve yakalanmayan (unhandled)
    /// her exception'ı burada tek bir noktadan yakalarız.
    ///
    /// NEDEN GEREKLİ?: Bu middleware olmadan, bir controller action'ında beklenmeyen
    /// bir exception (örn. null reference, veritabanı bağlantı hatası) fırlatılırsa,
    /// ASP.NET Core varsayılan olarak çıplak bir stack trace (Development'ta) veya
    /// içeriksiz bir "500 Internal Server Error" (Production'da) döner — istemci
    /// tarafı bundan hiçbir şey anlamaz VE biz sunucu tarafında bunu göremeyiz.
    ///
    /// Bu middleware iki şeyi garanti eder:
    ///  1) Her hata, tüm bağlamıyla (hangi endpoint, hangi kullanıcı, hangi exception
    ///     tipi) Serilog üzerinden loglanır — dosyaya ve (Fatal/Error seviyesinde) DB'ye.
    ///  2) İstemciye HER ZAMAN tutarlı, öngörülebilir bir JSON hata gövdesi döner
    ///     (iç detaylar/stack trace asla dışarı sızmaz — bilgi ifşası riski).
    /// </summary>
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;
        private readonly IHostEnvironment _env;

        public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger, IHostEnvironment env)
        {
            _next = next;
            _logger = logger;
            _env = env;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HataylaBasEt(context, ex);
            }
        }

        private async Task HataylaBasEt(HttpContext context, Exception ex)
        {
            // Exception tipine göre HTTP durum kodu ve log seviyesi belirle.
            // Bu ayrım önemlidir: beklenen bir "yetkisiz erişim" hatasıyla,
            // beklenmeyen bir "Gemini API kotası doldu" hatasını aynı ciddiyette
            // loglamak istemeyiz — ikincisi operasyon ekibinin UYANMASI gereken bir olaydır.
            var (statusCode, seviye, kullaniciMesaji) = ex switch
            {
                UnauthorizedAccessException => (HttpStatusCode.Unauthorized, LogLevel.Warning, "Bu işlem için yetkiniz bulunmuyor."),
                ArgumentException or InvalidOperationException => (HttpStatusCode.BadRequest, LogLevel.Warning, ex.Message),
                KeyNotFoundException => (HttpStatusCode.NotFound, LogLevel.Warning, "İstenen kayıt bulunamadı."),
                GeminiServisException => (HttpStatusCode.BadGateway, LogLevel.Error, "Yapay zeka servisi (Gemini) şu anda yanıt veremiyor. Lütfen daha sonra tekrar deneyin."),
                HttpRequestException => (HttpStatusCode.BadGateway, LogLevel.Error, "Dış bir servise (SPK sitesi veya Gemini API) ulaşılamadı."),
                TaskCanceledException or TimeoutException => (HttpStatusCode.GatewayTimeout, LogLevel.Error, "İstek zaman aşımına uğradı."),
                _ => (HttpStatusCode.InternalServerError, LogLevel.Error, "Beklenmeyen bir sunucu hatası oluştu.")
            };

            // Log() metodu, hem yapılandırılmış (structured) alanları (RequestPath,
            // StatusCode) hem de tam exception nesnesini (stack trace dahil) Serilog'a
            // geçirir. Bu sayede dosyada/DB'de "hangi endpoint'te, hangi exception
            // tipiyle, ne zaman" sorusuna geriye dönük cevap verebiliriz.
            _logger.Log(seviye, ex,
                "İşlenmeyen hata yakalandı. Yol: {RequestMethod} {RequestPath} | Durum Kodu: {StatusCode} | Kullanıcı: {KullaniciAdi}",
                context.Request.Method,
                context.Request.Path,
                (int)statusCode,
                context.User?.Identity?.Name ?? "anonim");

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)statusCode;

            var hataYaniti = new HataYanitDto
            {
                Basarili = false,
                Mesaj = kullaniciMesaji,
                HataKodu = ex.GetType().Name,
                IzlemeId = context.TraceIdentifier,
                // Stack trace SADECE Development ortamında dönülür — Production'da
                // iç sistem detaylarının (dosya yolları, kütüphane isimleri, sorgu
                // metinleri) dışarı sızması ciddi bir bilgi ifşası (bkz. OWASP
                // "Security Misconfiguration") riskidir.
                DetayGeliştirmeModu = _env.IsDevelopment() ? ex.ToString() : null
            };

            var json = JsonSerializer.Serialize(hataYaniti, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await context.Response.WriteAsync(json);
        }
    }

    /// <summary>İstemciye dönen standart, tutarlı hata gövdesi.</summary>
    public class HataYanitDto
    {
        public bool Basarili { get; set; }
        public string Mesaj { get; set; } = string.Empty;
        public string HataKodu { get; set; } = string.Empty;

        /// <summary>
        /// Bu isteğe özgü izleme kimliği (HttpContext.TraceIdentifier). Kullanıcı
        /// "bir hata aldım" dediğinde, bu ID'yi log dosyasında/DB'de arayarak
        /// tam olarak HANGİ istek olduğunu saniyeler içinde bulabilirsiniz —
        /// "ne zaman oldu, ne yaptın" diye soru-cevaba gerek kalmaz.
        /// </summary>
        public string IzlemeId { get; set; } = string.Empty;

        public string? DetayGeliştirmeModu { get; set; }
    }

    /// <summary>
    /// IApplicationBuilder üzerinde app.UseGlobalExceptionHandling() şeklinde
    /// okunaklı bir extension metodu sağlar (app.UseMiddleware&lt;T&gt;() yazmak
    /// yerine). Sadece okunabilirlik için — işlevsel bir fark yaratmaz.
    /// </summary>
    public static class GlobalExceptionMiddlewareExtensions
    {
        public static IApplicationBuilder UseGlobalExceptionHandling(this IApplicationBuilder app)
        {
            return app.UseMiddleware<GlobalExceptionMiddleware>();
        }
    }
}
