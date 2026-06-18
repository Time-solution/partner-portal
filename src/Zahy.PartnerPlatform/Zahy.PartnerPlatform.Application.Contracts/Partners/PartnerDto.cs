using System;

namespace Zahy.PartnerPlatform.Partners;

public class PartnerDto
{
    public Guid Id { get; set; }

    public PartnerType Type { get; set; }

    public PartnerStatus Status { get; set; }

    public string LegalName { get; set; } = string.Empty;

    public string? TradeName { get; set; }

    public ContactInfoDto ContactInfo { get; set; } = new();

    public BankInfoDto? BankInfo { get; set; }

    public string PrimaryContactEmail { get; set; } = string.Empty;

    public string RegistrantName { get; set; } = string.Empty;

    public string RegistrantPhone { get; set; } = string.Empty;

    public CloseReason? CloseReason { get; set; }

    public string? CloseNotes { get; set; }

    public string? OpenIddictClientId { get; set; }

    public DateTime CreationTime { get; set; }

    public bool HasCompleteBankInfo { get; set; }
}
