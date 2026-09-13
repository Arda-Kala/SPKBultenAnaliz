using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using SPKBultenAnaliz.API.Middleware;
using SPKBultenAnaliz.Business.BackgroundJobs;
using SPKBultenAnaliz.Business.Configuration;
using SPKBultenAnaliz.Business.Security;
using SPKBultenAnaliz.Business.Services;
using SPKBultenAnaliz.Core.Entities;
using SPKBultenAnaliz.Core.Interfaces;
using SPKBultenAnaliz.DataAccess;
using SPKBultenAnaliz.DataAccess.Repositories;

// =====================================================================
// SERILOG — "BOOTSTRAP LOGGER"
// =====================================================================
// Bu, uygulamanın normal DI (Dependency Injection) sistemi henüz ayağa
// kalkmadan ÖNCE çalışan geçici bir logger'dır. Amacı tek bir şey:
// builder.Build() aşamasında (appsettings okuma, servis kayıtları, DB
// migration) bir hata olursa, bunu da loglayabilmek. Bu olmadan, "uygulama
// hiç başlamadı" türü hatalar hiçbir yere yazılmaz — sadece konsol
// penceresi kapanır ve neden çöktüğünü asla öğrenemezsiniz.
// Program.cs'in en sonunda gerçek (appsettings.json'dan okunan) Serilog
// yapılandırmasıyla DEĞİŞTİRİLİR (bkz. builder.Host.UseSerilog(...) altı).
// =====================================================================
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("SPK Bülten Analiz API başlatılıyor...");

    var builder = WebApplication.CreateBuilder(args);

    // =====================================================================
    // SERILOG — GERÇEK (appsettings.json tabanlı) YAPILANDIRMA
    // =====================================================================
    // builder.Host.UseSerilog(...), ASP.NET Core'un varsayılan logging
    // altyapısını (Microsoft.Extensions.Logging) TAMAMEN Serilog'a devreder.
    // Yani projedeki mevcut _logger.LogInformation(...) / LogWarning(...) /
    // LogError(...) çağrılarının (AuthService, BultenService, GeminiApiConnector
    // vb. — hiçbiri değişmedi) HEPSİ artık otomatik olarak Serilog üzerinden,
    // appsettings.json > "Serilog" bölümünde tanımlı sink'lere (Console, File,
    // MSSqlServer) akar. Var olan kodda TEK SATIR değişiklik gerekmez.
    //
    // readFrom.Configuration: appsettings.json / appsettings.{Environment}.json
    // içindeki "Serilog": { ... } bölümünü okur (bkz. bu dosyalardaki güncelleme).
    // enrichers (MachineName, ProcessId, ThreadId): her log satırına otomatik
    // olarak "bu hangi sunucudan/thread'den geldi" bilgisini ekler — birden
    // fazla instance/sunucu çalıştığında logları birbirinden ayırt etmek için.
    // =====================================================================
    builder.Host.UseSerilog((context, services, loggerConfig) =>
    {
        loggerConfig
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithProcessId()
            .Enrich.WithThreadId();
    });

// =====================================================================
// CORS — Sadece appsettings.json (veya ortam değişkeni) içinde açıkça
// tanımlanan origin'lere (örn. Dashboard'un yayınlandığı adres) izin
// verilir. "Cors:AllowedOrigins" tanımlanmamışsa veya boşsa, güvenlik
// açığı oluşturmamak için HİÇBİR origin'e izin verilmez (AllowAnyOrigin
// KULLANILMAZ). Development ortamında appsettings.Development.json
// içindeki localhost adresleri kullanılabilir.
//
// Ortam değişkeni ile override örneği (appsettings.json'daki değeri geçersiz kılar):
//   Cors__AllowedOrigins__0=https://dashboard.example.com
// =====================================================================
var izinliOriginler = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (izinliOriginler.Length > 0)
        {
            policy.WithOrigins(izinliOriginler)
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
        else
        {
            // Hiç origin tanımlanmamışsa istekleri reddet; sessizce
            // AllowAnyOrigin'e düşmek güvenlik açığı oluşturur.
            policy.WithOrigins(Array.Empty<string>());
        }
    });
});

// =====================================================================
// 1) VERİTABANI BAĞLANTISI (DataAccess Katmanı)
// =====================================================================
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("'DefaultConnection' bağlantı dizesi appsettings.json içinde tanımlanmamış.");

builder.Services.AddDbContext<BultenDbContext>(options =>
    options.UseSqlServer(connectionString));

// =====================================================================
// 2) GEMINI YAPILANDIRMASI (Business Katmanı)
// =====================================================================
builder.Services.Configure<GeminiOptions>(
    builder.Configuration.GetSection(GeminiOptions.SectionName));

// =====================================================================
// 2.1) JWT / ADMIN KİMLİK DOĞRULAMA YAPILANDIRMASI
// =====================================================================
builder.Services.Configure<JwtOptions>(
    builder.Configuration.GetSection(JwtOptions.SectionName));

var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
var jwtKey = jwtSection["Key"];

if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)
{
    throw new InvalidOperationException(
        "'Jwt:Key' appsettings.json içinde tanımlanmamış veya çok kısa (en az 32 karakter olmalı). " +
        "Rastgele güvenli bir anahtar üretmek için: openssl rand -base64 48");
}

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromSeconds(30) // sunucu saatleri arası küçük tolerans
        };
    });

builder.Services.AddAuthorization();

// =====================================================================
// 3) DEPENDENCY INJECTION KAYITLARI
// =====================================================================

// --- Repository (Decorator Pattern ile önbellekleme; Sprint 3 - Use Case 3.1) ---
builder.Services.AddMemoryCache();
builder.Services.AddScoped<EfBultenRepository>();
builder.Services.AddScoped<IBultenRepository>(sp =>
    new CachedBultenRepository(
        sp.GetRequiredService<EfBultenRepository>(),
        sp.GetRequiredService<IMemoryCache>()));

builder.Services.AddScoped<IKullaniciRepository, EfKullaniciRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();

// --- HttpClient tabanlı servisler ---
// ÖNEMLİ: User-Agent kasıtlı olarak sıradan bir tarayıcı gibi ayarlandı.
builder.Services.AddHttpClient<ISpkScraperService, SpkScraperService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.UserAgent.ParseAdd(
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36");
    client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
    client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("tr-TR,tr;q=0.9,en-US;q=0.8,en;q=0.7");
});

builder.Services.AddHttpClient<IAiAnalysisService, GeminiApiConnector>();

// SPK Web Servisleri (ws.spk.gov.tr) — İşlem Yasaklı Kişi/Şirket sorgulama.
builder.Services.AddHttpClient<IIslemYasaklariService, IslemYasaklariService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(20);
    client.DefaultRequestHeaders.UserAgent.ParseAdd(
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36");
    client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
});

// --- Diğer Business servisleri ---
builder.Services.AddScoped<IPdfParserService, PdfParserService>();
builder.Services.AddScoped<IBultenService, BultenService>();

// --- Arka plan servisi (Sprint 1 - Use Case 1.2) ---
builder.Services.AddHostedService<SpkTakipServisi>();

// =====================================================================
// 4) API / CONTROLLER / SWAGGER
// =====================================================================
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "SPK Bülten Analiz Platformu API",
        Version = "v1",
        Description = "SPK bültenlerini özetleyen ve piyasa etkisini analiz eden Gemini destekli API."
    });

    // Swagger UI'da "Authorize" butonu ile JWT token girip korumalı uçları
    // deneyebilmek için gerekli tanım.
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "JWT token'ı 'Bearer {token}' formatında girin. Token'ı /api/auth/giris uç noktasından alabilirsiniz."
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// =====================================================================
// 1) GLOBAL EXCEPTION MIDDLEWARE — pipeline'ın EN BAŞINA eklenir.
// =====================================================================
// Neden en başa? Middleware'ler bir "soğan katmanları" gibi çalışır: bir
// istek en dıştaki middleware'den başlayıp içeri doğru ilerler, yanıt da
// içeriden dışarı doğru geri döner. GlobalExceptionMiddleware'i en dışa
// koyarsak, kendinden SONRA çalışan HER ŞEYDE (CORS, auth, controller'lar,
// hatta ileride eklenecek başka middleware'ler) fırlatılan exception'ları
// yakalayabilir. Aşağıya (örn. UseAuthentication'dan sonra) koyarsak,
// authentication/CORS aşamasında oluşan hataları asla göremeyiz.
app.UseGlobalExceptionHandling();

// Serilog'un kendi HTTP istek loglama middleware'i: her isteğin metodunu,
// yolunu, durum kodunu ve süresini TEK SATIRLIK, okunması kolay bir log
// olarak otomatik üretir (örn. "GET /api/bultenler responded 200 in 45ms").
// Bunu manuel olarak her controller'a yazmaya gerek kalmaz.
app.UseSerilogRequestLogging();

// =====================================================================
// REVERSE PROXY DESTEĞİ — Nginx/Caddy/IIS gibi bir ters proxy arkasında
// barındırılacaksa (tipik hosting senaryosu), gelen isteklerin gerçek
// şema (http/https) ve istemci IP bilgisini X-Forwarded-* header'larından
// okuması için gereklidir. Bu olmadan UseHttpsRedirection / şema kontrolü
// yanlış çalışabilir ve sonsuz yönlendirme döngüsüne girebilir.
// =====================================================================
app.UseForwardedHeaders(new Microsoft.AspNetCore.HttpOverrides.ForwardedHeadersOptions
{
    ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
        | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
});

if (izinliOriginler.Length == 0)
{
    app.Logger.LogWarning(
        "'Cors:AllowedOrigins' tanımlanmamış — API, wwwroot dışından (farklı bir origin'den) hiçbir " +
        "isteğe izin vermeyecek. Dashboard'u ayrı bir origin'den (örn. farklı bir port/domain) " +
        "yayınlıyorsanız appsettings.json içine 'Cors:AllowedOrigins' dizisini ekleyin.");
}

// =====================================================================
// 5) VERİTABANI MIGRATION + İLK KURULUM ADMIN KULLANICISI
// =====================================================================
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BultenDbContext>();
    db.Database.Migrate();

    // İlk kurulumda hiç kullanıcı yoksa, rastgele güçlü bir şifreyle bir
    // "admin" kullanıcısı oluşturulur ve şifre SADECE konsola (bir kereliğine)
    // yazdırılır. Bu, projeyi klonlayan/indiren herkesin aynı sabit şifreyle
    // (örn. "admin123") sisteme girebilmesi gibi ciddi bir güvenlik açığını
    // önler — her kurulum kendine özgü, tahmin edilemez bir ilk şifreye sahip olur.
    if (!db.Kullanicilar.Any())
    {
        var rastgeleSifre = SifreHashService.RastgeleSifreUret();
        var adminKullanici = new Kullanici
        {
            KullaniciAdi = "admin",
            SifreHash = SifreHashService.Hashle(rastgeleSifre),
            Rol = "Admin"
        };
        db.Kullanicilar.Add(adminKullanici);
        db.SaveChanges();

        var ayrac = new string('=', 70);
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(ayrac);
        Console.WriteLine("İLK KURULUM — ADMIN KULLANICISI OLUŞTURULDU");
        Console.WriteLine(ayrac);
        Console.WriteLine($"  Kullanıcı Adı : admin");
        Console.WriteLine($"  Şifre         : {rastgeleSifre}");
        Console.WriteLine("  Bu şifreyi şimdi not alın! Bir daha bu şekilde gösterilmeyecektir.");
        Console.WriteLine("  Giriş yaptıktan sonra admin panelinden şifrenizi değiştirmeniz önerilir.");
        Console.WriteLine(ayrac);
        Console.ResetColor();
    }
}

// =====================================================================
// 6) GELİŞTİRME ORTAMI: SWAGGER
// =====================================================================
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "SPK Bülten Analiz API v1");
    });
}

app.UseRouting();
app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles(); // wwwroot/index.html -> Dashboard

if (!app.Environment.IsDevelopment())
{
    // HSTS: tarayıcılara "bu siteye bir daha sadece HTTPS ile bağlan" der.
    // Sadece prod'da açılır; localhost geliştirmede gereksizdir.
    app.UseHsts();
}

app.UseHttpsRedirection();

// ÖNEMLİ: UseAuthentication MUTLAKA UseAuthorization'dan ÖNCE gelmelidir.
// Authentication = "Sen kimsin?" (JWT'yi okuyup kimliği belirler)
// Authorization  = "Bu işlemi yapmaya yetkin var mı?" ([Authorize] kontrolü)
// Sıra ters olursa [Authorize] her zaman "kimliksiz" görür ve 401 döner.
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Basit sağlık kontrolü — hosting sağlayıcısının (örn. Docker, bir
// yük dengeleyici veya uptime izleme aracı) uygulamanın ayakta olup
// olmadığını anlaması için. Kimlik doğrulama gerektirmez.
app.MapGet("/health", () => Results.Ok(new { durum = "sağlıklı", zaman = DateTime.UtcNow }))
    .AllowAnonymous();

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    // HostAbortedException, `dotnet ef migrations` gibi tasarım zamanı (design-time)
    // araçlarının uygulamayı kasıtlı olarak erken durdurmasıyla oluşur — gerçek bir
    // hata değildir, bu yüzden ayrıca loglanmaz (aksi halde her migration komutunda
    // sahte bir "Fatal" log kaydı oluşurdu).
    Log.Fatal(ex, "Uygulama beklenmeyen bir şekilde başlatılamadı / çöktü.");
}
finally
{
    // Serilog dosya/DB sink'leri arka planda tamponlu (buffered) yazar. Uygulama
    // kapanırken bu tamponun diske/DB'ye kesin olarak boşaltıldığından (flush)
    // emin olmazsak, en son birkaç log satırı kaybolabilir — özellikle de
    // uygulamanın çökme anındaki son (ve en kritik) loglar.
    Log.CloseAndFlush();
}