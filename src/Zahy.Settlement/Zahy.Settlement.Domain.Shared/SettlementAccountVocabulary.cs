using System.Collections.Generic;
using System.Linq;
using Volo.Abp;

namespace Zahy.Settlement;

/// <summary>
/// F12 — THE single pairing table for the three parallel chart-of-accounts vocabularies that exist in
/// the platform:
///   • numeric chart codes  — <see cref="SettlementAccountCode"/> (11, the authoritative posting codes)
///   • the legacy enum      — <see cref="SettlementAccountType"/> (8, a caller-friendly subset)
///   • the frontend names   — <c>ReportAccount</c> in <c>lib/reports/settlementReports.ts</c> (name-based)
///
/// The parallel types are intentionally KEPT (callers still use them); this file is the one place their
/// correspondence is defined so the representations cannot silently drift. The existing
/// <see cref="SettlementAccountTypeCodeMap"/> stays the Type→Code authority and is reused here as the
/// source for the Code→Type direction.
/// </summary>
public static class SettlementAccountVocabulary
{
    /// <summary>
    /// The frontend report-account names (mirror of <c>ReportAccount</c> in settlementReports.ts). The 8
    /// codes that have a frontend report line use that exact name; the 3 backend-only control/clearing
    /// codes (1250, 2300, 2400) carry their own stable names so every code round-trips.
    /// </summary>
    public static class ReportNames
    {
        public const string Cash = "Cash";                               // 1100
        public const string MerchantReceivable = "MerchantReceivable";   // 1200
        public const string PartnerReceivable = "PartnerReceivable";     // 1250 (backend-only)
        public const string VatInput = "VatInput";                       // 1300
        public const string PartnerPayable = "PartnerPayable";           // 2100
        public const string VatOutput = "VatOutput";                     // 2200
        public const string VatControl = "VatControl";                   // 2300 (backend-only)
        public const string ReflectionClearing = "ReflectionClearing";   // 2400 (backend-only)
        public const string RevenueNetSell = "RevenueNetSell";           // 4100
        public const string FeeRevenue = "FeeRevenue";                   // 4200
        public const string PartnerCost = "PartnerCost";                 // 5100
    }

    private static readonly IReadOnlyDictionary<string, string> CodeToReportNameMap =
        new Dictionary<string, string>
        {
            [SettlementAccountCode.BankCashClearing] = ReportNames.Cash,
            [SettlementAccountCode.ArMerchant] = ReportNames.MerchantReceivable,
            [SettlementAccountCode.ArPartner] = ReportNames.PartnerReceivable,
            [SettlementAccountCode.InputVat] = ReportNames.VatInput,
            [SettlementAccountCode.ApPartner] = ReportNames.PartnerPayable,
            [SettlementAccountCode.OutputVat] = ReportNames.VatOutput,
            [SettlementAccountCode.VatControl] = ReportNames.VatControl,
            [SettlementAccountCode.ReflectionClearing] = ReportNames.ReflectionClearing,
            [SettlementAccountCode.ResaleRevenue] = ReportNames.RevenueNetSell,
            [SettlementAccountCode.FeeRevenue] = ReportNames.FeeRevenue,
            [SettlementAccountCode.PartnerCogs] = ReportNames.PartnerCost
        };

    private static readonly IReadOnlyDictionary<string, string> ReportNameToCodeMap =
        CodeToReportNameMap.ToDictionary(kv => kv.Value, kv => kv.Key);

    // Code → Type is the inverse of the existing Type→Code authority; only the 8 type-bearing codes appear.
    private static readonly IReadOnlyDictionary<string, SettlementAccountType> CodeToTypeMap =
        SettlementAccountTypeCodeMap.All.ToDictionary(kv => kv.Value, kv => kv.Key);

    /// <summary>Code → frontend report-account name (all 11 canonical codes).</summary>
    public static IReadOnlyDictionary<string, string> CodeToReportName => CodeToReportNameMap;

    /// <summary>Frontend report-account name → code (all 11).</summary>
    public static IReadOnlyDictionary<string, string> ReportNameToCode => ReportNameToCodeMap;

    /// <summary>Code → legacy enum (the 8 type-bearing codes; control/clearing codes have no enum).</summary>
    public static IReadOnlyDictionary<string, SettlementAccountType> CodeToType => CodeToTypeMap;

    /// <summary>Legacy enum → code (reuses <see cref="SettlementAccountTypeCodeMap"/>, the authority).</summary>
    public static IReadOnlyDictionary<SettlementAccountType, string> TypeToCode => SettlementAccountTypeCodeMap.All;

    public static string ReportNameOf(string code) =>
        CodeToReportNameMap.TryGetValue(code, out var name)
            ? name
            : throw new AbpException($"No report-account name mapped for settlement code '{code}'.");

    public static string CodeOfReportName(string reportName) =>
        ReportNameToCodeMap.TryGetValue(reportName, out var code)
            ? code
            : throw new AbpException($"No settlement code mapped for report-account name '{reportName}'.");

    public static SettlementAccountType TypeOf(string code) =>
        CodeToTypeMap.TryGetValue(code, out var type)
            ? type
            : throw new AbpException($"No settlement account type mapped for code '{code}'.");

    public static bool TryGetType(string code, out SettlementAccountType type) =>
        CodeToTypeMap.TryGetValue(code, out type);

    /// <summary>Legacy enum → code, delegating to the existing authority.</summary>
    public static string CodeOf(SettlementAccountType type) => SettlementAccountTypeCodeMap.CodeOf(type);
}
