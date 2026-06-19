using System.Globalization;
using System.Linq;
using System.Text;
using UglyToad.PdfPig;

namespace Zahy.Finance;

internal static class FinancePdfTestTextExtractor
{
    public static string ExtractText(byte[] pdfBytes)
    {
        using var document = PdfDocument.Open(pdfBytes);
        var builder = new StringBuilder();

        foreach (var page in document.GetPages())
        {
            builder.AppendLine(string.Join(' ', page.GetWords().Select(word => word.Text)));
        }

        return NormalizePdfText(builder.ToString());
    }

    private static string NormalizePdfText(string text) =>
        text
            .Normalize(NormalizationForm.FormKC)
            .Replace("\uFB00", "ff", StringComparison.Ordinal)
            .Replace("\uFB01", "fi", StringComparison.Ordinal)
            .Replace("\uFB02", "fl", StringComparison.Ordinal);
}
