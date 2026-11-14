using DocumentFormat.OpenXml.Packaging;
using UglyToad.PdfPig;

namespace StajEvrakSistemi.Models;

public static class BelgeOkuyucu
{
    public static string MetinOku(string dosyaYolu, string uzanti)
    {
        try
        {
            if (uzanti == ".pdf")
            {
                string metin = "";
                using (var document = PdfDocument.Open(dosyaYolu))
                {
                    foreach (var page in document.GetPages())
                    {
                        metin += page.Text + " ";
                    }
                }
                return metin;
            }
            else if (uzanti == ".docx")
            {
                using (WordprocessingDocument wordDoc = WordprocessingDocument.Open(dosyaYolu, false))
                {
                    var body = wordDoc.MainDocumentPart?.Document?.Body;
                    return body?.InnerText ?? "";
                }
            }
        }
        catch
        {
            // Okuma sırasında hata olursa boş döneriz, sistem çökmez.
            return "";
        }

        return "";
    }
}