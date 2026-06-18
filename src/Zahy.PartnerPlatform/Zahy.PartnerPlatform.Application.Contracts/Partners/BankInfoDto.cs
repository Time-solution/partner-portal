using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Zahy.PartnerPlatform.Partners;

public class BankInfoDto : IValidatableObject
{
    [StringLength(PartnerPlatformConsts.MaxBankNameLength)]
    public string? BankName { get; set; }

    [StringLength(PartnerPlatformConsts.MaxAccountHolderNameLength)]
    public string? AccountHolderName { get; set; }

    [StringLength(PartnerPlatformConsts.MaxIbanLength)]
    public string? Iban { get; set; }

    [StringLength(PartnerPlatformConsts.MaxSwiftCodeLength)]
    public string? SwiftCode { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!SaudiIbanValidator.IsValidOrEmpty(Iban))
        {
            yield return new ValidationResult(
                "Invalid Saudi IBAN format. Expected SA followed by 22 digits.",
                new[] { nameof(Iban) });
        }
    }
}
