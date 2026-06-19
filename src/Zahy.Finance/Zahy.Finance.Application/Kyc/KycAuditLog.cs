using System;
using Microsoft.Extensions.Logging;

namespace Zahy.Finance;

public static class KycAuditLog
{
    public static void LogSubmissionCreated(
        ILogger logger,
        Guid submissionId,
        KycEntityKind entityKind,
        Guid entityId,
        int version)
    {
        logger.LogInformation(
            "KYC submission created {SubmissionId} for {EntityKind} entity {EntityId} version {Version}",
            submissionId,
            entityKind,
            entityId,
            version);
    }

    public static void LogVerificationTransition(
        ILogger logger,
        Guid verificationId,
        KycVerificationStatus fromStatus,
        KycVerificationStatus toStatus)
    {
        logger.LogInformation(
            "KYC verification {VerificationId} transitioned {FromStatus} -> {ToStatus}",
            verificationId,
            fromStatus,
            toStatus);
    }
}
