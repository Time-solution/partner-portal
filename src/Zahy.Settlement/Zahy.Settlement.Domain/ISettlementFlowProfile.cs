using System.Collections.Generic;

namespace Zahy.Settlement;

/// <summary>
/// A per-partner-type settlement strategy (DESIGN.md §6.1). One shared engine; each book is a
/// composed profile — NOT a subclass. A profile owns its book and its account subtree; the shared
/// primitives (Money, Journal, balance invariant, state machine) live in the engine, not here.
/// The allocation method that turns inputs into balanced legs is added in Phase 3.
/// </summary>
public interface ISettlementFlowProfile
{
    SettlementBook Book { get; }

    /// <summary>The subset of the chart of accounts this book is allowed to post to.</summary>
    IReadOnlyCollection<SettlementAccountType> AccountTree { get; }

    bool Owns(SettlementAccountType account);
}

/// <summary>Resolves the flow profile for a book. Composition over inheritance: profiles are injected.</summary>
public interface ISettlementFlowProfileResolver
{
    ISettlementFlowProfile Resolve(SettlementBook book);
}
