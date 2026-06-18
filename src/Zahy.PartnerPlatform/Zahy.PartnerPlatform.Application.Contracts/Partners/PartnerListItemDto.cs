using System;

namespace Zahy.PartnerPlatform.Partners;

public class PartnerListItemDto
{
    public Guid Id { get; set; }

    public PartnerType Type { get; set; }

    public PartnerStatus Status { get; set; }

    public string LegalName { get; set; } = string.Empty;

    public string? TradeName { get; set; }

    public string PrimaryContactEmail { get; set; } = string.Empty;

    public DateTime CreationTime { get; set; }

    /// <summary>Masked IBAN (last 4) when bank info exists.</summary>
    public string? MaskedIban { get; set; }
}
