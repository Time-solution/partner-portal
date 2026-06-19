using System.Linq;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Validation;
using Xunit;
using Zahy.Identity;
using Zahy.Identity.Auditing;
using Zahy.Identity.OpenIddict;
using Zahy.Identity.Roles;

namespace Zahy.PartnerPlatform.Partners;

public class PartnerAdminAppServiceTests : ZahyPartnerPlatformTestBase
{
    private readonly IPartnerRegistrationAppService _registrationAppService;
    private readonly IPartnerAdminAppService _adminAppService;
    private readonly IRepository<Partner, Guid> _partnerRepository;
    private readonly RecordingAdminAuditLogger _auditLogger;
    private readonly FakePartnerM2MClientProvisioner _m2mProvisioner;

    public PartnerAdminAppServiceTests()
    {
        _registrationAppService = GetRequiredService<IPartnerRegistrationAppService>();
        _adminAppService = GetRequiredService<IPartnerAdminAppService>();
        _partnerRepository = GetRequiredService<IRepository<Partner, Guid>>();
        _auditLogger = (RecordingAdminAuditLogger)GetRequiredService<IAdminAuditLogger>();
        _m2mProvisioner = (FakePartnerM2MClientProvisioner)GetRequiredService<IPartnerM2MClientProvisioner>();
    }

    [Fact]
    public async Task Should_Approve_Pending_Partner_And_Return_One_Time_M2M_Secret()
    {
        var partnerId = await RegisterPartnerAsync(includeBankInfo: true);
        _auditLogger.Clear();
        _m2mProvisioner.Reset();

        PartnerApproveResultDto result = null!;
        await WithUnitOfWorkAsync(async () =>
        {
            result = await _adminAppService.ApproveAsync(partnerId);
        });

        result.Partner.Status.ShouldBe(PartnerStatus.Active);
        result.ClientId.ShouldBe(PartnerM2MClientProvisioner.BuildClientId(partnerId));
        result.ClientSecret.ShouldBe("test-client-secret-shown-once");
        result.Scopes.ShouldBe([ZahyScopes.OrdersRead, ZahyScopes.WebhooksManage]);
        result.OwnerIdentityUserId.ShouldNotBeNull();
        result.OwnerSetPasswordToken.ShouldBe("test-set-password-token-once");

        _m2mProvisioner.LastRequest.ShouldNotBeNull();
        _m2mProvisioner.LastRequest!.PartnerId.ShouldBe(partnerId);
        _m2mProvisioner.LastRequest.Scopes.ShouldBe([ZahyScopes.OrdersRead, ZahyScopes.WebhooksManage]);

        _auditLogger.Entries.ShouldContain(e =>
            e.Action == "Partner.Approve" &&
            e.TargetId == partnerId.ToString() &&
            e.Result == AdminAuditResults.Success);

        await WithUnitOfWorkAsync(async () =>
        {
            var partner = await _partnerRepository.GetAsync(partnerId);
            partner.OpenIddictClientId.ShouldBe(result.ClientId);

            using (GetRequiredService<IDataFilter>().Disable<IPartnerDataFilter>())
            {
                var ownerMembership = await GetRequiredService<IRepository<PartnerUser, Guid>>()
                    .FirstOrDefaultAsync(x => x.PartnerId == partnerId && x.Role == ZahyRoles.PartnerOwner);
                ownerMembership.ShouldNotBeNull();
                ownerMembership!.Status.ShouldBe(PartnerUserStatus.Invited);
            }
        });
    }

    [Fact]
    public async Task Should_Not_Log_M2M_Client_Secret_In_Approve_Audit()
    {
        var partnerId = await RegisterPartnerAsync(includeBankInfo: true);
        _auditLogger.Clear();
        _m2mProvisioner.Reset();

        PartnerApproveResultDto result = null!;
        await WithUnitOfWorkAsync(async () =>
        {
            result = await _adminAppService.ApproveAsync(partnerId);
        });

        result.ClientSecret.ShouldNotBeNullOrWhiteSpace();
        _auditLogger.Entries
            .Where(e => e.Action == "Partner.Approve")
            .ShouldAllBe(e => e.ExtraData == null || !e.ExtraData!.Contains(result.ClientSecret));
    }

    [Fact]
    public async Task Should_Rotate_M2M_Client_Secret_For_Active_Partner()
    {
        var partnerId = await RegisterPartnerAsync(includeBankInfo: true);

        await WithUnitOfWorkAsync(async () =>
        {
            await _adminAppService.ApproveAsync(partnerId);
        });

        PartnerM2MRotateResultDto rotateResult = null!;
        await WithUnitOfWorkAsync(async () =>
        {
            rotateResult = await _adminAppService.RotateM2MClientSecretAsync(partnerId);
        });

        rotateResult.ClientSecret.ShouldBe("rotated-client-secret-once");
        rotateResult.ClientId.ShouldBe(PartnerM2MClientProvisioner.BuildClientId(partnerId));
    }

    [Fact]
    public async Task Should_Reject_Approve_When_BankInfo_Incomplete()
    {
        var partnerId = await RegisterPartnerAsync(includeBankInfo: false);
        _auditLogger.Clear();

        var exception = await Should.ThrowAsync<BusinessException>(async () =>
        {
            await WithUnitOfWorkAsync(async () =>
            {
                await _adminAppService.ApproveAsync(partnerId);
            });
        });

        exception.Code.ShouldBe(PartnerPlatformErrorCodes.BankInfoRequiredForApproval);
        _auditLogger.Entries.ShouldContain(e =>
            e.Action == "Partner.Approve" &&
            e.Result == AdminAuditResults.Denied);

        await WithUnitOfWorkAsync(async () =>
        {
            var partner = await _partnerRepository.GetAsync(partnerId);
            partner.Status.ShouldBe(PartnerStatus.Pending);
        });
    }

    [Fact]
    public async Task Should_Reject_Pending_Partner()
    {
        var partnerId = await RegisterPartnerAsync(includeBankInfo: false);

        PartnerDto result = null!;
        await WithUnitOfWorkAsync(async () =>
        {
            result = await _adminAppService.RejectAsync(partnerId, new PartnerLifecycleActionInput
            {
                Notes = "Incomplete documentation"
            });
        });

        result.Status.ShouldBe(PartnerStatus.Closed);
        result.CloseReason.ShouldBe(CloseReason.Rejected);
        result.CloseNotes.ShouldBe("Incomplete documentation");
    }

    [Fact]
    public async Task Should_Suspend_Reactivate_And_Close_Active_Partner()
    {
        var partnerId = await RegisterPartnerAsync(includeBankInfo: true);

        await WithUnitOfWorkAsync(async () =>
        {
            await _adminAppService.ApproveAsync(partnerId);
        });

        PartnerDto suspended = null!;
        await WithUnitOfWorkAsync(async () =>
        {
            suspended = await _adminAppService.SuspendAsync(partnerId, new PartnerLifecycleActionInput
            {
                Notes = "Compliance review"
            });
        });
        suspended.Status.ShouldBe(PartnerStatus.Suspended);

        PartnerDto reactivated = null!;
        await WithUnitOfWorkAsync(async () =>
        {
            reactivated = await _adminAppService.ReactivateAsync(partnerId);
        });
        reactivated.Status.ShouldBe(PartnerStatus.Active);

        PartnerDto closed = null!;
        await WithUnitOfWorkAsync(async () =>
        {
            closed = await _adminAppService.CloseAsync(partnerId, new PartnerLifecycleActionInput
            {
                Notes = "Contract ended"
            });
        });
        closed.Status.ShouldBe(PartnerStatus.Closed);
        closed.CloseReason.ShouldBe(CloseReason.AdminClosed);
    }

    [Fact]
    public async Task Should_Reject_Illegal_Lifecycle_Transition()
    {
        var partnerId = await RegisterPartnerAsync(includeBankInfo: false);

        var exception = await Should.ThrowAsync<BusinessException>(async () =>
        {
            await WithUnitOfWorkAsync(async () =>
            {
                await _adminAppService.SuspendAsync(partnerId);
            });
        });

        exception.Code.ShouldBe(PartnerPlatformErrorCodes.IllegalLifecycleTransition);
    }

    [Fact]
    public async Task Should_List_Partners_With_Status_Filter()
    {
        var pendingId = await RegisterPartnerAsync(includeBankInfo: false);
        var approvedId = await RegisterPartnerAsync(includeBankInfo: true);

        await WithUnitOfWorkAsync(async () =>
        {
            await _adminAppService.ApproveAsync(approvedId);
        });

        var pendingList = await _adminAppService.GetListAsync(new GetPartnersInput
        {
            Status = PartnerStatus.Pending,
            MaxResultCount = 100
        });

        pendingList.Items.ShouldContain(p => p.Id == pendingId);
        pendingList.Items.ShouldNotContain(p => p.Id == approvedId);
    }

    private async Task<Guid> RegisterPartnerAsync(bool includeBankInfo)
    {
        var input = new PartnerRegistrationInput
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
            BankInfo = includeBankInfo
                ? new BankInfoDto
                {
                    BankName = "Al Rajhi Bank",
                    AccountHolderName = "Acme Logistics LLC",
                    Iban = "SA0380000000608010167519"
                }
                : null
        };

        PartnerRegistrationResultDto result = null!;
        await WithUnitOfWorkAsync(async () =>
        {
            result = await _registrationAppService.RegisterAsync(input);
        });

        return result.PartnerId;
    }
}
