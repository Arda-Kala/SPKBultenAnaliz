namespace SPKBultenAnaliz.Business.Configuration
{
    /// <summary>
    /// appsettings.json içindeki "Jwt" bölümüne karşılık gelen ayar sınıfı.
    ///
    /// ÖĞRENME NOTU: "Key" alanı JWT imzalarını üretmek/doğrulamak için
    /// kullanılan gizli anahtardır. Gemini API anahtarına benzer şekilde bu
    /// da bir SIR (secret) değeridir — üretim ortamında appsettings.json'da
    /// düz metin tutulmamalı, dotnet user-secrets veya ortam değişkeni ile
    /// yönetilmelidir. En az 32 karakter (256 bit) uzunluğunda, rastgele
    /// üretilmiş bir değer olmalıdır (HMAC-SHA256 için minimum güvenli boyut).
    /// </summary>
    public class JwtOptions
    {
        public const string SectionName = "Jwt";

        public string Key { get; set; } = string.Empty;
        public string Issuer { get; set; } = "SPKBultenAnalizPlatformu";
        public string Audience { get; set; } = "SPKBultenAnalizKullanicilari";
        public int SureDakika { get; set; } = 120;
    }
}
