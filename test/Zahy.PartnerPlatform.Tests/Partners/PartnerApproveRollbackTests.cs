using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.Testing;
using Volo.Abp.Uow;
using Volo.Abp.Validation;
using Xunit;
using Zahy.Identity;
using Zahy.Identity.Auditing;
using Zahy.Identity.OpenIddict;
using Zahy.Identity.Roles;

namespace Zahy.PartnerPlatform.Partners;

[DependsOn(typeof(ZahyPartnerPlatformIntegrationTestModule))]
public class ZahyPartnerPlatformFailingM2MTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.Replace(ServiceDescriptor.Singleton<IPartnerM2MClientProvisioner, FailingPartnerM2MClientProvisioner>());
    }
}

public class PartnerApproveRollbackTests : AbpIntegratedTest<ZahyPartnerPlatformFailingM2MTestModule>
{
    private readonly IPartnerRegistrationAppService _registrationAppService;
    private readonly IPartnerAdminAppService _adminAppService;
    private readonly IRepository<Partner, Guid> _partnerRepository;
    private readonly RecordingAdminAuditLogger _auditLogger;

    public PartnerApproveRollbackTests()
    {
        _registrationAppService = GetRequiredService<IPartnerRegistrationAppService>();
        _adminAppService = GetRequiredService<IPartnerAdminAppService>();
        _partnerRepository = GetRequiredService<IRepository<Partner, Guid>>();
        _auditLogger = (RecordingAdminAuditLogger)GetRequiredService<IAdminAuditLogger>();
    }

    protected override void SetAbpApplicationCreationOptions(AbpApplicationCreationOptions options)
    {
        options.UseAutofac();
    }

    [Fact]
    public async Task Should_Rollback_Approve_When_M2M_Provision_Fails()
    {
        var partnerId = await RegisterPartnerAsync();
        _auditLogger.Clear();

        var exception = await Should.ThrowAsync<BusinessException>(async () =>
        {
            await WithUnitOfWorkAsync(async () =>
            {
                await _adminAppService.ApproveAsync(partnerId);
            });
        });

        exception.Code.ShouldBe(ZahyIdentityErrorCodes.PartnerM2MClientAlreadyExists);

        await WithUnitOfWorkAsync(async () =>
        {
            var partner = await _partnerRepository.GetAsync(partnerId);
            partner.Status.ShouldBe(PartnerStatus.Pending);
            partner.OpenIddictClientId.ShouldBeNull();
        });

        _auditLogger.Entries.ShouldContain(e =>
            e.Action == "Partner.Approve" &&
            e.Result == AdminAuditResults.Denied);
        _auditLogger.Entries.ShouldNotContain(e =>
            e.Action == "Partner.Approve" &&
            e.Result == AdminAuditResults.Success);
    }

    private async Task<Guid> RegisterPartnerAsync()
    {
        PartnerRegistrationResultDto result = null!;
        await WithUnitOfWorkAsync(async () =>
        {
            result = await _registrationAppService.RegisterAsync(new PartnerRegistrationInput
            {
                Type = PartnerType.Aggregator,
                LegalName = $"Partner {Guid.NewGuid():N}",
                PrimaryContactEmail = $"owner-{Guid.NewGuid():N}@acme.test",
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
                BankInfo = new BankInfoDto
                {
                    BankName = "Al Rajhi Bank",
                    AccountHolderName = "Acme Logistics LLC",
                    Iban = "SA0380000000608010167519"
                }
            });
        });

        return result.PartnerId;
    }

    private async Task WithUnitOfWorkAsync(Func<Task> action)
    {
        var uowManager = GetRequiredService<IUnitOfWorkManager>();
        using var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true);
        await action();
        await uow.CompleteAsync();
    }
}
