using System.Collections.Generic;
using System.Linq;

namespace Zahy.Settlement;

/// <summary>
/// Named handles for the real <see cref="SettlementChartOfAccounts"/> codes (Phase A). Posting
/// templates reference these — never literal strings — so a code change is caught in one place.
/// <see cref="All"/> must match the seeded chart exactly (asserted by tests).
/// </summary>
public static class SettlementAccountCode
{
    public const string BankCashClearing = "1100";
    public const string ArMerchant = "1200";
    /// <summary>Receivable FROM the partner (what the partner OWES Zahy, e.g. a billed fee) —
    /// distinct from 2100 AP-Partner (what Zahy owes the partner).</summary>
    public const string ArPartner = "1250";
    public const string InputVat = "1300";
    public const string ApPartner = "2100";
    public const string OutputVat = "2200";
    public const string VatControl = "2300";
    public const string ReflectionClearing = "2400";
    public const string ResaleRevenue = "4100";
    public const string FeeRevenue = "4200";
    public const string PartnerCogs = "5100";

    public static IReadOnlyCollection<string> All { get; } = new[]
    {
        BankCashClearing, ArMerchant, ArPartner, InputVat, ApPartner, OutputVat,
        VatControl, ReflectionClearing, ResaleRevenue, FeeRevenue, PartnerCogs
    };

    public static bool IsDefined(string code) => All.Contains(code);
}
