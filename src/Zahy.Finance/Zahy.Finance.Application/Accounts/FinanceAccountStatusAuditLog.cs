using System;
using Microsoft.Extensions.Logging;

namespace Zahy.Finance;

public static class FinanceAccountStatusAuditLog
{
    public static void LogTransition(
        ILogger logger,
        Guid auditId,
        FinanceAccountKind accountKind,
        Guid accountId,
        FinanceAccountStatus fromStatus,
        FinanceAccountStatus toStatus)
    {
        logger.LogInformation(
            "Finance account {AccountKind} {AccountId} status transitioned {FromStatus} -> {ToStatus} audit {AuditId}",
            accountKind,
            accountId,
            fromStatus,
            toStatus,
            auditId);
    }
}
