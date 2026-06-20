using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Zahy.Settlement;

namespace Zahy.PartnerCatalog;

/// <summary>
/// Append-only buy/sell pair carrier for the settlement bridge (2b/2c). No settlement dispatch in 2a.
/// VAT: Principal only — feeds <see cref="ResaleVatCalculator"/> in 2b/2c.
/// </summary>
public class SettlementCostMarkupSnapshot : Entity<Guid>
{
    public Guid MerchantActivationId { get; private set; }

    public Guid PartnerCatalogItemId { get; private set; }

    public Guid PartnerId { get; private set; }

    public Guid TenantId { get; private set; }

    public decimal BuyPriceAmount { get; private set; }

    public string BuyPriceCurrency { get; private set; } = PartnerCatalogConsts.DefaultCurrency;

    public bool BuyPriceVatInclusive { get; private set; }

    public decimal SellPriceAmount { get; private set; }

    public string SellPriceCurrency { get; private set; } = PartnerCatalogConsts.DefaultCurrency;

    public bool SellPriceVatInclusive { get; private set; }

    /// <summary>FnB sell-leg basis pending accountant (§11 A2) — default Unresolved; amount stored without asserting listing vs actual.</summary>
    public PartnerCatalogSellPriceSource SellPriceSource { get; private set; }

    public SettlementBook SettlementBook { get; private set; }

    /// <summary>Principal for all partner types — feeds settlement engine in 2b/2c.</summary>
    public VatTreatment VatTreatment { get; private set; }

    public SettlementCostMarkupTrigger Trigger { get; private set; }

    public string ExternalTransactionId { get; private set; } = string.Empty;

    /// <summary>Normalized storage key component; empty string when not a line trigger (never null in DB).</summary>
    public string OrderLineId { get; private set; } = string.Empty;

    public Guid? SettlementCaseId { get; private set; }

    public Guid? BillingChargeId { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public Money BuyPrice =>
        PartnerCatalogMoneyAssignment.Read(BuyPriceAmount, BuyPriceCurrency, BuyPriceVatInclusive);

    public Money SellPrice =>
        PartnerCatalogMoneyAssignment.Read(SellPriceAmount, SellPriceCurrency, SellPriceVatInclusive);

    protected SettlementCostMarkupSnapshot()
    {
    }

    public static SettlementCostMarkupSnapshot Create(
        Guid id,
        MerchantActivation activation,
        PartnerCatalogItem catalogItem,
        Money sellPrice,
        SettlementCostMarkupTrigger trigger,
        string externalTransactionId,
        string? orderLineId = null,
        PartnerCatalogSellPriceSource sellPriceSource = PartnerCatalogSellPriceSource.Unresolved,
        DateTime? createdAt = null)
    {
        Check.NotNull(activation, nameof(activation));
        Check.NotNull(catalogItem, nameof(catalogItem));

        if (activation.PartnerCatalogItemId != catalogItem.Id ||
            activation.PartnerId != catalogItem.PartnerId)
        {
            throw new BusinessException(PartnerCatalogErrorCodes.CatalogItemMismatch);
        }

        if (!activation.TenantId.HasValue || activation.TenantId.Value == Guid.Empty)
        {
            throw new BusinessException(PartnerCatalogErrorCodes.InvalidActivation)
                .WithData("Reason", "ActivationTenantRequired");
        }

        var (normalizedExternal, normalizedOrderLineId) =
            PartnerCatalogSnapshotIdempotency.BuildUniqueKeyComponents(
                externalTransactionId,
                orderLineId,
                trigger);

        var snapshot = new SettlementCostMarkupSnapshot
        {
            Id = id,
            MerchantActivationId = activation.Id,
            PartnerCatalogItemId = catalogItem.Id,
            PartnerId = activation.PartnerId,
            TenantId = activation.TenantId.Value,
            SettlementBook = catalogItem.EffectiveSettlementBook,
            VatTreatment = VatTreatment.Principal,
            Trigger = trigger,
            ExternalTransactionId = normalizedExternal,
            OrderLineId = normalizedOrderLineId,
            SellPriceSource = sellPriceSource,
            SettlementCaseId = null,
            CreatedAt = createdAt ?? DateTime.UtcNow
        };

        snapshot.AssignBuyPrice(catalogItem.PartnerCost);
        snapshot.AssignSellPrice(sellPrice);

        return snapshot;
    }

    public void LinkSettlementCase(Guid settlementCaseId)
    {
        if (settlementCaseId == Guid.Empty)
        {
            throw new BusinessException(PartnerCatalogErrorCodes.InvalidSettlementLink);
        }

        if (SettlementCaseId.HasValue && SettlementCaseId.Value != settlementCaseId)
        {
            throw new BusinessException(PartnerCatalogErrorCodes.SettlementCaseAlreadyLinked)
                .WithData("Existing", SettlementCaseId.Value)
                .WithData("Requested", settlementCaseId);
        }

        SettlementCaseId = settlementCaseId;
    }

    public void LinkBillingCharge(Guid billingChargeId)
    {
        if (billingChargeId == Guid.Empty)
        {
            throw new BusinessException(PartnerCatalogErrorCodes.InvalidBillingLink);
        }

        if (BillingChargeId.HasValue && BillingChargeId.Value != billingChargeId)
        {
            throw new BusinessException(PartnerCatalogErrorCodes.BillingChargeAlreadyLinked)
                .WithData("Existing", BillingChargeId.Value)
                .WithData("Requested", billingChargeId);
        }

        BillingChargeId = billingChargeId;
    }

    private void AssignBuyPrice(Money buyPrice)
    {
        PartnerCatalogMoneyAssignment.Assign(
            buyPrice,
            amount => BuyPriceAmount = amount,
            currency => BuyPriceCurrency = currency,
            vatInclusive => BuyPriceVatInclusive = vatInclusive);
    }

    private void AssignSellPrice(Money sellPrice)
    {
        PartnerCatalogMoneyAssignment.Assign(
            sellPrice,
            amount => SellPriceAmount = amount,
            currency => SellPriceCurrency = currency,
            vatInclusive => SellPriceVatInclusive = vatInclusive,
            PartnerCatalogErrorCodes.InvalidResalePrice);
    }
}
