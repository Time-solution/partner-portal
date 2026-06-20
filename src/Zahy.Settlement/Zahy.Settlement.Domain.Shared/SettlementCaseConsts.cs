namespace Zahy.Settlement;

public static class SettlementCaseConsts
{
    public const int MaxExternalTransactionIdLength = 256;
}

/// <summary>
/// Phase-2 error codes (case lifecycle, flow-profile routing, book isolation). Kept separate from
/// the Phase-1 SettlementErrorCodes so the locked Phase-1 file is not modified.
/// </summary>
public static class SettlementCaseErrorCodes
{
    public const string Namespace = "Zahy.Settlement";

    public const string IllegalStateTransition = Namespace + ":020";
    public const string EmptyExternalTransactionId = Namespace + ":021";
    public const string AccountNotInBook = Namespace + ":022";
    public const string UnknownFlowProfile = Namespace + ":023";
    public const string BookMismatch = Namespace + ":024";
    public const string InvalidReversalLink = Namespace + ":025";
}
