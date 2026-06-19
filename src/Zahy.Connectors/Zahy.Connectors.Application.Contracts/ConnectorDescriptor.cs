namespace Zahy.Connectors;

public sealed class ConnectorDescriptor
{
    public string ConnectorCode { get; init; } = string.Empty;

    public ConnectorKind Kind { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    public ConnectorOperations SupportedOperations { get; init; }
}
