using System;
using System.Linq;
using Shouldly;
using Xunit;

namespace Zahy.Settlement;

/// <summary>
/// F12 — the three parallel chart-of-accounts vocabularies (numeric code, legacy enum, frontend report
/// name) are bound by ONE table. These tests pin the round-trips so the representations cannot drift.
/// </summary>
public class SettlementAccountVocabularyTests
{
    [Fact]
    public void Vocabulary_RoundTrip_Code_To_Type_To_Code()
    {
        // Every type-bearing code round-trips through the legacy enum and back.
        foreach (var code in SettlementAccountVocabulary.CodeToType.Keys)
        {
            SettlementAccountVocabulary.CodeOf(SettlementAccountVocabulary.TypeOf(code)).ShouldBe(code);
        }

        // …and every enum value round-trips the other way.
        foreach (SettlementAccountType type in Enum.GetValues(typeof(SettlementAccountType)))
        {
            var code = SettlementAccountVocabulary.CodeOf(type);
            SettlementAccountVocabulary.TypeOf(code).ShouldBe(type);
        }
    }

    [Fact]
    public void Vocabulary_RoundTrip_Code_To_ReportName_To_Code()
    {
        foreach (var code in SettlementAccountCode.All)
        {
            var reportName = SettlementAccountVocabulary.ReportNameOf(code);
            SettlementAccountVocabulary.CodeOfReportName(reportName).ShouldBe(code);
        }
    }

    [Fact]
    public void Vocabulary_All_Canonical_Codes_Covered()
    {
        SettlementAccountVocabulary.CodeToReportName.Count.ShouldBe(11);
        SettlementAccountVocabulary.CodeToReportName.Keys
            .OrderBy(c => c)
            .ShouldBe(SettlementAccountCode.All.OrderBy(c => c));

        // The report-name set is injective (every code maps to a distinct name), so the reverse map is total.
        SettlementAccountVocabulary.ReportNameToCode.Count.ShouldBe(11);
    }

    [Fact]
    public void Vocabulary_Backend_Only_Codes_Have_No_Legacy_Type()
    {
        // 1250 / 2300 / 2400 are control/clearing codes with no SettlementAccountType — they map to a
        // report name but not to the legacy enum, which is expected (the enum is a caller-friendly subset).
        SettlementAccountVocabulary.CodeToType.Count.ShouldBe(8);
        SettlementAccountVocabulary.TryGetType(SettlementAccountCode.ArPartner, out _).ShouldBeFalse();
        SettlementAccountVocabulary.TryGetType(SettlementAccountCode.VatControl, out _).ShouldBeFalse();
        SettlementAccountVocabulary.TryGetType(SettlementAccountCode.ReflectionClearing, out _).ShouldBeFalse();
    }
}
