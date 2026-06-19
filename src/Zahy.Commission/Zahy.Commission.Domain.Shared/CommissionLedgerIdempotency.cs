namespace Zahy.Commission;

public static class CommissionLedgerIdempotency
{
    public static string BuildAccrualKey(string sourceType, string sourceId, Guid ruleId) =>
        $"{sourceType.Trim()}:{sourceId.Trim()}:{ruleId:D}";

    public static string BuildReversalKey(string sourceType, string sourceId, Guid ruleId, Guid originalEntryId) =>
        $"{BuildAccrualKey(sourceType, sourceId, ruleId)}:reversal:{originalEntryId:D}";
}
