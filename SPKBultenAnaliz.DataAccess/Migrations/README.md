# Migrations Klasörü

Bu klasör başlangıçta boştur. İlk migration'ı oluşturmak için proje kök
dizininde (SPKBultenAnaliz.API içinde) şu komutu çalıştırın:

```bash
dotnet ef migrations add InitialCreate --project ../SPKBultenAnaliz.DataAccess --startup-project .
dotnet ef database update --project ../SPKBultenAnaliz.DataAccess --startup-project .
```

Ayrıntılar için ana README.md dosyasındaki "Veritabanını Kurma" bölümüne bakın.
