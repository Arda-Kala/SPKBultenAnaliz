using System.Collections.Generic;

namespace SPKBultenAnaliz.Core.Interfaces
{
    /// <summary>
    /// PDF dokümanlarından metin ayıklayıp, Gemini'nin bağlam penceresine uygun
    /// büyüklükte anlamlı bloklara (chunk) bölen servisin sözleşmesi.
    /// </summary>
    public interface IPdfParserService
    {
        /// <summary>Ham PDF byte dizisinden düz metni çıkarır.</summary>
        string MetniAyikla(byte[] pdfIcerik);

        /// <summary>
        /// Uzun bir metni, verilen maksimum karakter sınırını aşmayan,
        /// mümkün olduğunca cümle/paragraf sınırlarına saygılı bloklara böler.
        /// </summary>
        List<string> BloklaraBol(string tamMetin, int maksimumBlokBoyutu = 4000);
    }
}
