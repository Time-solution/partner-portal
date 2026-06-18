using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace Zahy.PartnerPlatform.Partners;

public class PartnerRegistrationAppService : ApplicationService, IPartnerRegistrationAppService
{
    private readonly IRepository<Partner, System.Guid> _partnerRepository;

    public PartnerRegistrationAppService(IRepository<Partner, System.Guid> partnerRepository)
    {
        _partnerRepository = partnerRepository;
    }

    [UnitOfWork]
    public virtual async Task<PartnerRegistrationResultDto> RegisterAsync(PartnerRegistrationInput input)
    {
        await EnsureNoDuplicatePendingRegistrationAsync(input.PrimaryContactEmail);

        var contactInfo = MapContactInfo(input.ContactInfo);
        var bankInfo = MapBankInfo(input.BankInfo);

        var partner = new Partner(
            GuidGenerator.Create(),
            input.Type,
            input.LegalName,
            input.TradeName,
            contactInfo,
            bankInfo,
            input.PrimaryContactEmail.Trim(),
            input.RegistrantName.Trim(),
            input.RegistrantPhone.Trim());

        await _partnerRepository.InsertAsync(partner, autoSave: true);

        return new PartnerRegistrationResultDto
        {
            PartnerId = partner.Id,
            Status = partner.Status,
            SubmittedAt = partner.CreationTime
        };
    }

    private async Task EnsureNoDuplicatePendingRegistrationAsync(string primaryContactEmail)
    {
        var normalizedEmail = primaryContactEmail.Trim().ToUpperInvariant();
        var queryable = await _partnerRepository.GetQueryableAsync();
        var exists = queryable.Any(p =>
            p.Status == PartnerStatus.Pending &&
            p.PrimaryContactEmail.ToUpper() == normalizedEmail);

        if (exists)
        {
            throw new BusinessException(PartnerPlatformErrorCodes.DuplicatePendingRegistration)
                .WithData("PrimaryContactEmail", primaryContactEmail);
        }
    }

    private static ContactInfo MapContactInfo(ContactInfoDto dto)
    {
        return new ContactInfo(
            dto.ContactName.Trim(),
            dto.Email.Trim(),
            dto.Phone.Trim(),
            dto.AddressLine1.Trim(),
            dto.AddressLine2?.Trim(),
            dto.City.Trim(),
            dto.Region.Trim(),
            dto.PostalCode?.Trim() ?? string.Empty,
            string.IsNullOrWhiteSpace(dto.CountryCode)
                ? PartnerPlatformConsts.DefaultCountryCode
                : dto.CountryCode.Trim().ToUpperInvariant());
    }

    private static BankInfo MapBankInfo(BankInfoDto? dto)
    {
        if (dto == null)
        {
            return new BankInfo(null, null, null, null);
        }

        return new BankInfo(
            dto.BankName?.Trim(),
            dto.AccountHolderName?.Trim(),
            dto.Iban?.Trim(),
            dto.SwiftCode?.Trim());
    }
}
