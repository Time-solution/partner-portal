using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace Zahy.PartnerPlatform.Partners;

/// <summary>
/// Host-level partner aggregate (cross-tenant). Merchant isolation uses ABP tenant id;
/// partner isolation uses the <c>partner_id</c> claim on team members.
/// </summary>
public class Partner : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }

    public PartnerType Type { get; private set; }

    public PartnerStatus Status { get; private set; }

    public string LegalName { get; private set; } = string.Empty;

    public string? TradeName { get; private set; }

    public ContactInfo ContactInfo { get; private set; } = null!;

    public BankInfo BankInfo { get; private set; } = null!;

    /// <summary>Future owner email; no identity user is created until approval.</summary>
    public string PrimaryContactEmail { get; private set; } = string.Empty;

    public string RegistrantName { get; private set; } = string.Empty;

    public string RegistrantPhone { get; private set; } = string.Empty;

    public CloseReason? CloseReason { get; private set; }

    public string? CloseNotes { get; private set; }

    public string? OpenIddictClientId { get; private set; }

    protected Partner()
    {
    }

    public Partner(
        Guid id,
        PartnerType type,
        string legalName,
        string? tradeName,
        ContactInfo contactInfo,
        BankInfo bankInfo,
        string primaryContactEmail,
        string registrantName,
        string registrantPhone)
        : base(id)
    {
        Type = type;
        LegalName = Check.NotNullOrWhiteSpace(legalName, nameof(legalName));
        TradeName = tradeName;
        ContactInfo = Check.NotNull(contactInfo, nameof(contactInfo));
        BankInfo = Check.NotNull(bankInfo, nameof(bankInfo));
        PrimaryContactEmail = Check.NotNullOrWhiteSpace(primaryContactEmail, nameof(primaryContactEmail));
        RegistrantName = Check.NotNullOrWhiteSpace(registrantName, nameof(registrantName));
        RegistrantPhone = Check.NotNullOrWhiteSpace(registrantPhone, nameof(registrantPhone));
        Status = PartnerStatus.Pending;
        TenantId = null;

        if (!bankInfo.IsEmpty && !SaudiIbanValidator.IsValidOrEmpty(bankInfo.Iban))
        {
            throw new BusinessException("Zahy:PartnerPlatform:InvalidIban")
                .WithData("Iban", bankInfo.Iban);
        }
    }

    public bool HasCompleteBankInfo() => BankInfo.IsComplete;

    internal void SetStatus(PartnerStatus status, string? notes = null)
    {
        Status = status;
        if (notes != null)
        {
            CloseNotes = notes;
        }
    }

    internal void Close(CloseReason reason, string? notes)
    {
        Status = PartnerStatus.Closed;
        CloseReason = reason;
        CloseNotes = notes;
    }

    public void AssignOpenIddictClient(string clientId)
    {
        OpenIddictClientId = Check.NotNullOrWhiteSpace(clientId, nameof(clientId));
    }
}
