namespace Zahy.Connectors;

public static class ConnectorConsts
{
    public const string DefaultCurrency = "SAR";
    public const string SourceSystemPrefix = "connector";

    public const string MockAggregatorCode = "mock-aggregator";
    public const string MockThreePLCode = "mock-3pl";
    public const string MockCarrierCode = "mock-carrier";
    public const string MockJumpConsignmentCode = "mock-jump";

    public const int DefaultAcceptWindowMinutes = 5;

    public const int MaxConnectorCodeLength = 64;
    public const int MaxExternalIdLength = 128;
    public const int MaxNameLength = 256;
    public const int MaxNotesLength = 512;
    public const int MaxDisplayNameLength = 256;
    public const int MaxConfigJsonLength = 4096;
    public const int MaxSecretReferenceLength = 256;
}
