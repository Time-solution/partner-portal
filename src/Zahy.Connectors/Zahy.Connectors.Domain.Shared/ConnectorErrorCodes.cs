namespace Zahy.Connectors;

public static class ConnectorErrorCodes
{
    public const string Namespace = "Zahy.Connectors";

    public const string ConnectorNotFound = Namespace + ":001";
    public const string ConnectorNotSupportedForPartnerType = Namespace + ":002";
    public const string ConnectorReceiveFailed = Namespace + ":003";
    public const string ConnectorStatusUpdateFailed = Namespace + ":004";
    public const string ConnectorOrderNotFoundInLedger = Namespace + ":005";
    public const string InvalidConnectorCode = Namespace + ":006";
    public const string InvalidConnectorRegistration = Namespace + ":007";
}
