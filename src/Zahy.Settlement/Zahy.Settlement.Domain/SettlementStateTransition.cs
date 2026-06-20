using System;

namespace Zahy.Settlement;

/// <summary>An append-only record of one state change on a settlement case.</summary>
public sealed record SettlementStateTransition(SettlementCaseState? From, SettlementCaseState To, DateTime At);
