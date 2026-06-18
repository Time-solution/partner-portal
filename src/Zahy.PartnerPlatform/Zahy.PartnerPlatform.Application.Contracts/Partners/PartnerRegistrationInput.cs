using System.ComponentModel.DataAnnotations;

namespace Zahy.PartnerPlatform.Partners;

public class PartnerRegistrationInput
{
    [Required]
    public PartnerType Type { get; set; }

    [Required]
    [StringLength(PartnerPlatformConsts.MaxLegalNameLength)]
    public string LegalName { get; set; } = string.Empty;

    [StringLength(PartnerPlatformConsts.MaxTradeNameLength)]
    public string? TradeName { get; set; }

    [Required]
    public ContactInfoDto ContactInfo { get; set; } = new();

    public BankInfoDto? BankInfo { get; set; }

    [Required]
    [EmailAddress]
    [StringLength(PartnerPlatformConsts.MaxEmailLength)]
    public string PrimaryContactEmail { get; set; } = string.Empty;

    [Required]
    [StringLength(PartnerPlatformConsts.MaxNameLength)]
    public string RegistrantName { get; set; } = string.Empty;

    [Required]
    [StringLength(PartnerPlatformConsts.MaxPhoneLength)]
    public string RegistrantPhone { get; set; } = string.Empty;
}
