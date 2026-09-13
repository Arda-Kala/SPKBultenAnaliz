using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SPKBultenAnaliz.Core.DTOs
{
    // ------------------------------------------------------------------
    // Bu dosya, Google Gemini API'nin "generateContent" endpoint'i için
    // istek/yanıt modellerini ve bizim uygulama içi Data Contract'ımızı
    // (5.2 numaralı bölümdeki JSON şeması) temsil eder.
    // ------------------------------------------------------------------

    /// <summary>Gemini API'ye gönderilecek istek gövdesi.</summary>
    public class GeminiRequest
    {
        [JsonPropertyName("systemInstruction")]
        public GeminiSystemInstruction? SystemInstruction { get; set; }

        [JsonPropertyName("contents")]
        public List<GeminiContent> Contents { get; set; } = new();

        [JsonPropertyName("generationConfig")]
        public GeminiGenerationConfig? GenerationConfig { get; set; }
    }

    public class GeminiSystemInstruction
    {
        [JsonPropertyName("parts")]
        public List<GeminiPart> Parts { get; set; } = new();
    }

    public class GeminiContent
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = "user";

        [JsonPropertyName("parts")]
        public List<GeminiPart> Parts { get; set; } = new();
    }

    public class GeminiPart
    {
        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;
    }

    /// <summary>
    /// generationConfig içindeki responseSchema alanı sayesinde Gemini'yi,
    /// tanımladığımız JSON şemasına birebir uymaya zorlarız (bkz. 5.1 Öğrenme Notu).
    /// </summary>
    public class GeminiGenerationConfig
    {
        [JsonPropertyName("responseMimeType")]
        public string ResponseMimeType { get; set; } = "application/json";

        [JsonPropertyName("responseSchema")]
        public object? ResponseSchema { get; set; }

        [JsonPropertyName("temperature")]
        public double Temperature { get; set; } = 0.2;
    }

    /// <summary>Gemini API'den dönen ham yanıt zarfı.</summary>
    public class GeminiResponse
    {
        [JsonPropertyName("candidates")]
        public List<GeminiCandidate>? Candidates { get; set; }
    }

    public class GeminiCandidate
    {
        [JsonPropertyName("content")]
        public GeminiContent? Content { get; set; }

        [JsonPropertyName("finishReason")]
        public string? FinishReason { get; set; }
    }

    /// <summary>
    /// 5.2 numaralı bölümdeki Veri Sözleşmesi (Data Contract) — Gemini'nin
    /// candidates[0].content.parts[0].text alanından JSON.Deserialize edilecek
    /// asıl analiz sonucu modeli.
    /// </summary>
    public class GeminiAnalizSonucu
    {
        [JsonPropertyName("bultenGenelOzet")]
        public string BultenGenelOzet { get; set; } = string.Empty;

        [JsonPropertyName("analizSonuclari")]
        public List<GeminiEtkiUnsuru> AnalizSonuclari { get; set; } = new();
    }

    public class GeminiEtkiUnsuru
    {
        [JsonPropertyName("etkilenenUnsur")]
        public string EtkilenenUnsur { get; set; } = string.Empty;

        [JsonPropertyName("sektor")]
        public string Sektor { get; set; } = string.Empty;

        [JsonPropertyName("duyguDurumu")]
        public string DuyguDurumu { get; set; } = string.Empty;

        [JsonPropertyName("etkiSkoru")]
        public int EtkiSkoru { get; set; }

        [JsonPropertyName("vade")]
        public string Vade { get; set; } = string.Empty;

        [JsonPropertyName("hisseKodu")]
        public string HisseKodu { get; set; } = string.Empty;

        [JsonPropertyName("aiGerekceYorumu")]
        public string AiGerekceYorumu { get; set; } = string.Empty;
    }
}