namespace Zahy.Connectors;

public sealed class CanonicalMenuSyncResult
{
    public IReadOnlyList<CanonicalMenuItem> Items { get; init; } = [];

    public DateTime SyncedAt { get; init; }
}

public sealed class CanonicalAcceptResult
{
    public CanonicalAcceptOutcome Outcome { get; init; }

    public string ExternalOrderId { get; init; } = string.Empty;

    public DateTime OccurredAt { get; init; }
}
