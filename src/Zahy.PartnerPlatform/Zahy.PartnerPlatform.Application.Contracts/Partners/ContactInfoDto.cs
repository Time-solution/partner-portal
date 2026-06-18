using System.ComponentModel.DataAnnotations;

namespace Zahy.PartnerPlatform.Partners;

public class ContactInfoDto
{
    [Required]
    [StringLength(PartnerPlatformConsts.MaxNameLength)]
    public string ContactName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(PartnerPlatformConsts.MaxEmailLength)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(PartnerPlatformConsts.MaxPhoneLength)]
    public string Phone { get; set; } = string.Empty;

    [Required]
    [StringLength(PartnerPlatformConsts.MaxAddressLineLength)]
    public string AddressLine1 { get; set; } = string.Empty;

    [StringLength(PartnerPlatformConsts.MaxAddressLineLength)]
    public string? AddressLine2 { get; set; }

    [Required]
    [StringLength(PartnerPlatformConsts.MaxCityLength)]
    public string City { get; set; } = string.Empty;

    [Required]
    [StringLength(PartnerPlatformConsts.MaxRegionLength)]
    public string Region { get; set; } = string.Empty;

    [StringLength(PartnerPlatformConsts.MaxPostalCodeLength)]
    public string PostalCode { get; set; } = string.Empty;

    [StringLength(PartnerPlatformConsts.MaxCountryCodeLength)]
    public string CountryCode { get; set; } = PartnerPlatformConsts.DefaultCountryCode;
}
