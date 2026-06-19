using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Security.Claims;
using Volo.Abp.Users;
using Xunit;

namespace Zahy.Finance;

public class FinanceKycStep3Tests : ZahyFinanceTestBase
{
    private readonly IKycSubmissionService _submissionService;
    private readonly IKycVerificationService _verificationService;
    private readonly IFinanceAccountService _accountService;
    private readonly IFinanceDocumentKycBlockBuilder _documentKycBlockBuilder;
    private readonly IKycFieldProtector _fieldProtector;
    private readonly ICurrentPrincipalAccessor _principalAccessor;
    private readonly RecordingLoggerProvider _recordingLogger;

    public FinanceKycStep3Tests()
    {
        _submissionService = GetRequiredService<IKycSubmissionService>();
        _verificationService = GetRequiredService<IKycVerificationService>();
        _accountService = GetRequiredService<IFinanceAccountService>();
        _documentKycBlockBuilder = GetRequiredService<IFinanceDocumentKycBlockBuilder>();
        _fieldProtector = GetRequiredService<IKycFieldProtector>();
        _principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
        _recordingLogger = GetRequiredService<RecordingLoggerProvider>();
    }

    [Fact]
    public async Task Illegal_Kyc_Transition_Rejected()
    {
        var partnerId = Guid.NewGuid();
        KycSubmissionResult submission = null!;

        await WithUnitOfWorkAsync(async () =>
        {
            submission = await _submissionService.SubmitAsync(CreateSubmissionRequest(partnerId));
        });

        using (_principalAccessor.Change(CreateReviewerPrincipal()))
        {
            await WithUnitOfWorkAsync(async () =>
            {
                var ex = await Should.ThrowAsync<BusinessException>(() =>
                    _verificationService.VerifyAsync(new KycVerifyRequest
                    {
                        VerificationId = submission.VerificationId,
                        CanonicalFields = CreateCanonicalFields()
                    }));

                ex.Code.ShouldBe(FinanceErrorCodes.IllegalKycTransition);
            });
        }
    }

    [Fact]
    public async Task Account_Does_Not_Open_Until_Verified()
    {
        var partnerId = Guid.NewGuid();
        KycSubmissionResult submission = null!;

        await WithUnitOfWorkAsync(async () =>
        {
            submission = await _submissionService.SubmitAsync(CreateSubmissionRequest(partnerId));

            var ex = await Should.ThrowAsync<BusinessException>(() =>
                _accountService.OpenPartnerAccountAsync(partnerId));

            ex.Code.ShouldBe(FinanceErrorCodes.KycNotVerified);
        });

        using (_principalAccessor.Change(CreateReviewerPrincipal()))
        {
            await WithUnitOfWorkAsync(async () =>
            {
                await _verificationService.StartReviewAsync(submission.VerificationId);
                await _verificationService.VerifyAsync(new KycVerifyRequest
                {
                    VerificationId = submission.VerificationId,
                    CanonicalFields = CreateCanonicalFields()
                });
            });
        }

        await WithUnitOfWorkAsync(async () =>
        {
            var opened = await _accountService.OpenPartnerAccountAsync(partnerId);
            opened.IsNew.ShouldBeTrue();
            opened.Status.ShouldBe(FinanceAccountStatus.Active);
        });
    }

    [Fact]
    public async Task Documents_Use_Verified_Kyc_Not_Raw_Submission()
    {
        var partnerId = Guid.NewGuid();
        const string submittedCr = "1010123456";
        const string verifiedCr = "2020987654";
        const string submittedIban = "SA0380000000608010167519";
        const string verifiedIban = "SA4420000000000012345678";

        KycSubmissionResult submission = null!;

        await WithUnitOfWorkAsync(async () =>
        {
            submission = await _submissionService.SubmitAsync(new KycSubmissionRequest
            {
                EntityKind = KycEntityKind.Partner,
                EntityId = partnerId,
                Payload = new KycSubmissionPayload
                {
                    LegalNameAr = "شركة مقدمة",
                    LegalNameEn = "Submitted Name Ltd",
                    CommercialRegistrationNumber = submittedCr,
                    VatNumber = "300012345600003",
                    Iban = submittedIban,
                    LegalAddress = "Submitted Address Riyadh"
                }
            });
        });

        using (_principalAccessor.Change(CreateReviewerPrincipal()))
        {
            await WithUnitOfWorkAsync(async () =>
            {
                await _verificationService.StartReviewAsync(submission.VerificationId);
                await _verificationService.VerifyAsync(new KycVerifyRequest
                {
                    VerificationId = submission.VerificationId,
                    CanonicalFields = new KycVerifiedCanonicalFields
                    {
                        LegalNameAr = "شركة معتمدة",
                        LegalNameEn = "Verified Name Ltd",
                        CommercialRegistrationNumber = verifiedCr,
                        VatNumber = "300099988877766",
                        Iban = verifiedIban,
                        LegalAddress = "Verified Address Jeddah"
                    }
                });
            });
        }

        FinanceDocumentKycBlockDto block = null!;

        await WithUnitOfWorkAsync(async () =>
        {
            block = await _documentKycBlockBuilder.BuildAsync(KycEntityKind.Partner, partnerId);
        });

        block.CommercialRegistrationNumber.ShouldBe(verifiedCr);
        block.CommercialRegistrationNumber.ShouldNotBe(submittedCr);
        block.Iban.ShouldBe(verifiedIban);
        block.Iban.ShouldNotBe(submittedIban);
        block.LegalNameEn.ShouldBe("Verified Name Ltd");
    }

    [Fact]
    public async Task Resubmission_Creates_New_Append_Only_Submission_Row()
    {
        var partnerId = Guid.NewGuid();

        KycSubmissionResult first = null!;
        KycSubmissionResult second = null!;

        await WithUnitOfWorkAsync(async () =>
        {
            first = await _submissionService.SubmitAsync(CreateSubmissionRequest(partnerId));
            second = await _submissionService.SubmitAsync(CreateSubmissionRequest(partnerId));
        });

        second.Version.ShouldBe(2);
        second.SubmissionId.ShouldNotBe(first.SubmissionId);
        second.VerificationId.ShouldNotBe(first.VerificationId);
    }

    [Fact]
    public void Kyc_Audit_Log_Uses_Identifiers_Only()
    {
        var messages = new List<string>();
        var logger = new RecordingLoggerProvider.RecordingLogger(
            messages,
            "Zahy.Finance.KycSubmissionService");

        var submissionId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        const string sensitiveIban = "SA9922334455667788990011";

        KycAuditLog.LogSubmissionCreated(
            logger,
            submissionId,
            KycEntityKind.Partner,
            entityId,
            version: 1);

        var line = messages.Single();
        line.ShouldContain(submissionId.ToString("D"));
        line.ShouldContain(entityId.ToString("D"));
        line.ShouldNotContain(sensitiveIban);
        line.ToLowerInvariant().ShouldNotContain("iban");
    }

    [Fact]
    public async Task Kyc_Sensitive_Fields_Are_Never_Written_To_Logs()
    {
        _recordingLogger.Messages.Clear();

        var partnerId = Guid.NewGuid();
        const string sensitiveIban = "SA9922334455667788990011";
        const string sensitiveCr = "8899776655";
        const string sensitiveVat = "399988877766655";

        await WithUnitOfWorkAsync(async () =>
        {
            await _submissionService.SubmitAsync(new KycSubmissionRequest
            {
                EntityKind = KycEntityKind.Partner,
                EntityId = partnerId,
                Payload = new KycSubmissionPayload
                {
                    LegalNameAr = "شركة حساسة",
                    LegalNameEn = "Sensitive Entity",
                    CommercialRegistrationNumber = sensitiveCr,
                    VatNumber = sensitiveVat,
                    Iban = sensitiveIban,
                    LegalAddress = "Sensitive Street 1"
                }
            });
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var submission = (await GetRequiredService<IRepository<KycSubmission, Guid>>().GetListAsync())
                .Single(x => x.EntityId == partnerId);
            submission.ProtectedIban.ShouldNotBe(sensitiveIban);
            submission.ProtectedCommercialRegistrationNumber.ShouldNotBe(sensitiveCr);
        });

        var financeKycLogs = _recordingLogger.Messages
            .Where(message => message.StartsWith("[Zahy.Finance.Kyc", StringComparison.Ordinal))
            .ToList();

        financeKycLogs.ShouldNotBeEmpty();

        var combinedLogs = string.Join('\n', financeKycLogs);
        foreach (var line in financeKycLogs)
        {
            (line.Contains("KYC submission created", StringComparison.Ordinal) ||
             line.Contains("KYC verification", StringComparison.Ordinal) &&
             line.Contains("transitioned", StringComparison.Ordinal))
                .ShouldBeTrue($"Unexpected KYC log line: {line}");
        }

        combinedLogs.ShouldNotContain(sensitiveIban);
        combinedLogs.ShouldNotContain(sensitiveCr);
        combinedLogs.ShouldNotContain(sensitiveVat);
        combinedLogs.ShouldNotContain("Sensitive Street");
        combinedLogs.ShouldContain(partnerId.ToString("D"));
    }

    [Fact]
    public void DevAppLayerKycFieldProtector_Is_Swappable_Seams()
    {
        var protectedValue = _fieldProtector.Protect("SA0380000000608010167519");
        protectedValue.ShouldNotBe("SA0380000000608010167519");
        _fieldProtector.Unprotect(protectedValue).ShouldBe("SA0380000000608010167519");
    }

    private static KycSubmissionRequest CreateSubmissionRequest(Guid partnerId) =>
        new()
        {
            EntityKind = KycEntityKind.Partner,
            EntityId = partnerId,
            Payload = new KycSubmissionPayload
            {
                LegalNameAr = "شركة",
                LegalNameEn = "Entity Ltd",
                CommercialRegistrationNumber = "1010111111",
                VatNumber = "300011111111111",
                Iban = "SA0380000000608010167519",
                LegalAddress = "Riyadh"
            }
        };

    private static KycVerifiedCanonicalFields CreateCanonicalFields() =>
        new()
        {
            LegalNameAr = "شركة",
            LegalNameEn = "Entity Ltd",
            CommercialRegistrationNumber = "1010111111",
            VatNumber = "300011111111111",
            Iban = "SA0380000000608010167519",
            LegalAddress = "Riyadh"
        };

    private static ClaimsPrincipal CreateReviewerPrincipal()
    {
        var identity = new ClaimsIdentity("Test");
        identity.AddClaim(new Claim(AbpClaimTypes.UserId, Guid.NewGuid().ToString("D")));
        identity.AddClaim(new Claim(AbpClaimTypes.Role, "admin"));
        return new ClaimsPrincipal(identity);
    }
}
