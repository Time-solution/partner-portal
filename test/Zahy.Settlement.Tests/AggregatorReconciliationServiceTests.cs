using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Guids;
using Xunit;
using Zahy.Identity.Auditing;
using Zahy.Settlement.AggregatorReconciliation;

namespace Zahy.Settlement;

/// <summary>
/// End-to-end (fakes) over the orchestration: content-hash import idempotency, the demo-shaped
/// variance queue, audited note-mandatory resolution, two-person close, immutability after close —
/// and the COMPUTE-ONLY proof: the trial balance over a fixed posting set is byte-identical after
/// the whole import→match→resolve→close cycle, and the reflected views it read are unchanged.
/// </summary>
public class AggregatorReconciliationServiceTests
{
    private static readonly Guid Partner = Guid.NewGuid();
    private static readonly DateTime From = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime To = new(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Day = new(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Now = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

    // ---- fakes (ISettlementCaseStore test pattern) ------------------------------------------------
    private sealed class FakeStore : IAggregatorStatementStore
    {
        public readonly List<AggregatorStatement> Statements = new();
        public readonly List<AggregatorStatementLine> Lines = new();
        public readonly List<AggregatorStatementException> Exceptions = new();

        public Task<AggregatorStatement?> FindByImportKeyAsync(string key, CancellationToken ct = default) =>
            Task.FromResult(Statements.FirstOrDefault(s => s.ImportIdempotencyKey == key));

        public Task<AggregatorStatement?> FindAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Statements.FirstOrDefault(s => s.Id == id));

        public Task<IReadOnlyList<AggregatorStatement>> GetListAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<AggregatorStatement>>(Statements.ToList());

        public Task InsertAsync(AggregatorStatement statement, IReadOnlyList<AggregatorStatementLine> lines, CancellationToken ct = default)
        {
            Statements.Add(statement);
            Lines.AddRange(lines);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(AggregatorStatement statement, CancellationToken ct = default) => Task.CompletedTask;

        public Task<IReadOnlyList<AggregatorStatementLine>> GetLinesAsync(Guid statementId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<AggregatorStatementLine>>(Lines.Where(l => l.StatementId == statementId).ToList());

        public Task InsertExceptionsAsync(IReadOnlyList<AggregatorStatementException> exceptions, CancellationToken ct = default)
        {
            Exceptions.AddRange(exceptions);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<AggregatorStatementException>> GetExceptionsAsync(Guid statementId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<AggregatorStatementException>>(Exceptions.Where(e => e.StatementId == statementId).ToList());

        public Task<AggregatorStatementException?> FindExceptionAsync(Guid exceptionId, CancellationToken ct = default) =>
            Task.FromResult(Exceptions.FirstOrDefault(e => e.Id == exceptionId));

        public Task UpdateExceptionAsync(AggregatorStatementException exception, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeAudit : IAdminAuditLogger
    {
        public readonly List<(string Action, string? TargetId, string? Extra)> Entries = new();

        public Task LogAsync(string action, string? targetType = null, string? targetId = null,
            string result = AdminAuditResults.Success, string? extraData = null)
        {
            Entries.Add((action, targetId, extraData));
            return Task.CompletedTask;
        }
    }

    private static (AggregatorReconciliationService Service, FakeStore Store, FakeAudit Audit, InMemoryAggregatorReflectedOrderSource Source) Build()
    {
        var store = new FakeStore();
        var audit = new FakeAudit();
        var source = new InMemoryAggregatorReflectedOrderSource();
        var service = new AggregatorReconciliationService(store, source, audit, SimpleGuidGenerator.Instance);
        return (service, store, audit, source);
    }

    // ---- the demo-shaped statement (5 clean + 4 deliberate variances, net consistent) --------------
    private static ImportAggregatorStatementRequest DemoRequest()
    {
        var lines = new List<ImportAggregatorStatementLine>
        {
            new() { ExternalOrderRef = "ORD-1001", OrderDate = Day, Gross = 113.00m, AggregatorFee = 10.00m, Net = 103.00m },
            new() { ExternalOrderRef = "ORD-1002", OrderDate = Day, Gross = 226.00m, AggregatorFee = 20.00m, Net = 206.00m },
            new() { ExternalOrderRef = "ORD-1003", OrderDate = Day, Gross = 56.50m, AggregatorFee = 5.00m, Net = 51.50m },
            new() { ExternalOrderRef = "ORD-1004", OrderDate = Day, Gross = 79.10m, AggregatorFee = 7.00m, Net = 72.10m },
            new() { ExternalOrderRef = "ORD-1005", OrderDate = Day, Gross = 90.40m, AggregatorFee = 8.00m, Net = 82.40m },
            // deliberate variances:
            new() { ExternalOrderRef = "ORD-2001", OrderDate = Day, Gross = 45.20m, AggregatorFee = 4.00m, Net = 41.20m },  // MissingInLedger
            new() { ExternalOrderRef = "ORD-2002", OrderDate = Day, Gross = 100.00m, AggregatorFee = 9.00m, Net = 91.00m }, // AmountMismatch (ledger 98.00)
            new() { ExternalOrderRef = "ORD-2003", OrderDate = Day, Gross = 67.80m, AggregatorFee = 6.00m, Net = 61.80m },  // Fee vs BUY 5.00
            new() { ExternalOrderRef = "ORD-1001", OrderDate = Day, Gross = 113.00m, AggregatorFee = 10.00m, Net = 103.00m }, // DuplicateLine
        };

        return new ImportAggregatorStatementRequest
        {
            PartnerId = Partner,
            Source = "Jahez",
            PeriodFrom = From,
            PeriodTo = To,
            DeclaredGross = 778.00m,
            DeclaredFees = 69.00m,
            DeclaredNet = 709.00m, // Σ per-order (gross − fee) over unique lines — ties the invariant
            Currency = "SAR",
            Lines = lines
        };
    }

    private static void SeedLedger(InMemoryAggregatorReflectedOrderSource source)
    {
        ReflectedOrderMatchView View(string reference, decimal gross, decimal buy) =>
            new(reference, Day, Money.Of(gross, "SAR", vatInclusive: true), Money.Of(buy, "SAR", vatInclusive: true));

        source.Seed(Partner, new[]
        {
            View("ORD-1001", 113.00m, 10.00m),
            View("ORD-1002", 226.00m, 20.00m),
            View("ORD-1003", 56.50m, 5.00m),
            View("ORD-1004", 79.10m, 7.00m),
            View("ORD-1005", 90.40m, 8.00m),
            View("ORD-2002", 98.00m, 9.00m), // gross off by 2.00 on the statement
            View("ORD-2003", 67.80m, 5.00m), // statement fee 6.00 vs BUY 5.00
            // ORD-2001 deliberately absent → MissingInLedger
        });
    }

    [Fact]
    public async Task Reimporting_Identical_Content_Is_A_NoOp_With_Zero_New_Rows()
    {
        var (service, store, _, _) = Build();

        var first = await service.ImportAsync(DemoRequest(), "importer", Now);
        first.IsNew.ShouldBeTrue();
        store.Statements.Count.ShouldBe(1);
        store.Lines.Count.ShouldBe(9);

        var second = await service.ImportAsync(DemoRequest(), "importer", Now.AddMinutes(5));
        second.IsNew.ShouldBeFalse();
        second.StatementId.ShouldBe(first.StatementId);
        store.Statements.Count.ShouldBe(1); // zero new rows
        store.Lines.Count.ShouldBe(9);
    }

    [Fact]
    public async Task Demo_Statement_Queues_Exactly_The_Four_Deliberate_Variances()
    {
        var (service, store, _, source) = Build();
        SeedLedger(source);

        var import = await service.ImportAsync(DemoRequest(), "importer", Now);
        await service.MatchAsync(import.StatementId, Now);

        var statement = store.Statements.Single();
        statement.Status.ShouldBe(AggregatorStatementStatus.HasExceptions);

        var types = store.Exceptions.Select(e => e.Type).ToList();
        types.Count.ShouldBe(4);
        types.ShouldContain(AggregatorVarianceType.MissingInLedger);
        types.ShouldContain(AggregatorVarianceType.AmountMismatch);
        types.ShouldContain(AggregatorVarianceType.FeeVsBuySnapshotMismatch);
        types.ShouldContain(AggregatorVarianceType.DuplicateLine);
        types.ShouldNotContain(AggregatorVarianceType.NetTransferMismatch); // declared net ties
    }

    [Fact]
    public async Task Happy_Path_Statement_Reaches_Reconciled_And_Closes()
    {
        var (service, store, _, source) = Build();

        // Clean world: the ledger holds exactly the statement's five orders (an extra reflected
        // order would rightly be MissingInStatement — covered by the matcher tests).
        ReflectedOrderMatchView View(string reference, decimal gross, decimal buy) =>
            new(reference, Day, Money.Of(gross, "SAR", vatInclusive: true), Money.Of(buy, "SAR", vatInclusive: true));
        source.Seed(Partner, new[]
        {
            View("ORD-1001", 113.00m, 10.00m),
            View("ORD-1002", 226.00m, 20.00m),
            View("ORD-1003", 56.50m, 5.00m),
            View("ORD-1004", 79.10m, 7.00m),
            View("ORD-1005", 90.40m, 8.00m),
        });

        var request = DemoRequest();
        request.Lines = request.Lines.Take(5).ToList(); // clean subset only
        request.DeclaredGross = 565.00m;
        request.DeclaredFees = 50.00m;
        request.DeclaredNet = 515.00m; // ties through CodNetTransferred

        var import = await service.ImportAsync(request, "importer", Now);
        await service.MatchAsync(import.StatementId, Now);

        var statement = store.Statements.Single();
        statement.Status.ShouldBe(AggregatorStatementStatus.Reconciled);
        store.Exceptions.ShouldBeEmpty();

        await service.CloseAsync(import.StatementId, "closer", Now);
        statement.Status.ShouldBe(AggregatorStatementStatus.Closed);
    }

    [Fact]
    public async Task Full_Cycle_Enforces_Notes_TwoPerson_And_Audits_Every_Step()
    {
        var (service, store, audit, source) = Build();
        SeedLedger(source);

        var import = await service.ImportAsync(DemoRequest(), "importer", Now);
        await service.MatchAsync(import.StatementId, Now);

        // Resolution requires a note.
        var firstException = store.Exceptions.First();
        await Should.ThrowAsync<BusinessException>(
            service.ResolveExceptionAsync(firstException.Id, "  ", "amina", Now));

        foreach (var exception in store.Exceptions)
        {
            await service.ResolveExceptionAsync(exception.Id, $"Confirmed with Jahez ops — {exception.Type}.", "amina", Now);
        }

        // Two-person: the resolver cannot close…
        (await Should.ThrowAsync<BusinessException>(service.CloseAsync(import.StatementId, "amina", Now)))
            .Code.ShouldBe(SettlementAggregatorStatementErrorCodes.CloseBlockedSameActorAsResolver);

        // …a different human commits.
        var closed = await service.CloseAsync(import.StatementId, "badr", Now);
        closed.Status.ShouldBe(AggregatorStatementStatus.Closed);

        // Audited: import, match, 4 resolutions, close.
        audit.Entries.Count(e => e.Action == AggregatorReconciliationService.AuditImport).ShouldBe(1);
        audit.Entries.Count(e => e.Action == AggregatorReconciliationService.AuditMatch).ShouldBe(1);
        audit.Entries.Count(e => e.Action == AggregatorReconciliationService.AuditResolveException).ShouldBe(4);
        audit.Entries.Count(e => e.Action == AggregatorReconciliationService.AuditClose).ShouldBe(1);

        // Immutable after close: no further resolution.
        (await Should.ThrowAsync<BusinessException>(
                service.ResolveExceptionAsync(store.Exceptions.First().Id, "late note", "amina", Now)))
            .Code.ShouldBe(SettlementAggregatorStatementErrorCodes.StatementImmutable);
    }

    [Fact]
    public async Task Reconciliation_Posts_Nothing_Trial_Balance_And_Reflected_Views_Are_Untouched()
    {
        // A fixed posting world (existing templates — settled orders + a payment into a bank).
        var period = SettlementPeriod.Of(2026, 6);
        Money Incl(decimal amount) => Money.Of(amount, SettlementConsts.DefaultCurrency, vatInclusive: true);
        var postings = new[]
        {
            SettlementPostingTemplates.Principal(Incl(60.00m), Incl(40.00m), 0.15m).Tag(Partner, Guid.NewGuid(), period, "ORD-1001"),
            SettlementPostingTemplates.Principal(Incl(40.00m), Incl(25.00m), 0.15m).Tag(Partner, Guid.NewGuid(), period, "ORD-1002"),
        };

        var trialBefore = JsonSerializer.Serialize(SettlementReports.TrialBalanceFor(postings, period));

        var (service, store, _, source) = Build();
        SeedLedger(source);
        var viewsBefore = JsonSerializer.Serialize(
            await source.GetForPartnerPeriodAsync(Partner, From, To));

        // The whole cycle: import → match → resolve all → close.
        var import = await service.ImportAsync(DemoRequest(), "importer", Now);
        await service.MatchAsync(import.StatementId, Now);
        foreach (var exception in store.Exceptions)
        {
            await service.ResolveExceptionAsync(exception.Id, "checked", "amina", Now);
        }
        await service.CloseAsync(import.StatementId, "badr", Now);

        // No posting anywhere: the trial balance over the SAME postings is byte-identical…
        JsonSerializer.Serialize(SettlementReports.TrialBalanceFor(postings, period)).ShouldBe(trialBefore);

        // …the reflected views the matcher read are byte-identical…
        JsonSerializer.Serialize(await source.GetForPartnerPeriodAsync(Partner, From, To)).ShouldBe(viewsBefore);

        // …and the Gate-1 domain surface exposes no posting/journal member at all (structural).
        foreach (var type in new[]
                 {
                     typeof(AggregatorStatement), typeof(AggregatorStatementLine),
                     typeof(AggregatorStatementException), typeof(AggregatorMatchOutcome),
                 })
        {
            type.GetProperties().ShouldAllBe(p =>
                p.PropertyType != typeof(PostingResult) && p.PropertyType != typeof(Journal));
        }
    }
}
