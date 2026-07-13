using System;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace Zahy.Settlement;

/// <summary>
/// Statement lifecycle + the human gates: Imported → Matching → Reconciled | HasExceptions → Closed,
/// mandatory-note resolution, close blocked on open exceptions, the TWO-PERSON rule (resolver ≠
/// closer, mirroring reconciler ≠ releaser), and immutability after Reconciled/Closed.
/// </summary>
public class AggregatorStatementLifecycleTests
{
    private static readonly DateTime At = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

    private static AggregatorStatement NewStatement() =>
        AggregatorStatement.Import(
            Guid.NewGuid(), Guid.NewGuid(), "Jahez",
            new DateTime(2026, 6, 1), new DateTime(2026, 6, 30),
            "hash-" + Guid.NewGuid().ToString("N"),
            778.00m, 55.00m, 709.00m, "SAR", "importer", At, lineCount: 9);

    private static AggregatorStatementException NewException(Guid statementId) =>
        AggregatorStatementException.Create(
            Guid.NewGuid(), statementId, null, AggregatorVarianceType.MissingInLedger,
            "ORD-2001", null, 45.20m, "No reflected order.");

    [Fact]
    public void Import_Guards_Reject_Empty_Lines_And_Inverted_Period()
    {
        Should.Throw<BusinessException>(() => AggregatorStatement.Import(
                Guid.NewGuid(), Guid.NewGuid(), "Jahez",
                new DateTime(2026, 6, 1), new DateTime(2026, 6, 30),
                "hash", 0m, 0m, 0m, "SAR", "importer", At, lineCount: 0))
            .Code.ShouldBe(SettlementAggregatorStatementErrorCodes.StatementImportInvalid);

        Should.Throw<BusinessException>(() => AggregatorStatement.Import(
                Guid.NewGuid(), Guid.NewGuid(), "Jahez",
                new DateTime(2026, 6, 30), new DateTime(2026, 6, 1),
                "hash", 0m, 0m, 0m, "SAR", "importer", At, lineCount: 3))
            .Code.ShouldBe(SettlementAggregatorStatementErrorCodes.StatementImportInvalid);
    }

    [Fact]
    public void Clean_Match_Reaches_Reconciled_And_Any_Closer_May_Commit()
    {
        var statement = NewStatement();
        statement.BeginMatching();
        statement.CompleteMatching(hasExceptions: false, At);

        statement.Status.ShouldBe(AggregatorStatementStatus.Reconciled);

        // No exceptions were resolved, so the two-person set is empty — the closer commits.
        statement.Close("closer", Array.Empty<string>(), openExceptionCount: 0, At);
        statement.Status.ShouldBe(AggregatorStatementStatus.Closed);
        statement.ClosedBy.ShouldBe("closer");

        // Idempotent re-close is a no-op.
        statement.Close("someone-else", Array.Empty<string>(), 0, At.AddDays(1));
        statement.ClosedBy.ShouldBe("closer");
    }

    [Fact]
    public void Illegal_Transitions_Are_Rejected()
    {
        var statement = NewStatement();

        Should.Throw<BusinessException>(() => statement.CompleteMatching(false, At))
            .Code.ShouldBe(SettlementAggregatorStatementErrorCodes.IllegalStatementTransition);

        statement.BeginMatching();
        Should.Throw<BusinessException>(() => statement.BeginMatching())
            .Code.ShouldBe(SettlementAggregatorStatementErrorCodes.IllegalStatementTransition);

        Should.Throw<BusinessException>(() => statement.Close("x", Array.Empty<string>(), 0, At))
            .Code.ShouldBe(SettlementAggregatorStatementErrorCodes.IllegalStatementTransition);
    }

    [Fact]
    public void Close_Is_Blocked_While_Any_Exception_Is_Open()
    {
        var statement = NewStatement();
        statement.BeginMatching();
        statement.CompleteMatching(hasExceptions: true, At);

        Should.Throw<BusinessException>(() => statement.Close("closer", Array.Empty<string>(), openExceptionCount: 1, At))
            .Code.ShouldBe(SettlementAggregatorStatementErrorCodes.CloseBlockedOpenExceptions);
    }

    [Fact]
    public void Two_Person_Rule_Resolver_Cannot_Close_But_A_Different_User_Can()
    {
        var statement = NewStatement();
        statement.BeginMatching();
        statement.CompleteMatching(hasExceptions: true, At);

        var resolvers = new[] { "amina" };

        // The human who resolved an exception cannot also close — even with the permission.
        Should.Throw<BusinessException>(() => statement.Close("AMINA ", resolvers, 0, At))
            .Code.ShouldBe(SettlementAggregatorStatementErrorCodes.CloseBlockedSameActorAsResolver);

        statement.Close("badr", resolvers, 0, At);
        statement.Status.ShouldBe(AggregatorStatementStatus.Closed);
        statement.ClosedBy.ShouldBe("badr");
    }

    [Fact]
    public void Exception_Resolution_Requires_A_Note_And_First_Resolution_Stands()
    {
        var exception = NewException(Guid.NewGuid());

        Should.Throw<BusinessException>(() => exception.Resolve("   ", "amina", At))
            .Code.ShouldBe(SettlementAggregatorStatementErrorCodes.ExceptionResolutionRequiresNote);

        exception.Resolve("Aggregator confirmed the order was cancelled after cutoff.", "amina", At);
        exception.Resolved.ShouldBeTrue();
        exception.ResolvedBy.ShouldBe("amina");

        exception.Resolve("second note", "badr", At.AddDays(1)); // idempotent no-op
        exception.ResolvedBy.ShouldBe("amina");
        exception.ResolutionNote.ShouldBe("Aggregator confirmed the order was cancelled after cutoff.");
    }

    [Fact]
    public void Reconciled_And_Closed_Statements_Are_Immutable()
    {
        var reconciled = NewStatement();
        reconciled.BeginMatching();
        reconciled.CompleteMatching(hasExceptions: false, At);
        Should.Throw<BusinessException>(() => reconciled.EnsureMutable())
            .Code.ShouldBe(SettlementAggregatorStatementErrorCodes.StatementImmutable);

        reconciled.Close("closer", Array.Empty<string>(), 0, At);
        Should.Throw<BusinessException>(() => reconciled.EnsureMutable())
            .Code.ShouldBe(SettlementAggregatorStatementErrorCodes.StatementImmutable);
    }
}
