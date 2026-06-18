using System.ComponentModel.DataAnnotations;
using System.Linq;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Validation;
using Xunit;

namespace Zahy.PartnerPlatform.Partners;

public class PartnerRegistrationTests : ZahyPartnerPlatformTestBase
{
    private readonly IPartnerRegistrationAppService _registrationAppService;
    private readonly IRepository<Partner, Guid> _partnerRepository;

    public PartnerRegistrationTests()
    {
        _registrationAppService = GetRequiredService<IPartnerRegistrationAppService>();
        _partnerRepository = GetRequiredService<IRepository<Partner, Guid>>();
    }

    [Fact]
    public async Task Should_Create_Pending_Partner_Without_BankInfo()
    {
        var input = CreateValidInput(includeBankInfo: false);

        PartnerRegistrationResultDto result = null!;
        await WithUnitOfWorkAsync(async () =>
        {
            result = await _registrationAppService.RegisterAsync(input);
        });

        result.Status.ShouldBe(PartnerStatus.Pending);
        result.PartnerId.ShouldNotBe(Guid.Empty);
        result.SubmittedAt.ShouldNotBe(default);

        await WithUnitOfWorkAsync(async () =>
        {
            var partner = await _partnerRepository.GetAsync(result.PartnerId);
            partner.Status.ShouldBe(PartnerStatus.Pending);
            partner.Type.ShouldBe(PartnerType.Aggregator);
            partner.LegalName.ShouldBe(input.LegalName);
            partner.PrimaryContactEmail.ShouldBe(input.PrimaryContactEmail);
            partner.BankInfo.IsEmpty.ShouldBeTrue();
            partner.HasCompleteBankInfo().ShouldBeFalse();
            partner.TenantId.ShouldBeNull();
        });
    }

    [Fact]
    public async Task Should_Create_Pending_Partner_With_Valid_BankInfo()
    {
        var input = CreateValidInput(includeBankInfo: true);

        PartnerRegistrationResultDto result = null!;
        await WithUnitOfWorkAsync(async () =>
        {
            result = await _registrationAppService.RegisterAsync(input);
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var partner = await _partnerRepository.GetAsync(result.PartnerId);
            partner.BankInfo.Iban.ShouldBe("SA0380000000608010167519");
            partner.HasCompleteBankInfo().ShouldBeTrue();
        });
    }

    [Fact]
    public async Task Should_Reject_Invalid_Iban_On_Dto_Validation()
    {
        var input = CreateValidInput(includeBankInfo: true);
        input.BankInfo!.Iban = "INVALID";

        await Should.ThrowAsync<AbpValidationException>(async () =>
        {
            await WithUnitOfWorkAsync(async () =>
            {
                await _registrationAppService.RegisterAsync(input);
            });
        });
    }

    [Fact]
    public async Task Should_Reject_Missing_Required_Fields_On_Dto_Validation()
    {
        var input = new PartnerRegistrationInput();

        var exception = await Should.ThrowAsync<AbpValidationException>(async () =>
        {
            await WithUnitOfWorkAsync(async () =>
            {
                await _registrationAppService.RegisterAsync(input);
            });
        });

        exception.ValidationErrors.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Should_Reject_Duplicate_Pending_Registration_For_Same_Email()
    {
        const string email = "pending-duplicate@test.local";
        var input = CreateValidInput(includeBankInfo: false, primaryContactEmail: email);

        await WithUnitOfWorkAsync(async () =>
        {
            await _registrationAppService.RegisterAsync(input);
        });

        var duplicate = CreateValidInput(includeBankInfo: false, primaryContactEmail: email);

        var exception = await Should.ThrowAsync<BusinessException>(async () =>
        {
            await WithUnitOfWorkAsync(async () =>
            {
                await _registrationAppService.RegisterAsync(duplicate);
            });
        });

        exception.Code.ShouldBe(PartnerPlatformErrorCodes.DuplicatePendingRegistration);
    }

    [Fact]
    public void BankInfoDto_Should_Validate_Iban_Format()
    {
        var dto = new BankInfoDto { Iban = "BAD" };
        var results = Validate(dto);

        results.ShouldContain(r => r.MemberNames.Contains(nameof(BankInfoDto.Iban)));
    }

    private static PartnerRegistrationInput CreateValidInput(bool includeBankInfo, string? primaryContactEmail = null)
    {
        primaryContactEmail ??= $"owner-{Guid.NewGuid():N}@acme.test";

        return new PartnerRegistrationInput
        {
            Type = PartnerType.Aggregator,
            LegalName = "Acme Logistics LLC",
            TradeName = "Acme",
            PrimaryContactEmail = primaryContactEmail,
            RegistrantName = "Sara Al-Qahtani",
            RegistrantPhone = "+966501234567",
            ContactInfo = new ContactInfoDto
            {
                ContactName = "Sara Al-Qahtani",
                Email = "contact@acme.test",
                Phone = "+966501234567",
                AddressLine1 = "King Fahd Road 100",
                City = "Riyadh",
                Region = "Riyadh",
                PostalCode = "12345",
                CountryCode = "SA"
            },
            BankInfo = includeBankInfo
                ? new BankInfoDto
                {
                    BankName = "Al Rajhi Bank",
                    AccountHolderName = "Acme Logistics LLC",
                    Iban = "SA0380000000608010167519"
                }
                : null
        };
    }

    private static IList<ValidationResult> Validate(object instance)
    {
        var context = new ValidationContext(instance);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(instance, context, results, validateAllProperties: true);
        return results;
    }
}
