namespace SPKBultenAnaliz.Business.Configuration
{
    /// <summary>
    /// appsettings.json / user-secrets içindeki "Gemini" bölümüne karşılık gelen
    /// güçlü tipli (strongly-typed) ayar sınıfı.
    ///
    /// ÖĞRENME NOTU: ApiKey değeri BURAYA hardcode edilmez. Geliştirme ortamında
    ///   dotnet user-secrets set "Gemini:ApiKey" "AIza..."
    /// komutuyla ayarlanır; üretimde ortam değişkeni veya Key Vault kullanılır.
    /// </summary>
    public class GeminiOptions
    {
        public const string SectionName = "Gemini";

        public string ApiKey { get; set; } = string.Empty;

        /// <summary>
        /// Kullanılacak model adı. Temmuz 2026 itibarıyla Gemini 3 ailesi "preview"
        /// aşamasındadır ve resmi model kimlikleri "-preview" ekiyle biter
        /// (örn. "gemini-3-flash-preview", "gemini-3-pro-preview"). Google bu modelleri
        /// kararlı (stable) sürüme geçirdiğinde bu son eki kaldırmanız gerekebilir —
        /// güncel model listesini https://ai.google.dev/gemini-api/docs/models adresinden
        /// veya "GET /v1beta/models?key=..." çağrısıyla doğrulayabilirsiniz.
        /// </summary>
        public string Model { get; set; } = "gemini-3.5-flash";

        public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta/models";

        /// <summary>Gemini API çağrısı için maksimum bekleme süresi (saniye).</summary>
        public int TimeoutSaniye { get; set; } = 60;

        /// <summary>429/5xx hatalarında kaç kez yeniden denenecek.</summary>
        public int MaksimumYenidenDeneme { get; set; } = 3;
    }
}