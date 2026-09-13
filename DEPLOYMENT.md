# Dağıtım (Deployment) Rehberi

Bu doküman, projeyi bir sunucuya/hosting'e kurarken izlenecek adımları ve
zorunlu ortam değişkenlerini özetler. GitHub Actions / CI kullanılmadığı
için tüm adımlar manuel veya kendi script'lerinle yapılacaktır.

## 1) Zorunlu Ortam Değişkenleri

Uygulama appsettings.json içinde bu değerleri **boş** bırakır; production'da
mutlaka ortam değişkeni (veya `dotnet user-secrets`, sadece geliştirmede)
ile doldurulmalıdır. `__` (çift alt çizgi) iç içe appsettings anahtarlarını temsil eder.

| Ortam Değişkeni                  | Açıklama                                                              | Örnek / Üretim komutu |
|-----------------------------------|-------------------------------------------------------------------------|------------------------|
| `ConnectionStrings__DefaultConnection` | SQL Server bağlantı dizesi                                        | `Server=...;Database=SPKBulten;User Id=...;Password=...;TrustServerCertificate=True` |
| `Jwt__Key`                        | JWT imzalama anahtarı, **en az 32 karakter**                            | `openssl rand -base64 48` |
| `Gemini__ApiKey`                  | Gemini API anahtarı                                                      | Google AI Studio'dan alınır |
| `Cors__AllowedOrigins__0`         | Dashboard'un yayınlandığı origin (birden fazlaysa `__1`, `__2` ekle)     | `https://dashboard.senin-domainin.com` |
| `ASPNETCORE_ENVIRONMENT`          | `Production` olarak ayarlanmalı                                         | `Production` |

> `Jwt__Key` veya `ConnectionStrings__DefaultConnection` boş kalırsa uygulama
> **başlamayı reddeder** (Program.cs içinde kasıtlı kontrol var) — bu bir
> hata değil, güvenlik önlemidir.

## 2) Veritabanı

- İlk açılışta `db.Database.Migrate()` otomatik çalışır, migration'ları
  kendin elle uygulamana gerek yok.
- **Tek instance ile başlat.** Aynı anda birden fazla replika/instance ile
  başlatırsan migration'lar çakışabilir. İlk kurulum ve migration'lar
  tamamlandıktan sonra ölçeklendirebilirsin.
- İlk çalıştırmada hiç kullanıcı yoksa otomatik bir `admin` kullanıcısı ve
  rastgele güçlü bir şifre oluşturulur; şifre **sadece konsol/log çıktısına**
  bir kereliğine yazdırılır. Docker ile çalıştırıyorsan:
  ```
  docker logs spk-bulten-analiz
  ```
  komutuyla görebilirsin. Bu şifreyi hemen not al ve admin panelinden değiştir.

## 3) Docker ile Dağıtım (önerilen, en basit yol)

```bash
# Repo kökünde (SPKBultenAnaliz.sln'in bulunduğu klasörde):
docker build -t spk-bulten-analiz -f SPKBultenAnaliz.API/Dockerfile .

docker run -d -p 8080:8080 \
  -e ConnectionStrings__DefaultConnection="Server=...;Database=...;User Id=...;Password=...;TrustServerCertificate=True" \
  -e Jwt__Key="$(openssl rand -base64 48)" \
  -e Gemini__ApiKey="senin-api-anahtarin" \
  -e Cors__AllowedOrigins__0="https://dashboard-adresin.com" \
  -e ASPNETCORE_ENVIRONMENT="Production" \
  --name spk-bulten-analiz spk-bulten-analiz

docker logs spk-bulten-analiz   # ilk admin şifresini görmek için
```

SQL Server'ı da container olarak çalıştırmak istersen `mcr.microsoft.com/mssql/server:2022-latest`
imajını kullanabilir, ikisini aynı Docker network'üne bağlayabilirsin.

## 4) Docker Kullanmadan Dağıtım (Linux + systemd örneği)

```bash
# Sunucuda:
dotnet publish SPKBultenAnaliz.API/SPKBultenAnaliz.API.csproj -c Release -o /var/www/spk-bulten-analiz

# Ortam değişkenlerini bir .env / systemd EnvironmentFile içine yaz, örn:
# /etc/spk-bulten-analiz.env
#   ConnectionStrings__DefaultConnection=...
#   Jwt__Key=...
#   Gemini__ApiKey=...
#   Cors__AllowedOrigins__0=https://dashboard-adresin.com
#   ASPNETCORE_ENVIRONMENT=Production
#   ASPNETCORE_URLS=http://localhost:5080

# Basit bir systemd servisi ile arka planda sürekli ayakta tut ve
# sunucu yeniden başlasa bile otomatik başlamasını sağla.
```

Windows/IIS'e kuracaksan `dotnet publish` çıktısını IIS'in "ASP.NET Core Module"
ile bir Application Pool'a bağlaman yeterli; ayrıca ortam değişkenlerini
IIS'in `web.config` / Application Pool ayarlarından tanımlaman gerekir.

## 5) Reverse Proxy (Nginx/Caddy) Arkasında Çalıştırma

Uygulama artık `UseForwardedHeaders` middleware'i içeriyor, yani bir ters
proxy'nin arkasında (TLS'i proxy sonlandırıyor, uygulamaya http ile
gidiyor) sorunsuz çalışır. Proxy tarafında sadece şu header'ların
iletildiğinden emin ol: `X-Forwarded-For`, `X-Forwarded-Proto`.

Örnek minimal Nginx bloğu:
```nginx
location / {
    proxy_pass         http://127.0.0.1:8080;
    proxy_set_header    X-Forwarded-For $remote_addr;
    proxy_set_header    X-Forwarded-Proto $scheme;
    proxy_set_header    Host $host;
}
```

## 6) Loglama ve Hata İzleme (Serilog)

Uygulama artık Serilog ile yapılandırılmış (structured) loglama kullanıyor:

- **Konsol**: Docker/systemd loglarında (`docker logs`, `journalctl`) her zaman görünür.
- **Dosya**: `logs/spk-bulten-analiz-YYYYMMDD.log` — günlük döner, varsayılan olarak
  son 30 gün saklanır. Sunucuda bu klasörü periyodik olarak yedeklemek/temizlemek
  isteyebilirsin (`retainedFileCountLimit` ile appsettings.json'dan ayarlanabilir).
- **Veritabanı**: `Error` ve üzeri seviyedeki kayıtlar, `ConnectionStrings:DefaultConnection`
  kullanılarak SQL Server'daki `Logs` tablosuna da yazılır (tablo ilk çalıştırmada
  otomatik oluşturulur). Böylece "geçen hafta hangi hatalar oldu?" sorusunu SQL ile
  sorgulayabilirsin — dosyaları tek tek açmana gerek kalmaz.
- appsettings.Development.json'da DB sink'i **kapalı** — geliştirirken gereksiz DB
  yazımı/gürültü olmasın diye. Sadece appsettings.json (Production) içinde aktif.
- Her isteğe (request) özgü bir `IzlemeId` (TraceIdentifier) hata yanıtlarına eklenir;
  kullanıcı "bir hata aldım" dediğinde bu ID ile log dosyasında/DB'de arama yaparak
  ilgili kaydı saniyeler içinde bulabilirsin.
- **GlobalExceptionMiddleware** tüm işlenmeyen (unhandled) hataları tek noktadan
  yakalar; istemciye stack trace SADECE `ASPNETCORE_ENVIRONMENT=Development` iken
  döner, Production'da asla iç sistem detayı sızdırılmaz.

## 7) Sağlık Kontrolü (Health Check)

Uygulama artık kimlik doğrulaması gerektirmeyen bir `/health` uç noktası
sunuyor. Hosting sağlayıcın (Docker healthcheck, uptime monitörü, yük
dengeleyici) bu adresi periyodik yoklayarak uygulamanın ayakta olup
olmadığını kontrol edebilir:
```
GET /health  →  { "durum": "sağlıklı", "zaman": "..." }
```

## 8) Dağıtım Öncesi Son Kontrol Listesi

- [ ] `Jwt__Key`, `ConnectionStrings__DefaultConnection`, `Gemini__ApiKey` ortam değişkenleri ayarlandı
- [ ] `ASPNETCORE_ENVIRONMENT=Production` ayarlandı (Development'ta Swagger açık kalır, prod'da kapanır)
- [ ] `Cors__AllowedOrigins__0` gerçek Dashboard adresine ayarlandı (boş bırakılırsa hiçbir origin'e izin verilmez)
- [ ] SQL Server erişilebilir ve migration'lar sorunsuz uygulandı (ilk açılış loglarından kontrol et)
- [ ] İlk admin şifresi loglardan alınıp not edildi, giriş yapıldıktan sonra değiştirildi
- [ ] Domain için gerçek bir TLS sertifikası var (Let's Encrypt / hosting sağlayıcının sertifikası)
- [ ] `/health` uç noktası dışarıdan erişilebilir ve 200 dönüyor
- [ ] Arka plan servisi (SPK tarama, 6 saatte bir) loglarda çalıştığı görülüyor
- [ ] `logs/` klasörü uygulamanın yazma izni olan bir yerde ve `Logs` tablosu SQL Server'da otomatik oluşmuş
