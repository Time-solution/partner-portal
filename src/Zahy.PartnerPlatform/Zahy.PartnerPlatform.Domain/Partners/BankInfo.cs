namespace Zahy.PartnerPlatform.Partners;

/// <summary>Payout bank details. Optional at registration; required before approval.</summary>
public class BankInfo
{
    public string? BankName { get; private set; }

    public string? AccountHolderName { get; private set; }

    public string? Iban { get; private set; }

    public string? SwiftCode { get; private set; }

    protected BankInfo()
    {
    }

    public BankInfo(string? bankName, string? accountHolderName, string? iban, string? swiftCode)
    {
        BankName = bankName;
        AccountHolderName = accountHolderName;
        Iban = string.IsNullOrWhiteSpace(iban) ? null : SaudiIbanValidator.Normalize(iban);
        SwiftCode = swiftCode;
    }

    public bool IsComplete =>
        !string.IsNullOrWhiteSpace(BankName) &&
        !string.IsNullOrWhiteSpace(AccountHolderName) &&
        !string.IsNullOrWhiteSpace(Iban);

    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(BankName) &&
        string.IsNullOrWhiteSpace(AccountHolderName) &&
        string.IsNullOrWhiteSpace(Iban) &&
        string.IsNullOrWhiteSpace(SwiftCode);
}
