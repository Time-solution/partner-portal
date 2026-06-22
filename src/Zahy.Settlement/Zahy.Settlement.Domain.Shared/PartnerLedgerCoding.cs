using System.Collections.Generic;
using System.Globalization;
using Volo.Abp;

namespace Zahy.Settlement;

/// <summary>
/// Pure coding rules for the per-partner ledger sub-accounts — the partner analogue of Track B's
/// <see cref="BankLedgerCoding"/>.
///
/// The canonical chart keeps 2100 "AP-Partner" and 1250 "AR-Partner" as PARENT roll-ups and NEVER
/// mutates them. Each partner active in settlement gets:
///   • a PAYABLE sub-account 2101–2149 beneath the 2100 parent (Liability / Credit-normal), and
///   • a RECEIVABLE sub-account 1251–1299 beneath the 1250 parent (Asset / Debit-normal).
///
/// These sub-codes are dynamic registry data — validated for posting via
/// <see cref="SettlementAccountCode.IsPostable"/> but NOT seeded into the production chart (the live
/// chart change stays gated, same governance as the bank sub-accounts and 1250 itself).
/// </summary>
public static class PartnerLedgerCoding
{
    /// <summary>The 2100 parent the payable sub-accounts roll up to.</summary>
    public const string PayableParentCode = SettlementAccountCode.ApPartner;

    /// <summary>The 1250 parent the receivable sub-accounts roll up to.</summary>
    public const string ReceivableParentCode = SettlementAccountCode.ArPartner;

    public const int FirstPayableSubCode = 2101;
    public const int LastPayableSubCode = 2149;   // stays below 2200 Output VAT

    public const int FirstReceivableSubCode = 1251;
    public const int LastReceivableSubCode = 1299; // stays below 1300 Input VAT

    /// <summary>True for a reserved partner PAYABLE sub-account (2101–2149) beneath the 2100 parent.</summary>
    public static bool IsPayableSubAccount(string? code) =>
        InRange(code, FirstPayableSubCode, LastPayableSubCode);

    /// <summary>True for a reserved partner RECEIVABLE sub-account (1251–1299) beneath the 1250 parent.</summary>
    public static bool IsReceivableSubAccount(string? code) =>
        InRange(code, FirstReceivableSubCode, LastReceivableSubCode);

    /// <summary>True for either a partner payable or receivable sub-account.</summary>
    public static bool IsPartnerSubAccount(string? code) =>
        IsPayableSubAccount(code) || IsReceivableSubAccount(code);

    /// <summary>The next free payable sub-code (2101, 2102, …). Lowest free slot first (gaps reused).</summary>
    public static string NextPayableCode(IEnumerable<string> existingCodes) =>
        NextInRange(existingCodes, FirstPayableSubCode, LastPayableSubCode);

    /// <summary>The next free receivable sub-code (1251, 1252, …). Lowest free slot first (gaps reused).</summary>
    public static string NextReceivableCode(IEnumerable<string> existingCodes) =>
        NextInRange(existingCodes, FirstReceivableSubCode, LastReceivableSubCode);

    private static bool InRange(string? code, int first, int last) =>
        code is { Length: 4 }
        && int.TryParse(code, NumberStyles.None, CultureInfo.InvariantCulture, out var n)
        && n >= first
        && n <= last;

    private static string NextInRange(IEnumerable<string> existingCodes, int first, int last)
    {
        var used = new HashSet<int>();
        foreach (var code in existingCodes)
        {
            if (int.TryParse(code, NumberStyles.None, CultureInfo.InvariantCulture, out var n))
            {
                used.Add(n);
            }
        }

        for (var n = first; n <= last; n++)
        {
            if (!used.Contains(n))
            {
                return n.ToString(CultureInfo.InvariantCulture);
            }
        }

        throw new BusinessException(SettlementPartnerLedgerErrorCodes.SubAccountRangeExhausted)
            .WithData("First", first)
            .WithData("Last", last);
    }
}

/// <summary>Error codes for the per-partner ledger registry. Continues the Settlement series.</summary>
public static class SettlementPartnerLedgerErrorCodes
{
    public const string SubAccountRangeExhausted = "Zahy.Settlement:060";
    public const string EmptyPartnerName = "Zahy.Settlement:061";
}
