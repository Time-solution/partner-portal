using Zahy.OrderLedger;

namespace Zahy.Connectors;

public interface ICanonicalOrderMapper
{
    OrderSourceSnapshot ToSnapshot(CanonicalOrder order);

    OrderSourceSnapshot ToSnapshot(OrderRecord record);

    OrderSourceSnapshot ToStatusSnapshot(
        OrderSourceSnapshot latestSnapshot,
        CanonicalOrderStatusUpdate update);

    CanonicalOrder FromSnapshot(OrderSourceSnapshot snapshot, ConnectorKind connectorKind, string connectorCode);
}

public static class ConnectorSourceSystem
{
    public static string Build(ConnectorKind kind, string connectorCode) =>
        $"{ConnectorConsts.SourceSystemPrefix}:{kind.ToString().ToLowerInvariant()}:{connectorCode}";

    public static bool TryParse(string sourceSystem, out ConnectorKind kind, out string connectorCode)
    {
        kind = default;
        connectorCode = string.Empty;

        if (string.IsNullOrWhiteSpace(sourceSystem))
        {
            return false;
        }

        var parts = sourceSystem.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 3 ||
            !parts[0].Equals(ConnectorConsts.SourceSystemPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!Enum.TryParse<ConnectorKind>(parts[1], ignoreCase: true, out kind))
        {
            return false;
        }

        connectorCode = parts[2];
        return true;
    }
}
