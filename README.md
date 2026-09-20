<div align="center">

# 📋 SPK Bülten Analiz Platformu

**SPK (Sermaye Piyasası Kurulu) bültenlerini otomatik indirip Google Gemini AI ile analiz eden ASP.NET Core 8 platformu**

![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet)
![EF Core](https://img.shields.io/badge/EF_Core-8.0-blue?style=flat-square)
![Gemini](https://img.shields.io/badge/Gemini-AI-orange?style=flat-square&logo=google)
![License](https://img.shields.io/badge/lisans-kişisel-gray?style=flat-square)

</div>

---

## ✨ Özellikler

- 📥 **Otomatik Bülten Tarama** — SPK sitesini tarayarak yeni PDF bültenleri çeker
- 📄 **PDF Metin Çıkarma** — Bültenleri bloklara ayırarak yapılandırılmış metne dönüştürür
- 🤖 **Gemini AI Analizi** — Her bülteni Gemini API ile özetler, piyasa etkisini değerlendirir
- ⚡ **Önbellek Katmanı** — Decorator Pattern ile tekrarlayan sorgular önbelleklenir
- 📊 **Dashboard** — Tüm analizleri görsel olarak listeleyen web arayüzü
- 🔐 **JWT Kimlik Doğrulama** — Güvenli admin girişi
- 🧪 **Birim Testleri** — xUnit + Moq ile kritik servisler test edilmiş
- 📝 **Serilog Loglama** — Console + File + MSSqlServer sink desteği

---

## 🛠️ Teknoloji Yığını

| Katman | Teknoloji |
|---|---|
| Backend | ASP.NET Core 8 Web API |
| ORM | Entity Framework Core 8 |
| Veritabanı | Microsoft SQL Server |
| Yapay Zeka | Google Gemini API |
| Önbellekleme | IMemoryCache + Decorator Pattern |
| Loglama | Serilog |
| Test | xUnit + Moq |
| Auth | JWT Bearer Token |

---

## 📁 Proje Yapısı

```
SPKBultenAnaliz.sln
├── SPKBultenAnaliz.Core          → Entity'ler, DTO'lar, Arayüzler
├── SPKBultenAnaliz.DataAccess    → EF Core DbContext, Repository'ler, Cache Decorator
├── SPKBultenAnaliz.Business      → Scraper, PDF Parser, Gemini Connector, Orkestrasyon
├── SPKBultenAnaliz.API           → Web API, Controller, Dashboard (wwwroot)
└── SPKBultenAnaliz.Tests         → xUnit + Moq birim testleri
```

> Bağımlılık yönü: **API → Business → DataAccess → Core** (asla tersi değil)

---

## ⚙️ Kurulum

### Gereksinimler

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [SQL Server Express](https://www.microsoft.com/tr-tr/sql-server/sql-server-downloads) (ücretsiz)
- [Google Gemini API Anahtarı](https://aistudio.google.com/app/apikey) (ücretsiz)

---

### 1. Repoyu klonla

```bash
git clone https://github.com/KULLANICI_ADIN/SPKBultenAnaliz.git
cd SPKBultenAnaliz/SPK_Bulten_Analiz
```

---

### 2. EF Core CLI yükle

```bash
dotnet tool install --global dotnet-ef
```

---

### 3. Gizli değerleri ayarla

Bu proje gizli değerleri `appsettings.json` yerine **User Secrets** ile yönetir — hiçbir anahtar git'e gitmez.

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
  "Server=(local);Database=SPKBultenDb;Trusted_Connection=True;TrustServerCertificate=True;" \
  --project SPKBultenAnaliz.API

dotnet user-secrets set "Gemini:ApiKey" "GEMINI_API_ANAHTARIN" \
  --project SPKBultenAnaliz.API

dotnet user-secrets set "Jwt:Key" "EnAz32KarakterGucluBirSifreGir2026!" \
  --project SPKBultenAnaliz.API
```

> 💡 Gemini API anahtarı almak için → [aistudio.google.com/app/apikey](https://aistudio.google.com/app/apikey)

---

### 4. Veritabanını oluştur

```bash
dotnet ef database update \
  --project SPKBultenAnaliz.DataAccess \
  --startup-project SPKBultenAnaliz.API
```

Başarılı olursa `Bultenler`, `BultenMetinBloklari`, `BultenAnalizSonuclari` tabloları otomatik oluşur.

---

### 5. Çalıştır

```bash
dotnet run --project SPKBultenAnaliz.API
```

| Adres | Açıklama |
|---|---|
| `http://localhost:5080` | Dashboard |
| `http://localhost:5080/swagger` | API dokümantasyonu |

---

### 6. Testleri çalıştır

```bash
dotnet test
```

---

## 🧪 Temel API Endpoint'leri

| Method | Endpoint | Açıklama |
|---|---|---|
| `POST` | `/api/bultenler/tara` | SPK sitesini tara, yeni bültenleri çek |
| `GET` | `/api/bultenler` | Tüm bültenlerin listesi |
| `GET` | `/api/bultenler/{id}` | Tek bülten detayı |
| `POST` | `/api/bultenler/{id}/analiz` | Başarısız analizi yeniden tetikle |

---

## 🔒 Güvenlik

- `appsettings.json` içinde **hiçbir gizli değer yoktur** — tümü boş bırakılmıştır
- Tüm sırlar geliştirmede **User Secrets**, production'da **Environment Variables** ile sağlanır
- `secrets.json`, `logs/`, `bin/`, `obj/`, `.vs/` git'e gitmez

Güvenlik açığı bildirimi için [SECURITY.md](.github/SECURITY.md) dosyasına bakın.

---

## 📄 Lisans

Bu proje kişisel / eğitim amaçlıdır.
