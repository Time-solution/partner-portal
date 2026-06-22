using System.Collections.Generic;
using System.Globalization;
using Volo.Abp;

namespace Zahy.Settlement;

/// <summary>
/// Pure coding rules for the bank registry's per-bank ledger accounts (Track B).
///
/// The canonical chart (<see cref="SettlementChartOfAccounts"/>) keeps 1100 as the PARENT
/// "Bank / Cash Clearing" roll-up and is NEVER mutated by the registry. Each registered bank gets a
/// sub-account in the reserved range 1101–1149 BENEATH 1100 (Asset / Debit-normal, like the parent).
/// These sub-codes are dynamic registry data — they are validated for posting via
/// <see cref="SettlementAccountCode.IsPostable"/> but are NOT seeded into the production chart
/// (the live chart change stays gated, same governance as 1250).
/// </summary>
public static class BankLedgerCoding
{
    /// <summary>The 1100 parent the bank sub-accounts roll up to.</summary>
    public const string ParentCode = SettlementAccountCode.BankCashClearing;

    public const int FirstSubCode = 1101;

    public const int LastSubCode = 1149;

    /// <summary>True for a reserved bank sub-account code (1101–1149) beneath the 1100 parent.</summary>
    public static bool IsBankSubAccount(string? code) =>
        code is { Length: 4 }
        && int.TryParse(code, NumberStyles.None, CultureInfo.InvariantCulture, out var n)
        && n >= FirstSubCode
        && n <= LastSubCode;

    /// <summary>
    /// The next free sub-code (1101, 1102, …) given the codes already in use. Lowest free slot first,
    /// so deactivating + re-adding reuses gaps. Throws when the reserved range is exhausted.
    /// </summary>
    public static string NextCode(IEnumerable<string> existingCodes)
    {
        var used = new HashSet<int>();
        foreach (var code in existingCodes)
        {
            if (int.TryParse(code, NumberStyles.None, CultureInfo.InvariantCulture, out var n))
            {
                used.Add(n);
            }
        }

        for (var n = FirstSubCode; n <= LastSubCode; n++)
        {
            if (!used.Contains(n))
            {
                return n.ToString(CultureInfo.InvariantCulture);
            }
        }

        throw new BusinessException(SettlementBankAccountErrorCodes.SubAccountRangeExhausted)
            .WithData("First", FirstSubCode)
            .WithData("Last", LastSubCode);
    }

    /// <summary>
    /// Mask an account number / IBAN for display — keep the last 4 characters, mask the rest.
    /// Display-only; the full value is never recomputed or altered.
    /// </summary>
    public static string Mask(string? accountNumber)
    {
        var value = accountNumber?.Trim() ?? string.Empty;
        if (value.Length <= 4)
        {
            return value;
        }

        return new string('•', value.Length - 4) + value[^4..];
    }
}

/// <summary>Error codes for the bank registry (Track B).</summary>
public static class SettlementBankAccountErrorCodes
{
    public const string SubAccountRangeExhausted = "Zahy.Settlement:040";
    public const string EmptyName = "Zahy.Settlement:041";
    public const string EmptyAccountNumber = "Zahy.Settlement:042";
}
