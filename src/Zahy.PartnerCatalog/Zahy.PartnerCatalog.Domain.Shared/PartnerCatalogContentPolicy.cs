using System.Text.RegularExpressions;
using Volo.Abp;

namespace Zahy.PartnerCatalog;

/// <summary>
/// ANTI-DISINTERMEDIATION default (ratifiable decision): merchant-facing text must not carry a
/// contact channel — URLs, emails, or phone numbers — so the deal stays on-platform. ONE shared
/// validator for every merchant-facing field (listing rows + the existing partner brief, merchant
/// benefit, and package explanation). If the policy is rejected later, this single source is the
/// one revert point.
/// </summary>
public static class PartnerCatalogContentPolicy
{
    // URLs: scheme://, www., or bare domain.tld (2+ letter TLD).
    private static readonly Regex UrlPattern = new(
        @"(https?://|www\.)\S+|(?<![\w@.])[a-zA-Z0-9-]{2,}\.(com|net|org|io|sa|co|me|app|shop|store|info|biz)(?![\w])",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex EmailPattern = new(
        @"[\w.+-]+@[\w-]+\.[\w.]{2,}",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Phones: international (+9665…), local 05XXXXXXXX, or any 8+ digit run allowing separators.
    private static readonly Regex PhonePattern = new(
        @"(\+?\d[\d\s\-().]{7,}\d)",
        RegexOptions.Compiled);

    public static bool ContainsContactChannel(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return UrlPattern.IsMatch(text) || EmailPattern.IsMatch(text) || PhonePattern.IsMatch(text);
    }

    /// <summary>Throws :056 when the merchant-facing <paramref name="text"/> carries a contact channel.</summary>
    public static void EnsureNoContactChannel(string? text, string fieldName)
    {
        if (ContainsContactChannel(text))
        {
            throw new BusinessException(PartnerCatalogListingErrorCodes.MerchantFacingContactInfoNotAllowed)
                .WithData("Field", fieldName);
        }
    }
}
