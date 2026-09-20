# Güvenlik Politikası

## Gizli Değerler

Bu projede API anahtarları, şifreler ve bağlantı bilgileri **hiçbir zaman** kaynak kodda bulunmaz. Tüm gizli değerler:

- Geliştirme ortamında: `dotnet user-secrets` ile yönetilir
- Production ortamında: Environment Variables ile sağlanır

## Güvenlik Açığı Bildirimi

Bu projede bir güvenlik açığı keşfettiyseniz lütfen doğrudan GitHub Issues açmak yerine iletişime geçin.

Kodda yanlışlıkla commit edilmiş bir API anahtarı veya şifre görürseniz lütfen bildirin — hemen iptal edilecektir.
