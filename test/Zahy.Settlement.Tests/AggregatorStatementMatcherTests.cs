using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace Zahy.Settlement;

/// <summary>
/// Statement ↔ reflected-ledger matching. Zahy is PRINCIPAL: the statement fee is our BUY side
/// (compared to the snapshot buy leg), gross is ReflectionOnly merchant sales (compared to the
/// reflected order amount). The net tie-out reuses SettlementFourWaySplit.CodNetTransferred —
/// no new money math in this gate.
/// </summary>
public class AggregatorStatementMatcherTests
{
    private static readonly Guid Partner = Guid.NewGuid();
    private static readonly DateTime From = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime To = new(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime OrderDay = new(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc);

    private static AggregatorStatement Statement(decimal declaredNet, int lineCount = 5) =>
        AggregatorStatement.Import(
            Guid.NewGuid(), Partner, "Jahez", From, To, "hash-" + Guid.NewGuid().ToString("N"),
            declaredGross: 0m, declaredFees: 0m, declaredNet: declaredNet,
            currency: "SAR", importedBy: "importer", importedAt: OrderDay, lineCount: lineCount);

    private static AggregatorStatementLine Line(string reference, decimal gross, decimal fee) =>
        AggregatorStatementLine.Create(Guid.NewGuid(), Guid.NewGuid(), reference, OrderDay, gross, fee, gross - fee);

    private static ReflectedOrderMatchView View(string reference, decimal gross, decimal buyFee) =>
        new(reference, OrderDay, Money.Of(gross, "SAR", vatInclusive: true), Money.Of(buyFee, "SAR", vatInclusive: true));

    private static readonly (string Ref, decimal Gross, decimal Fee)[] CleanRows =
    {
        ("ORD-1001", 113.00m, 10.00m),
        ("ORD-1002", 226.00m, 20.00m),
        ("ORD-1003", 56.50m, 5.00m),
        ("ORD-1004", 79.10m, 7.00m),
        ("ORD-1005", 90.40m, 8.00m),
    };

    private static List<AggregatorStatementLine> CleanLines() =>
        CleanRows.Select(r => Line(r.Ref, r.Gross, r.Fee)).ToList();

    private static List<ReflectedOrderMatchView> CleanViews() =>
        CleanRows.Select(r => View(r.Ref, r.Gross, r.Fee)).ToList();

    private static decimal CleanNet() =>
        SettlementMoney.Round(CleanRows.Sum(r => SettlementFourWaySplit.CodNetTransferred(
            Money.Of(r.Gross, "SAR"), Money.Of(r.Fee, "SAR"))));

    [Fact]
    public void Happy_Path_All_Lines_Match_And_Net_Ties_Through_CodNetTransferred()
    {
        var lines = CleanLines();
        var outcome = AggregatorStatementMatcher.Match(Statement(CleanNet()), lines, CleanViews());

        outcome.HasVariances.ShouldBeFalse();
        outcome.MatchedLineIds.Count.ShouldBe(5);
        outcome.MatchedLineIds.ShouldBe(lines.Select(l => l.Id).ToList(), ignoreOrder: true);
        outcome.ComputedNetTransferred.ShouldBe(515.00m); // Σ (gross − fee) via the existing seam
    }

    [Fact]
    public void Statement_Line_With_No_Reflected_Order_Is_MissingInLedger()
    {
        var lines = CleanLines();
        lines.Add(Line("ORD-2001", 45.20m, 4.00m));

        var outcome = AggregatorStatementMatcher.Match(
            Statement(SettlementMoney.Round(CleanNet() + 41.20m)), lines, CleanViews());

        var variance = outcome.Variances.ShouldHaveSingleItem();
        variance.Type.ShouldBe(AggregatorVarianceType.MissingInLedger);
        variance.ExternalOrderRef.ShouldBe("ORD-2001");
        outcome.MatchedLineIds.Count.ShouldBe(5);
    }

    [Fact]
    public void Reflected_Order_Absent_From_Statement_Is_MissingInStatement()
    {
        var views = CleanViews();
        views.Add(View("ORD-9999", 88.00m, 8.00m));

        var outcome = AggregatorStatementMatcher.Match(Statement(CleanNet()), CleanLines(), views);

        var variance = outcome.Variances.ShouldHaveSingleItem();
        variance.Type.ShouldBe(AggregatorVarianceType.MissingInStatement);
        variance.StatementLineId.ShouldBeNull();
        variance.ExpectedAmount.ShouldBe(88.00m);
    }

    [Fact]
    public void Gross_Differing_Beyond_Tolerance_Is_AmountMismatch()
    {
        var lines = CleanLines();
        lines.Add(Line("ORD-2002", 100.00m, 9.00m)); // statement says 100.00
        var views = CleanViews();
        views.Add(View("ORD-2002", 98.00m, 9.00m)); // ledger says 98.00 — off by 2.00

        var outcome = AggregatorStatementMatcher.Match(
            Statement(SettlementMoney.Round(CleanNet() + 91.00m)), lines, views);

        var variance = outcome.Variances.ShouldHaveSingleItem();
        variance.Type.ShouldBe(AggregatorVarianceType.AmountMismatch);
        variance.ExpectedAmount.ShouldBe(98.00m);
        variance.ActualAmount.ShouldBe(100.00m);
    }

    [Fact]
    public void Fee_Differing_From_Snapshot_Buy_Is_FeeVsBuySnapshotMismatch()
    {
        var lines = CleanLines();
        lines.Add(Line("ORD-2003", 67.80m, 6.00m)); // statement fee 6.00
        var views = CleanViews();
        views.Add(View("ORD-2003", 67.80m, 5.00m)); // snapshot BUY leg 5.00 — off by 1.00

        var outcome = AggregatorStatementMatcher.Match(
            Statement(SettlementMoney.Round(CleanNet() + 61.80m)), lines, views);

        var variance = outcome.Variances.ShouldHaveSingleItem();
        variance.Type.ShouldBe(AggregatorVarianceType.FeeVsBuySnapshotMismatch);
        variance.ExpectedAmount.ShouldBe(5.00m); // OUR buy side — never a commission we earn
        variance.ActualAmount.ShouldBe(6.00m);
    }

    [Fact]
    public void Same_Ref_Twice_Is_DuplicateLine_And_Second_Occurrence_Is_Excluded()
    {
        var lines = CleanLines();
        lines.Add(Line("ORD-1001", 113.00m, 10.00m)); // duplicate of the first clean row

        var outcome = AggregatorStatementMatcher.Match(Statement(CleanNet()), lines, CleanViews());

        var variance = outcome.Variances.ShouldHaveSingleItem();
        variance.Type.ShouldBe(AggregatorVarianceType.DuplicateLine);
        outcome.MatchedLineIds.Count.ShouldBe(5); // first occurrence still matches
        outcome.ComputedNetTransferred.ShouldBe(515.00m); // duplicate excluded from the tie-out
    }

    [Fact]
    public void Declared_Net_Breaking_The_Invariant_Is_NetTransferMismatch()
    {
        var outcome = AggregatorStatementMatcher.Match(
            Statement(SettlementMoney.Round(CleanNet() + 5.00m)), CleanLines(), CleanViews());

        var variance = outcome.Variances.ShouldHaveSingleItem();
        variance.Type.ShouldBe(AggregatorVarianceType.NetTransferMismatch);
        variance.StatementLineId.ShouldBeNull(); // statement-level
        variance.ExpectedAmount.ShouldBe(515.00m); // computed via CodNetTransferred
        variance.ActualAmount.ShouldBe(520.00m);
    }

    [Fact]
    public void Tolerance_Boundary_001_Matches_And_002_Mismatches()
    {
        SettlementAggregatorStatementConsts.AmountTolerance.ShouldBe(0.01m); // the single source

        // diff exactly 0.01 — matches.
        var okLines = new List<AggregatorStatementLine> { Line("ORD-T1", 100.01m, 9.00m) };
        var okViews = new List<ReflectedOrderMatchView> { View("ORD-T1", 100.00m, 9.00m) };
        var ok = AggregatorStatementMatcher.Match(Statement(91.01m, 1), okLines, okViews);
        ok.HasVariances.ShouldBeFalse();
        ok.MatchedLineIds.ShouldHaveSingleItem();

        // diff 0.02 — AmountMismatch.
        var badLines = new List<AggregatorStatementLine> { Line("ORD-T2", 100.02m, 9.00m) };
        var badViews = new List<ReflectedOrderMatchView> { View("ORD-T2", 100.00m, 9.00m) };
        var bad = AggregatorStatementMatcher.Match(Statement(91.02m, 1), badLines, badViews);
        bad.Variances.ShouldHaveSingleItem().Type.ShouldBe(AggregatorVarianceType.AmountMismatch);
    }

    [Fact]
    public void Matching_Mutates_Nothing_It_Reads()
    {
        var lines = CleanLines();
        lines.Add(Line("ORD-2002", 100.00m, 9.00m));
        var views = CleanViews();
        views.Add(View("ORD-2002", 98.00m, 9.00m));

        var linesBefore = JsonSerializer.Serialize(lines.Select(l => new { l.Id, l.ExternalOrderRef, l.Gross, l.AggregatorFee, l.Net }));
        var viewsBefore = JsonSerializer.Serialize(views);

        AggregatorStatementMatcher.Match(Statement(SettlementMoney.Round(CleanNet() + 91.00m)), lines, views);

        JsonSerializer.Serialize(lines.Select(l => new { l.Id, l.ExternalOrderRef, l.Gross, l.AggregatorFee, l.Net }))
            .ShouldBe(linesBefore);
        JsonSerializer.Serialize(views).ShouldBe(viewsBefore); // reflected views byte-identical
    }
}
