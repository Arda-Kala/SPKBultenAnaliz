# Katkıda Bulunma Rehberi

Pull request göndermeden önce lütfen şunları kontrol edin:

## ✅ Kontrol Listesi

- [ ] API anahtarı, şifre veya connection string içermiyor
- [ ] `appsettings.json` değiştirilmemiş — gizli değerler User Secrets ile eklendi
- [ ] `bin/`, `obj/`, `logs/`, `.vs/` klasörleri commit edilmedi
- [ ] `dotnet build` hatasız tamamlandı
- [ ] `dotnet test` tüm testler geçti

## 🔒 Güvenlik Kuralı

`appsettings.json` dosyasına **asla** gerçek değer yazmayın. Bunun yerine:

```bash
dotnet user-secrets set "Anahtar" "Deger" --project SPKBultenAnaliz.API
```
