using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Xunit;
using Zahy.Commission;

namespace Zahy.Finance;

public class FinanceAccountStep2Tests : ZahyFinanceTestBase
{
    private readonly IFinanceAccountService _accountService;
    private readonly IFinanceAccountQueryService _accountQueryService;
    private readonly ICommissionLedgerService _ledgerService;
    private readonly IBillingChargeService _billingChargeService;
    private readonly IRepository<KycVerification, Guid> _kycVerificationRepository;
    private readonly IRepository<KycSubmission, Guid> _kycSubmissionRepository;
    private readonly IRepository<AccountPosting, Guid> _postingRepository;
    private readonly IRepository<PartnerFinancialAccount, Guid> _partnerAccountRepository;
    private readonly IGuidGenerator _guidGenerator;
    private readonly TestCurrentPartner _currentPartner;

    public FinanceAccountStep2Tests()
    {
        _accountService = GetRequiredService<IFinanceAccountService>();
        _accountQueryService = GetRequiredService<IFinanceAccountQueryService>();
        _ledgerService = GetRequiredService<ICommissionLedgerService>();
        _billingChargeService = GetRequiredService<IBillingChargeService>();
        _kycVerificationRepository = GetRequiredService<IRepository<KycVerification, Guid>>();
        _kycSubmissionRepository = GetRequiredService<IRepository<KycSubmission, Guid>>();
        _postingRepository = GetRequiredService<IRepository<AccountPosting, Guid>>();
        _partnerAccountRepository = GetRequiredService<IRepository<PartnerFinancialAccount, Guid>>();
        _guidGenerator = GetRequiredService<IGuidGenerator>();
        _currentPartner = GetRequiredService<TestCurrentPartner>();
        _currentPartner.Id = null;
    }

    [Fact]
    public async Task Opens_Once_On_Verified()
    {
        var partnerId = Guid.NewGuid();

        await WithUnitOfWorkAsync(async () =>
        {
            await SeedVerifiedPartnerKycAsync(partnerId);
        });

        FinanceAccountOpenResult first = null!;
        FinanceAccountOpenResult second = null!;

        await WithUnitOfWorkAsync(async () =>
        {
            first = await _accountService.OpenPartnerAccountAsync(partnerId);
            first.IsNew.ShouldBeTrue();
            first.Status.ShouldBe(FinanceAccountStatus.Active);
        });

        await WithUnitOfWorkAsync(async () =>
        {
            second = await _accountService.OpenPartnerAccountAsync(partnerId);
            second.IsNew.ShouldBeFalse();
            second.AccountId.ShouldBe(first.AccountId);
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var accounts = await _partnerAccountRepository.GetListAsync();
            accounts.Count(x => x.PartnerId == partnerId).ShouldBe(1);
        });
    }

    [Fact]
    public async Task Blocked_If_Not_Verified()
    {
        var partnerId = Guid.NewGuid();

        await WithUnitOfWorkAsync(async () =>
        {
            var ex = await Should.ThrowAsync<BusinessException>(() =>
                _accountService.OpenPartnerAccountAsync(partnerId));

            ex.Code.ShouldBe(FinanceErrorCodes.KycNotVerified);
        });
    }

    [Fact]
    public async Task Postings_Are_Append_Only()
    {
        typeof(AccountPosting)
            .GetProperty(nameof(AccountPosting.PostingAmount))!
            .SetMethod!
            .IsPublic.ShouldBeFalse();

        var partnerId = Guid.NewGuid();
        var ruleId = Guid.NewGuid();

        await WithUnitOfWorkAsync(async () =>
        {
            await SeedVerifiedPartnerKycAsync(partnerId);
            await _accountService.OpenPartnerAccountAsync(partnerId);
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var request = CreateAccrualRequest(partnerId, ruleId, 100m, 10m, CommissionDirection.PlatformEarns);
            await _ledgerService.AccrueAsync(request);
            await _ledgerService.AccrueAsync(request);
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var postings = await _postingRepository.GetListAsync();
            postings.Count.ShouldBe(1);
            postings.Single().PostingAmount.ShouldBe(10m);
        });
    }

    [Fact]
    public async Task Balance_Equals_Sum_Of_Postings_Exactly()
    {
        var partnerId = Guid.NewGuid();
        var platformRuleId = Guid.NewGuid();
        var partnerRuleId = Guid.NewGuid();

        await WithUnitOfWorkAsync(async () =>
        {
            await SeedVerifiedPartnerKycAsync(partnerId);
            await _accountService.OpenPartnerAccountAsync(partnerId);

            await _ledgerService.AccrueAsync(CreateAccrualRequest(
                partnerId,
                platformRuleId,
                100m,
                10m,
                CommissionDirection.PlatformEarns,
                sourceId: "balance-platform"));

            await _ledgerService.AccrueAsync(CreateAccrualRequest(
                partnerId,
                partnerRuleId,
                50m,
                5m,
                CommissionDirection.PartnerEarns,
                sourceId: "balance-partner"));

            await _billingChargeService.ChargeAsync(new BillingChargeRequest
            {
                PartnerId = partnerId,
                ChargeTarget = BillingChargeTarget.Partner,
                Kind = BillingChargeKind.ActivationFee,
                Amount = 20m,
                IdempotencyKey = BillingIdempotency.BuildActivationKey(partnerId)
            });
        });

        await WithUnitOfWorkAsync(async () =>
        {
            _currentPartner.Id = partnerId;

            var postings = await _postingRepository.GetListAsync();
            var expected = postings.Sum(x => x.PostingAmount);
            expected.ShouldBe(25m);

            var balance = await _accountQueryService.GetPartnerBalanceAsync(partnerId);
            balance.Balance.ShouldBe(expected);
            balance.Balance.ShouldBe(25m);
        });
    }

    [Fact]
    public async Task Partner_Cannot_See_Other_Partner_Account()
    {
        var partnerA = Guid.NewGuid();
        var partnerB = Guid.NewGuid();

        await WithUnitOfWorkAsync(async () =>
        {
            await SeedVerifiedPartnerKycAsync(partnerA);
            await _accountService.OpenPartnerAccountAsync(partnerA);
        });

        _currentPartner.Id = partnerB;

        await WithUnitOfWorkAsync(async () =>
        {
            await Should.ThrowAsync<AbpAuthorizationException>(async () =>
            {
                await _accountQueryService.GetPartnerBalanceAsync(partnerA);
            });
        });
    }

    private async Task SeedVerifiedPartnerKycAsync(Guid partnerId)
    {
        var protector = GetRequiredService<IKycFieldProtector>();
        var submissionId = _guidGenerator.Create();
        await _kycSubmissionRepository.InsertAsync(
            KycSubmission.Create(
                submissionId,
                KycEntityKind.Partner,
                partnerId,
                version: 1,
                submittedByUserId: null,
                submittedAt: DateTime.UtcNow,
                new ProtectedKycFieldBundle
                {
                    LegalNameAr = protector.Protect("Test Partner AR"),
                    LegalNameEn = protector.Protect("Test Partner EN"),
                    CommercialRegistrationNumber = protector.Protect("1010999999"),
                    VatNumber = protector.Protect("300099999999999"),
                    Iban = protector.Protect("SA0380000000608010167519"),
                    LegalAddress = protector.Protect("Riyadh")
                }),
            autoSave: true);

        await _kycVerificationRepository.InsertAsync(
            KycVerification.CreateVerified(
                _guidGenerator.Create(),
                KycEntityKind.Partner,
                partnerId,
                submissionId,
                DateTime.UtcNow),
            autoSave: true);
    }

    private static CommissionAccrualRequest CreateAccrualRequest(
        Guid partnerId,
        Guid ruleId,
        decimal basisAmount,
        decimal computedCommission,
        CommissionDirection direction,
        string sourceType = "order.paid",
        string? sourceId = null) =>
        new()
        {
            PartnerId = partnerId,
            TenantId = Guid.NewGuid(),
            RuleId = ruleId,
            SourceType = sourceType,
            SourceId = sourceId ?? $"connector:aggregator:mock:{ruleId:N}",
            OrderRecordId = Guid.NewGuid(),
            BasisAmount = basisAmount,
            ComputedCommission = computedCommission,
            Direction = direction,
            Currency = CommissionConsts.DefaultCurrency
        };
}
