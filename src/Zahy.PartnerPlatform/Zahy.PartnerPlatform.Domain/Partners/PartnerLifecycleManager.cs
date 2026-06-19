using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace Zahy.PartnerPlatform.Partners;

public class PartnerLifecycleManager : DomainService
{
    public void EnsureCanApprove(Partner partner)
    {
        EnsureTransition(partner, PartnerLifecycleAction.Approve);

        if (!partner.HasCompleteBankInfo())
        {
            throw new BusinessException(PartnerPlatformErrorCodes.BankInfoRequiredForApproval)
                .WithData("PartnerId", partner.Id);
        }
    }

    public void Approve(Partner partner)
    {
        EnsureCanApprove(partner);
        partner.SetStatus(PartnerStatus.Active);
    }

    public void Reject(Partner partner, string? notes)
    {
        EnsureTransition(partner, PartnerLifecycleAction.Reject);
        partner.Close(CloseReason.Rejected, notes);
    }

    public void Suspend(Partner partner, string? notes)
    {
        EnsureTransition(partner, PartnerLifecycleAction.Suspend);
        partner.SetStatus(PartnerStatus.Suspended, notes);
    }

    public void Reactivate(Partner partner, string? notes)
    {
        EnsureTransition(partner, PartnerLifecycleAction.Reactivate);
        partner.SetStatus(PartnerStatus.Active, notes);
    }

    public void Close(Partner partner, string? notes)
    {
        EnsureTransition(partner, PartnerLifecycleAction.Close);
        partner.Close(CloseReason.AdminClosed, notes);
    }

    private static void EnsureTransition(Partner partner, PartnerLifecycleAction action)
    {
        if (!PartnerLifecyclePolicy.TryGetTargetStatus(partner.Status, action, out _))
        {
            throw new BusinessException(PartnerPlatformErrorCodes.IllegalLifecycleTransition)
                .WithData("PartnerId", partner.Id)
                .WithData("CurrentStatus", partner.Status.ToString())
                .WithData("Action", action.ToString());
        }
    }
}
