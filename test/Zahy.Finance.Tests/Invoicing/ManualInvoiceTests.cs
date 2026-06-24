using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Xunit;
using Zahy.Commission;
using Zahy.Identity.Permissions;
using Zahy.Identity.Roles;

namespace Zahy.Finance;

[Collection(PdfRenderCollection.Name)]
public class ManualInvoiceTests : ZahyFinanceTestBase
{
    private const decimal VatRate = 0.15m;

    private readonly IFinanceDocumentAppService _manualInvoiceAppService;
    private readonly IFinanceInvoiceGenerationService _invoiceGenerationService;
    private readonly IFinanceAccountService _accountService;
    private readonly IBillingChargeService _billingChargeService;
    private readonly IRepository<FinanceDocument, Guid> _documentRepository;
    private readonly IGuidGenerator _guidGenerator;
    private readonly IKycFieldProtector _fieldProtector;
    private readonly TestCurrentPartner _currentPartner;

    public ManualInvoiceTests()
    {
        _manualInvoiceAppService = GetRequiredService<IFinanceDocumentAppService>();
        _invoiceGenerationService = GetRequiredService<IFinanceInvoiceGenerationService>();
        _accountService = GetRequiredService<IFinanceAccountService>();
        _billingChargeService = GetRequiredService<IBillingChargeService>();
        _documentRepository = GetRequiredService<IRepository<FinanceDocument, Guid>>();
        _guidGenerator = GetRequiredService<IGuidGenerator>();
        _fieldProtector = GetRequiredService<IKycFieldProtector>();
        _currentPartner = GetRequiredService<TestCurrentPartner>();
        _currentPartner.Id = null;
    }

    private static CreateManualInvoiceRequest ExternalRequest(
        string idempotencyKey,
        params ManualInvoiceLineDto[] lines) =>
        new()
        {
            RecipientType = FinanceDocumentRecipientType.External,
            RecipientNameOverride = "Acme Consulting LLC",
            Lines = lines.ToList(),
            IdempotencyKey = idempotencyKey,
            Currency = "SAR"
        };

    private static ManualInvoiceLineDto Line(string description, decimal qty, decimal unitInclusive, string? account = null) =>
        new()
        {
            Description = description,
            Quantity = qty,
            UnitPriceInclusive = unitInclusive,
            AccountCode = account
        };

    [Fact]
    public async Task ManualInvoice_Persists_All_Line_Items()
    {
        FinanceDocumentDto dto = null!;
        await WithUnitOfWorkAsync(async () =>
        {
            dto = await _manualInvoiceAppService.CreateManualInvoiceAsync(ExternalRequest(
                "manual:persist-lines",
                Line("Foo", 1m, 100m),
                Line("Bar", 2m, 50m),
                Line("Baz", 1m, 200m)));
        });

        dto.Source.ShouldBe(FinanceDocumentSource.Manual);
        dto.Lines.Count.ShouldBe(3);
        dto.Lines.Select(l => l.LineNo).ShouldBe(new[] { 1, 2, 3 });
        dto.Lines[0].Description.ShouldBe("Foo");
        dto.Lines[1].LineTotalInclusive.ShouldBe(100.00m);
        dto.Lines.All(l => l.AccountCode == FinanceConsts.DefaultManualLineAccountCode).ShouldBeTrue();

        await WithUnitOfWorkAsync(async () =>
        {
            var persisted = await _documentRepository.GetAsync(dto.Id);
            persisted.Lines.Count.ShouldBe(3);
            persisted.Source.ShouldBe(FinanceDocumentSource.Manual);
        });
    }

    [Fact]
    public async Task ManualInvoice_PostingSum_Equals_Sum_Of_Lines()
    {
        FinanceDocumentDto dto = null!;
        await WithUnitOfWorkAsync(async () =>
        {
            dto = await _manualInvoiceAppService.CreateManualInvoiceAsync(ExternalRequest(
                "manual:postingsum",
                Line("Foo", 1m, 100m),
                Line("Bar", 2m, 50m),
                Line("Baz", 1m, 200m)));
        });

        var expected = dto.Lines.Sum(l => l.LineTotalInclusive);
        dto.PostingSum.ShouldBe(expected);
        dto.GrandTotalInclusive.ShouldBe(expected);
        dto.PostingSum.ShouldBe(400.00m);
    }

    [Fact]
    public async Task ManualInvoice_VatSplit_Matches_FinanceVat_Per_Line()
    {
        FinanceDocumentDto dto = null!;
        await WithUnitOfWorkAsync(async () =>
        {
            dto = await _manualInvoiceAppService.CreateManualInvoiceAsync(ExternalRequest(
                "manual:vatsplit",
                Line("Foo", 1m, 100m),
                Line("Bar", 2m, 50m),
                Line("Baz", 1m, 200m)));
        });

        foreach (var line in dto.Lines)
        {
            line.VatNet.ShouldBe(FinanceVat.NetOfInclusive(line.LineTotalInclusive, VatRate));
            line.VatAmount.ShouldBe(FinanceVat.VatOfInclusive(line.LineTotalInclusive, VatRate));
            (line.VatNet + line.VatAmount).ShouldBe(line.LineTotalInclusive);
        }

        dto.SubtotalNet.ShouldBe(347.83m);
        dto.VatTotal.ShouldBe(52.17m);
    }

    [Fact]
    public async Task ManualInvoice_Rejects_Zero_Lines()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var ex = await Should.ThrowAsync<BusinessException>(() =>
                _manualInvoiceAppService.CreateManualInvoiceAsync(new CreateManualInvoiceRequest
                {
                    RecipientType = FinanceDocumentRecipientType.External,
                    RecipientNameOverride = "Acme",
                    Lines = new List<ManualInvoiceLineDto>(),
                    IdempotencyKey = "manual:zero-lines"
                }));
            ex.Code.ShouldBe(FinanceErrorCodes.ManualInvoiceInvalidLines);
        });
    }

    [Fact]
    public async Task ManualInvoice_Rejects_NonPositive_Quantity()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var ex = await Should.ThrowAsync<BusinessException>(() =>
                _manualInvoiceAppService.CreateManualInvoiceAsync(ExternalRequest(
                    "manual:bad-qty",
                    Line("Foo", 0m, 100m))));
            ex.Code.ShouldBe(FinanceErrorCodes.ManualInvoiceInvalidLines);
        });
    }

    [Fact]
    public async Task ManualInvoice_Rejects_NonPositive_UnitPrice()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var ex = await Should.ThrowAsync<BusinessException>(() =>
                _manualInvoiceAppService.CreateManualInvoiceAsync(ExternalRequest(
                    "manual:bad-unit",
                    Line("Foo", 1m, 0m))));
            ex.Code.ShouldBe(FinanceErrorCodes.ManualInvoiceInvalidLines);
        });
    }

    [Fact]
    public async Task ManualInvoice_Idempotent_Same_Key_Returns_Same_Document()
    {
        FinanceDocumentDto first = null!;
        FinanceDocumentDto second = null!;

        await WithUnitOfWorkAsync(async () =>
        {
            first = await _manualInvoiceAppService.CreateManualInvoiceAsync(ExternalRequest(
                "manual:idem", Line("Foo", 1m, 115m)));
        });
        await WithUnitOfWorkAsync(async () =>
        {
            second = await _manualInvoiceAppService.CreateManualInvoiceAsync(ExternalRequest(
                "manual:idem", Line("Foo", 1m, 115m)));
        });

        second.Id.ShouldBe(first.Id);
        second.InvoiceNumber.ShouldBe(first.InvoiceNumber);

        await WithUnitOfWorkAsync(async () =>
        {
            (await _documentRepository.GetCountAsync()).ShouldBe(1);
        });
    }

    [Fact]
    public void ManualInvoice_Permission_WriteManualInvoice_Required()
    {
        var policy = GetCreateManualInvoiceAuthorizePolicy();
        policy.ShouldBe(ZahyPermissions.Finance.WriteManualInvoice);

        // Granted ONLY to the accountant + super admin; never partner / merchant.
        var finance = ZahyRoleRegistry.Find(ZahyRoles.PlatformFinance);
        finance!.Permissions.ShouldContain(ZahyPermissions.Finance.WriteManualInvoice);
        ZahyPermissions.All().ShouldContain(ZahyPermissions.Finance.WriteManualInvoice);

        foreach (var roleName in new[]
                 {
                     ZahyRoles.PartnerOwner, ZahyRoles.PartnerManager, ZahyRoles.PartnerStaff,
                     ZahyRoles.MerchantOwner, ZahyRoles.MerchantManager, ZahyRoles.MerchantStaff, ZahyRoles.MerchantViewer
                 })
        {
            var role = ZahyRoleRegistry.Find(roleName);
            role!.Permissions.ShouldNotContain(ZahyPermissions.Finance.WriteManualInvoice);
        }
    }

    [Fact]
    public void ManualInvoice_Permission_FinanceReadAll_NotSufficient()
    {
        // The write gate is WriteManualInvoice, NOT ReadAll — so a principal holding only ReadAll cannot author.
        var policy = GetCreateManualInvoiceAuthorizePolicy();
        policy.ShouldNotBe(ZahyPermissions.Finance.ReadAll);
        policy.ShouldBe(ZahyPermissions.Finance.WriteManualInvoice);
        ZahyPermissions.Finance.WriteManualInvoice.ShouldNotBe(ZahyPermissions.Finance.ReadAll);
    }

    [Fact]
    public void ManualInvoice_TotalsImbalanced_ThrowsCode_080()
    {
        var lines = new[]
        {
            FinanceInvoiceLine.Create(1, "Foo", 1m, 100m, VatRate)
        };

        // Declared PostingSum (999) deliberately disagrees with the line total (100) → invariant must fire.
        var ex = Should.Throw<BusinessException>(() =>
            FinanceDocument.CreateManualInvoice(
                _guidGenerator.Create(),
                FinanceDocumentRecipientType.External,
                null,
                "Acme",
                "manual:imbalance",
                FinanceInvoiceNumberFormat.Format(DateTime.UtcNow.Year, 1),
                DateTime.UtcNow.Year,
                1,
                lines,
                declaredPostingSum: 999m,
                DateTime.UtcNow));

        ex.Code.ShouldBe(FinanceErrorCodes.ManualInvoiceTotalsImbalanced);
    }

    [Fact]
    public async Task ManualInvoice_LedgerDerived_Path_Unchanged()
    {
        var partnerId = Guid.NewGuid();
        await SeedPartnerWithBillingAsync(partnerId, 25m);
        _currentPartner.Id = partnerId;

        FinanceInvoiceGenerationResult result = null!;
        await WithUnitOfWorkAsync(async () =>
        {
            result = await _invoiceGenerationService.GeneratePartnerInvoiceOnDemandAsync(
                new FinanceInvoiceRequest { PartnerId = partnerId });
        });

        result.IsNew.ShouldBeTrue();
        result.InvoiceNumber.ShouldStartWith("ZAHY-INV-");
        result.PostingSum.ShouldBe(25.00m);

        await WithUnitOfWorkAsync(async () =>
        {
            var document = (await _documentRepository.GetListAsync()).Single();
            document.Source.ShouldBe(FinanceDocumentSource.LedgerDerived);
            document.Lines.ShouldBeEmpty();
            document.Recipient.ShouldBeNull();
        });
    }

    [Fact]
    public async Task ManualInvoice_Pdf_RendersFromLines_NotFromLedger()
    {
        // External recipient has NO finance account / ledger. If GetPdf took the ledger path it would throw
        // AccountNotFound; succeeding with a non-empty PDF proves it rendered from the stored Lines.
        FinanceDocumentDto dto = null!;
        await WithUnitOfWorkAsync(async () =>
        {
            dto = await _manualInvoiceAppService.CreateManualInvoiceAsync(ExternalRequest(
                "manual:pdf", Line("Consulting", 1m, 1150m)));
        });

        FinanceDocumentBytesResult pdf = null!;
        await WithUnitOfWorkAsync(async () =>
        {
            pdf = await _manualInvoiceAppService.GetPdfAsync(dto.Id);
        });

        pdf.Content.ShouldNotBeEmpty();
        pdf.ContentType.ShouldBe("application/pdf");
        pdf.PostingSum.ShouldBe(1150.00m);
    }

    [Fact]
    public async Task ManualInvoice_RecipientResolution_ExternalUsesOverride()
    {
        FinanceDocumentDto dto = null!;
        await WithUnitOfWorkAsync(async () =>
        {
            dto = await _manualInvoiceAppService.CreateManualInvoiceAsync(new CreateManualInvoiceRequest
            {
                RecipientType = FinanceDocumentRecipientType.External,
                RecipientNameOverride = "External Override Co",
                Lines = new List<ManualInvoiceLineDto> { Line("Fee", 1m, 115m) },
                IdempotencyKey = "manual:external-name"
            });
        });

        dto.Recipient.ShouldBe("External Override Co");
        dto.RecipientType.ShouldBe(FinanceDocumentRecipientType.External);
    }

    [Fact]
    public async Task ManualInvoice_RecipientResolution_PartnerLookedUpByRef()
    {
        var partnerId = Guid.NewGuid();
        await WithUnitOfWorkAsync(async () => await SeedPartnerVerifiedKycAsync(partnerId));

        FinanceDocumentDto dto = null!;
        await WithUnitOfWorkAsync(async () =>
        {
            dto = await _manualInvoiceAppService.CreateManualInvoiceAsync(new CreateManualInvoiceRequest
            {
                RecipientType = FinanceDocumentRecipientType.Partner,
                RecipientReference = partnerId,
                Lines = new List<ManualInvoiceLineDto> { Line("Onboarding", 1m, 1150m) },
                IdempotencyKey = "manual:partner-lookup"
            });
        });

        dto.Recipient.ShouldBe("Verified Partner Ltd");
        dto.RecipientType.ShouldBe(FinanceDocumentRecipientType.Partner);
        dto.RecipientReference.ShouldBe(partnerId);
    }

    private static string? GetCreateManualInvoiceAuthorizePolicy()
    {
        var method = typeof(FinanceManualInvoiceAppService)
            .GetMethod(nameof(IFinanceDocumentAppService.CreateManualInvoiceAsync));
        method.ShouldNotBeNull();

        var authorize = method!
            .GetCustomAttributes(inherit: true)
            .FirstOrDefault(a => a.GetType().FullName == "Microsoft.AspNetCore.Authorization.AuthorizeAttribute");
        authorize.ShouldNotBeNull();

        var policyProperty = authorize!.GetType().GetProperty("Policy");
        return policyProperty!.GetValue(authorize) as string;
    }

    private async Task SeedPartnerWithBillingAsync(Guid partnerId, decimal billingAmount)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            await SeedPartnerVerifiedKycAsync(partnerId);
            await _accountService.OpenPartnerAccountAsync(partnerId);

            if (billingAmount > 0)
            {
                await _billingChargeService.ChargeAsync(new BillingChargeRequest
                {
                    PartnerId = partnerId,
                    ChargeTarget = BillingChargeTarget.Partner,
                    Kind = BillingChargeKind.Subscription,
                    Amount = billingAmount,
                    IdempotencyKey = $"billing:seed:{partnerId:N}"
                });
            }
        });
    }

    private async Task SeedPartnerVerifiedKycAsync(Guid partnerId)
    {
        var submissionRepo = GetRequiredService<IRepository<KycSubmission, Guid>>();
        var verificationRepo = GetRequiredService<IRepository<KycVerification, Guid>>();
        var submissionId = _guidGenerator.Create();
        var verificationId = _guidGenerator.Create();

        await submissionRepo.InsertAsync(
            KycSubmission.Create(
                submissionId,
                KycEntityKind.Partner,
                partnerId,
                version: 1,
                submittedByUserId: null,
                submittedAt: DateTime.UtcNow,
                new ProtectedKycFieldBundle
                {
                    LegalNameAr = _fieldProtector.Protect("Submitted AR"),
                    LegalNameEn = _fieldProtector.Protect("Submitted EN"),
                    CommercialRegistrationNumber = _fieldProtector.Protect("1010111111"),
                    VatNumber = _fieldProtector.Protect("300011111111111"),
                    Iban = _fieldProtector.Protect("SA9922334455667788990011"),
                    LegalAddress = _fieldProtector.Protect("Submitted Address")
                }),
            autoSave: true);

        var verification = KycVerification.CreateForSubmission(
            verificationId,
            KycEntityKind.Partner,
            partnerId,
            submissionId);
        verification.MarkUnderReview(Guid.NewGuid(), DateTime.UtcNow);
        verification.MarkVerified(new KycVerifiedCanonicalFields
        {
            LegalNameAr = "شركة معتمدة",
            LegalNameEn = "Verified Partner Ltd",
            CommercialRegistrationNumber = "2020222222",
            VatNumber = "300099988877766",
            Iban = "SA4420000000000012345678",
            LegalAddress = "Verified Address"
        }, Guid.NewGuid(), DateTime.UtcNow);

        await verificationRepo.InsertAsync(verification, autoSave: true);
    }
}
