# SPK Bülten Özetleme ve Piyasa Etki Analiz Platformu

**Google Gemini API destekli — Öğrenme Odaklı Sürüm (v2.0.0)**

Bu proje, `SPK_Bulten_Analiz_Gemini_Ogrenme_Surumu.docx` dokümanındaki kurumsal
proje gereksinimlerine uygun olarak hazırlanmış, çalışan bir .NET 8 çözümüdür.
Sprint 1 (altyapı + chunking), Sprint 2 (Gemini entegrasyonu) ve Sprint 3
(önbellekleme + dashboard + testler) kapsamındaki tüm ana bileşenleri içerir.

---

## 📁 Proje Yapısı

```
SPKBultenAnaliz.sln
├── SPKBultenAnaliz.Core          → Entity'ler, DTO'lar, Arayüzler (Interfaces)
├── SPKBultenAnaliz.DataAccess    → EF Core DbContext, Repository'ler, Cache Decorator
├── SPKBultenAnaliz.Business      → Scraper, PDF Parser, Gemini Connector, Orkestrasyon
├── SPKBultenAnaliz.API           → Web API, Controller, Dashboard (wwwroot)
└── SPKBultenAnaliz.Tests         → xUnit + Moq birim testleri
```

Bağımlılık yönü: **API → Business → DataAccess → Core** (asla tersi değil).

---

## ✅ Ön Koşullar

1. **.NET 8 SDK** — https://dotnet.microsoft.com/download/dotnet/8.0
2. **SQL Server** (LocalDB, Express veya tam sürüm)
3. **Google Gemini API Anahtarı** — https://aistudio.google.com üzerinden ücretsiz alınabilir
4. (Opsiyonel) Visual Studio 2022 veya VS Code + C# Dev Kit eklentisi

Kurulumu doğrulayın:
```bash
dotnet --version   # 8.0.x görünmeli
```

---

## 🚀 Adım Adım Kurulum

### 1) Bağımlılıkları geri yükleyin (NuGet restore)

```bash
cd SPK_Bulten_Analiz
dotnet restore
```

### 2) Gemini API anahtarını güvenli şekilde tanımlayın

**appsettings.json içine ASLA düz metin olarak yazmayın.** Bunun yerine
User Secrets kullanın:

```bash
cd SPKBultenAnaliz.API
dotnet user-secrets init
dotnet user-secrets set "Gemini:ApiKey" "AIzaSy...buraya_kendi_anahtariniz..."
```

### 3) Veritabanı bağlantısını kontrol edin

`SPKBultenAnaliz.API/appsettings.json` içindeki `ConnectionStrings:DefaultConnection`
değerini kendi SQL Server kurulumunuza göre düzenleyin. Varsayılan değer LocalDB
içindir ve çoğu Visual Studio kurulumunda değişiklik gerektirmez:

```json
"DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=SPKBultenDb;Trusted_Connection=true;TrustServerCertificate=true;"
```

### 4) EF Core aracını kurun (bir kereye mahsus)

```bash
dotnet tool install --global dotnet-ef
```

### 5) İlk Migration'ı oluşturun ve veritabanını kurun

```bash
cd SPKBultenAnaliz.API
dotnet ef migrations add InitialCreate --project ../SPKBultenAnaliz.DataAccess --startup-project .
dotnet ef database update --project ../SPKBultenAnaliz.DataAccess --startup-project .
```

Bu komut sonunda `Bultenler`, `BultenMetinBloklari`, `BultenAnalizSonuclari`
tablolarının oluştuğunu SQL Server Management Studio veya Azure Data Studio
üzerinden görebilirsiniz.

> **Not:** `Program.cs` içinde geliştirme ortamında `db.Database.Migrate()`
> otomatik çalıştığı için, migration'ı bir kez oluşturduktan sonra `dotnet run`
> her seferinde veritabanını güncel tutar.

### 6) Projeyi çalıştırın

```bash
dotnet run --project SPKBultenAnaliz.API
```

Tarayıcı otomatik olarak Swagger arayüzünü (`/swagger`) açacaktır. Dashboard'u
görmek için `http://localhost:5080/` adresine gidin (port farklıysa konsol
çıktısındaki adresi kullanın).

### 7) Testleri çalıştırın

```bash
dotnet test
```

---

## 🧪 Uygulamayı Deneme Senaryosu

1. Swagger'dan (veya Dashboard'daki "Yeni Bültenleri Tara" butonundan)
   `POST /api/bultenler/tara` uç noktasını çağırın.
   - Bu, `SpkScraperService`'i tetikler ve SPK sitesini tarar.
   - **Önemli:** `SpkScraperService.cs` içindeki `BultenListesiUrl` ve CSS
     seçicileri örnek/varsayılan değerlerdir; gerçek SPK sitesinin güncel HTML
     yapısına göre düzenlenmesi gerekir (dosya içinde bununla ilgili bir
     Öğrenme Notu bulunmaktadır).
2. Yeni bülten tespit edilirse, PDF indirilir → metne çevrilir → bloklara
   ayrılır → Gemini API'ye gönderilir → sonuç veritabanına yazılır.
3. `GET /api/bultenler` ile tüm bültenlerin özetini, `GET /api/bultenler/{id}`
   ile tek bir bültenin detayını görebilirsiniz.
4. Bir analiz başarısız olursa (`DogrulamaBekleniyor` durumu), o bülten için
   `POST /api/bultenler/{id}/analiz` ile yeniden analiz tetikleyebilirsiniz.

---

## 🎓 Bu Projeyi Nasıl Öğrenerek İncelemeli?

Önerilen okuma sırası (Faz 1 → Faz 2 → Faz 3 mantığına uygun):

1. `SPKBultenAnaliz.Core/Entities/*.cs` — Veri modelini anlayın.
2. `SPKBultenAnaliz.Core/Interfaces/*.cs` — Her katmanın "sözleşmesini" okuyun.
3. `SPKBultenAnaliz.DataAccess/BultenDbContext.cs` — EF Core'un tabloları nasıl
   oluşturduğunu inceleyin.
4. `SPKBultenAnaliz.DataAccess/Repositories/EfBultenRepository.cs` — Somut
   veri erişim mantığı.
5. `SPKBultenAnaliz.Business/Services/PdfParserService.cs` — Chunking
   algoritmasını inceleyin, ardından `SPKBultenAnaliz.Tests/PdfParserServiceTests.cs`
   dosyasındaki testleri çalıştırıp anlayın.
6. `SPKBultenAnaliz.Business/Services/GeminiApiConnector.cs` — Harici bir API'ye
   nasıl güvenli (retry, timeout, hata yönetimi) bağlanılacağını inceleyin.
7. `SPKBultenAnaliz.Business/Services/BultenService.cs` — Tüm parçaların nasıl
   bir araya geldiğini (orkestrasyon) görün.
8. `SPKBultenAnaliz.API/Program.cs` — Dependency Injection kayıtlarının
   tümünün nasıl birbirine bağlandığını inceleyin.
9. `SPKBultenAnaliz.DataAccess/Repositories/CachedBultenRepository.cs` —
   Decorator Pattern ile önbellekleme örneğini inceleyin (Sprint 3).

Her dosyada `ÖĞRENME NOTU` veya açıklayıcı XML yorumları (`///`) bulacaksınız —
bunlar "neden böyle yazıldığını" açıklamak için bilhassa eklenmiştir.

---

## 🔧 Sırada Ne Var? (Henüz Yapılmayanlar)

Bu iskelet, dokümandaki tüm Sprint'lerin temel yapı taşlarını içerir; ancak
gerçek bir üretim sistemine dönüştürmek için şunları kendiniz tamamlamalısınız:

- `SpkScraperService.cs` içindeki CSS seçicilerini gerçek SPK sitesine göre
  güncellemek (Use Case 1.2).
- `BultenService.BulteniAnalizEtAsync` içinde, çok bloklu bültenlerde her
  bloğu ayrı ayrı analiz edip sonuçları birleştirme mantığını geliştirmek
  (şu an öğretici sadelik için bloklar birleştirilip tek istekte gönderiliyor).
- Dashboard'a tarih filtresi, sayfalama (pagination) eklemek.
- `dotnet ef migrations add InitialCreate` komutunu çalıştırarak gerçek
  migration dosyalarını üretmek (bu repo'da migration dosyaları kasıtlı
  olarak boş bırakılmıştır; sizin ortamınızda üretilmesi gerekir).

Bu adımların her biri, dokümandaki ilgili Use Case'e karşılık gelir — sırayla
ilerlemeniz önerilir.

---

## 📄 İlgili Doküman

Bu kod tabanı, `SPK_Bulten_Analiz_Gemini_Ogrenme_Surumu.docx` içindeki
mimari, veri şeması ve sprint planına birebir uygun olarak yazılmıştır.
Herhangi bir tasarım kararının "neden"ini merak ederseniz, önce ilgili
dokümandaki bölüme, sonra kod içindeki `ÖĞRENME NOTU` yorumlarına bakın.
