using System.Linq;

namespace Zahy.PartnerPlatform.Partners;

/// <summary>Validates Saudi Arabia IBAN format (24 chars: SA + 22 digits).</summary>
public static class SaudiIbanValidator
{
    public static bool IsValidOrEmpty(string? iban)
    {
        if (string.IsNullOrWhiteSpace(iban))
        {
            return true;
        }

        return IsValid(iban);
    }

    public static bool IsValid(string iban)
    {
        var normalized = Normalize(iban);
        if (normalized.Length != 24)
        {
            return false;
        }

        if (!normalized.StartsWith("SA"))
        {
            return false;
        }

        return normalized.Substring(2).All(char.IsDigit);
    }

    public static string Normalize(string iban) =>
        iban.Replace(" ", string.Empty).ToUpperInvariant();
}
