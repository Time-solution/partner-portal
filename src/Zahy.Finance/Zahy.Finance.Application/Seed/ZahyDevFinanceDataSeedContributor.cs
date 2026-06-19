using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Uow;
using Zahy.Commission;

namespace Zahy.Finance;

/// <summary>
/// Dev-only finance account with verified KYC and sample billing postings for the dev partner user.
/// </summary>
public class ZahyDevFinanceDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    private readonly IConfiguration _configuration;
    private readonly IFinanceAccountService _accountService;
    private readonly IBillingChargeService _billingChargeService;
    private readonly IRepository<FinancePlatformSettings, Guid> _settingsRepository;
    private readonly IRepository<KycSubmission, Guid> _submissionRepository;
    private readonly IRepository<KycVerification, Guid> _verificationRepository;
    private readonly IRepository<PartnerFinancialAccount, Guid> _partnerAccountRepository;
    private readonly IKycFieldProtector _fieldProtector;
    private readonly IGuidGenerator _guidGenerator;

    public ZahyDevFinanceDataSeedContributor(
        IConfiguration configuration,
        IFinanceAccountService accountService,
        IBillingChargeService billingChargeService,
        IRepository<FinancePlatformSettings, Guid> settingsRepository,
        IRepository<KycSubmission, Guid> submissionRepository,
        IRepository<KycVerification, Guid> verificationRepository,
        IRepository<PartnerFinancialAccount, Guid> partnerAccountRepository,
        IKycFieldProtector fieldProtector,
        IGuidGenerator guidGenerator)
    {
        _configuration = configuration;
        _accountService = accountService;
        _billingChargeService = billingChargeService;
        _settingsRepository = settingsRepository;
        _submissionRepository = submissionRepository;
        _verificationRepository = verificationRepository;
        _partnerAccountRepository = partnerAccountRepository;
        _fieldProtector = fieldProtector;
        _guidGenerator = guidGenerator;
    }

    [UnitOfWork]
    public virtual async Task SeedAsync(DataSeedContext context)
    {
        if (!IsDevSeedAllowed())
        {
            return;
        }

        await EnsurePlatformSettingsAsync();
        await EnsureDevPartnerFinanceAccountAsync();
    }

    private async Task EnsurePlatformSettingsAsync()
    {
        var existing = await _settingsRepository.FindAsync(FinanceConsts.PlatformSettingsId);
        if (existing != null)
        {
            return;
        }

        await _settingsRepository.InsertAsync(
            FinancePlatformSettings.CreateDefault(FinanceConsts.PlatformSettingsId),
            autoSave: true);
    }

    private async Task EnsureDevPartnerFinanceAccountAsync()
    {
        var partnerId = FinanceConsts.DevPartnerId;
        var accounts = await _partnerAccountRepository.GetQueryableAsync();
        if (accounts.Any(x => x.PartnerId == partnerId))
        {
            return;
        }

        await SeedVerifiedPartnerKycAsync(partnerId);
        await _accountService.OpenPartnerAccountAsync(partnerId);
        await _billingChargeService.ChargeAsync(new BillingChargeRequest
        {
            PartnerId = partnerId,
            ChargeTarget = BillingChargeTarget.Partner,
            Kind = BillingChargeKind.Subscription,
            Amount = 49m,
            IdempotencyKey = "billing:dev-partner-subscription",
            Description = "Dev subscription charge"
        });
    }

    private async Task SeedVerifiedPartnerKycAsync(Guid partnerId)
    {
        var submissionId = _guidGenerator.Create();
        var verificationId = _guidGenerator.Create();

        await _submissionRepository.InsertAsync(
            KycSubmission.Create(
                submissionId,
                KycEntityKind.Partner,
                partnerId,
                version: 1,
                submittedByUserId: null,
                submittedAt: DateTime.UtcNow,
                new ProtectedKycFieldBundle
                {
                    LegalNameAr = _fieldProtector.Protect("شركة زاهي للتطوير"),
                    LegalNameEn = _fieldProtector.Protect("Zahy Dev Partner LLC"),
                    CommercialRegistrationNumber = _fieldProtector.Protect("1010999888"),
                    VatNumber = _fieldProtector.Protect("300099988877766"),
                    Iban = _fieldProtector.Protect("SA4420000000000012345678"),
                    LegalAddress = _fieldProtector.Protect("Riyadh, Saudi Arabia")
                }),
            autoSave: true);

        var verification = KycVerification.CreateForSubmission(
            verificationId,
            KycEntityKind.Partner,
            partnerId,
            submissionId);
        verification.MarkUnderReview(Guid.NewGuid(), DateTime.UtcNow);
        verification.MarkVerified(
            new KycVerifiedCanonicalFields
            {
                LegalNameAr = "شركة زاهي للتطوير",
                LegalNameEn = "Zahy Dev Partner LLC",
                CommercialRegistrationNumber = "1010999888",
                VatNumber = "300099988877766",
                Iban = "SA4420000000000012345678",
                LegalAddress = "Riyadh, Saudi Arabia"
            },
            Guid.NewGuid(),
            DateTime.UtcNow);

        await _verificationRepository.InsertAsync(verification, autoSave: true);
    }

    private bool IsDevSeedAllowed()
    {
        if (!_configuration.GetValue<bool>("Zahy:DevSeed:Enabled"))
        {
            return false;
        }

        var environment = _configuration["ASPNETCORE_ENVIRONMENT"]
                          ?? _configuration["DOTNET_ENVIRONMENT"]
                          ?? Environments.Production;

        return string.Equals(environment, Environments.Development, StringComparison.OrdinalIgnoreCase);
    }
}
