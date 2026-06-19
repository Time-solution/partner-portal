using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Zahy.PartnerPlatform.Partners;

namespace Zahy.Commission;

public class CommissionRule : FullAuditedAggregateRoot<Guid>
{
    public string Name { get; private set; } = string.Empty;

    public bool IsEnabled { get; private set; }

    public CommissionDirection Direction { get; private set; }

    public CommissionTriggerType TriggerType { get; private set; }

    /// <summary>Fee dimension — at most one winning rule per fee type per order event.</summary>
    public CommissionFeeType FeeType { get; private set; }

    /// <summary>Default Subtotal; override only when explicitly configured on the rule.</summary>
    public CommissionBasisAmountKind BasisAmountKind { get; private set; } = CommissionBasisAmountKind.Subtotal;

    public CommissionScopeKind ScopeKind { get; private set; }

    public Guid? ScopePartnerId { get; private set; }

    public PartnerType? ScopePartnerType { get; private set; }

    public string? ScopeCategoryCode { get; private set; }

    public string? ScopeProductSku { get; private set; }

    public string BasisDefinitionJson { get; private set; } = "{}";

    public DateTime EffectiveFromUtc { get; private set; }

    public DateTime? EffectiveToUtc { get; private set; }

    public int Priority { get; private set; }

    protected CommissionRule()
    {
    }

    public CommissionRule(
        Guid id,
        string name,
        CommissionDirection direction,
        CommissionTriggerType triggerType,
        CommissionScopeKind scopeKind,
        CommissionBasisDefinition basisDefinition,
        DateTime effectiveFromUtc,
        CommissionFeeType feeType,
        int priority = 0,
        CommissionBasisAmountKind basisAmountKind = CommissionBasisAmountKind.Subtotal,
        Guid? scopePartnerId = null,
        PartnerType? scopePartnerType = null,
        string? scopeCategoryCode = null,
        string? scopeProductSku = null,
        DateTime? effectiveToUtc = null)
    {
        Id = id;
        SetName(name);
        Direction = direction;
        TriggerType = triggerType;
        FeeType = feeType;
        BasisAmountKind = basisAmountKind;
        ScopeKind = scopeKind;
        SetScope(scopeKind, scopePartnerId, scopePartnerType, scopeCategoryCode, scopeProductSku);
        SetBasisDefinition(basisDefinition);
        EffectiveFromUtc = effectiveFromUtc;
        EffectiveToUtc = effectiveToUtc;
        Priority = priority;
        IsEnabled = true;
    }

    public CommissionBasisDefinition GetBasisDefinition() =>
        CommissionBasisDefinitionJson.Parse(BasisDefinitionJson);

    public CommissionRuleSnapshot ToSnapshot() =>
        new()
        {
            Id = Id,
            FeeType = FeeType,
            BasisAmountKind = BasisAmountKind,
            ScopeKind = ScopeKind,
            Priority = Priority,
            BasisDefinition = GetBasisDefinition()
        };

    public void SetBasisDefinition(CommissionBasisDefinition basisDefinition)
    {
        Check.NotNull(basisDefinition, nameof(basisDefinition));
        ValidateBasisDefinition(basisDefinition);
        BasisDefinitionJson = CommissionBasisDefinitionJson.ToJson(basisDefinition);
    }

    public void SetName(string name)
    {
        Check.NotNullOrWhiteSpace(name, nameof(name));
        if (name.Length > CommissionConsts.MaxRuleNameLength)
        {
            throw new BusinessException(CommissionErrorCodes.InvalidCommissionRule);
        }

        Name = name.Trim();
    }

    private static void ValidateBasisDefinition(CommissionBasisDefinition basisDefinition)
    {
        if (basisDefinition.FlatFee == null &&
            basisDefinition.PercentageRate == null &&
            basisDefinition.Tiered == null)
        {
            throw new BusinessException(CommissionErrorCodes.InvalidBasisDefinition);
        }

        if (basisDefinition.Tiered?.Tiers.Count == 0)
        {
            throw new BusinessException(CommissionErrorCodes.InvalidBasisDefinition);
        }
    }

    private void SetScope(
        CommissionScopeKind scopeKind,
        Guid? scopePartnerId,
        PartnerType? scopePartnerType,
        string? scopeCategoryCode,
        string? scopeProductSku)
    {
        switch (scopeKind)
        {
            case CommissionScopeKind.Partner:
                if (scopePartnerId == null || scopePartnerId == Guid.Empty)
                {
                    throw new BusinessException(CommissionErrorCodes.InvalidCommissionRule);
                }

                ScopePartnerId = scopePartnerId;
                break;
            case CommissionScopeKind.PartnerType:
                if (scopePartnerType == null)
                {
                    throw new BusinessException(CommissionErrorCodes.InvalidCommissionRule);
                }

                ScopePartnerType = scopePartnerType;
                break;
            case CommissionScopeKind.Category:
                if (string.IsNullOrWhiteSpace(scopeCategoryCode))
                {
                    throw new BusinessException(CommissionErrorCodes.InvalidCommissionRule);
                }

                ScopeCategoryCode = scopeCategoryCode.Trim();
                break;
            case CommissionScopeKind.Product:
                if (string.IsNullOrWhiteSpace(scopeProductSku))
                {
                    throw new BusinessException(CommissionErrorCodes.InvalidCommissionRule);
                }

                ScopeProductSku = scopeProductSku.Trim();
                break;
            default:
                throw new BusinessException(CommissionErrorCodes.InvalidCommissionRule);
        }
    }
}
