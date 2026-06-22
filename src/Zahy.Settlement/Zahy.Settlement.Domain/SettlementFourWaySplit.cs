using System;
using Volo.Abp;

namespace Zahy.Settlement;

/// <summary>
/// The FOUR-way split of what the customer paid (3 parties + tax) — ported from the frontend (types.ts
/// FourWaySplit / assertSplitBalances / deriveFourWaySplit / codNetTransferred / assertCodReconciles) so the
/// SERVER enforces the money-conservation rule rather than only the UI.
///
/// Canonical invariant on every collected order (ONE rule — the allocator's older bespoke check now defers
/// to this):
///   Collected = Merchant + Delivery + ZahyMargin + NetVAT
///     Merchant   — items passed through to the merchant (Zahy books no VAT on these).
///     Delivery   — the delivery/partner gross take (VAT-inclusive); RETAINED by the delivery co under COD.
///     ZahyMargin — Zahy's NET margin (net sell − net buy on its resale leg).
///     NetVat     — Net VAT remitted to ZATCA (Output VAT − Input VAT) — the 4th recipient.
///
/// Worked case (order 901): 113.00 = 100.00 + 10.00 + 2.60 + 0.40.
///
/// The split is summed by RAW per-line amounts (mirrors the FE numeric sum); the <see cref="Money"/>
/// VAT-inclusive flag on the legs is not used by the invariant, so net/tax legs may carry it without
/// affecting the arithmetic.
/// </summary>
public sealed record SettlementFourWaySplit
{
    public Money Merchant { get; }

    public Money Delivery { get; }

    public Money ZahyMargin { get; }

    public Money NetVat { get; }

    public SettlementFourWaySplit(Money merchant, Money delivery, Money zahyMargin, Money netVat)
    {
        Merchant = Check.NotNull(merchant, nameof(merchant));
        Delivery = Check.NotNull(delivery, nameof(delivery));
        ZahyMargin = Check.NotNull(zahyMargin, nameof(zahyMargin));
        NetVat = Check.NotNull(netVat, nameof(netVat));

        EnsureSameCurrency(Merchant, Delivery);
        EnsureSameCurrency(Merchant, ZahyMargin);
        EnsureSameCurrency(Merchant, NetVat);
    }

    public string Currency => Merchant.Currency;

    /// <summary>Sum of the four recipients (raw amounts, rounded per line) — mirrors FE splitComponentsTotal.</summary>
    public decimal ComponentsTotal() =>
        SettlementMoney.Round(Merchant.Amount + Delivery.Amount + ZahyMargin.Amount + NetVat.Amount);

    /// <summary>
    /// The portion that actually reaches Zahy's recipients — everything EXCEPT the delivery take
    /// (Merchant + ZahyMargin + NetVAT). Under COD this equals the net transferred to Zahy.
    /// </summary>
    public decimal RecipientsExcludingDelivery() =>
        SettlementMoney.Round(Merchant.Amount + ZahyMargin.Amount + NetVat.Amount);

    /// <summary>
    /// Throws unless Collected == Merchant + Delivery + ZahyMargin + NetVAT (2dp). Mirrors FE
    /// assertSplitBalances. Callers may override the error code (e.g. the allocator keeps its
    /// AllocationDoesNotBalance contract while still using this one canonical invariant).
    /// </summary>
    public void EnsureBalances(Money collected, string? errorCode = null)
    {
        Check.NotNull(collected, nameof(collected));
        EnsureSameCurrency(Merchant, collected);

        var components = ComponentsTotal();
        var total = SettlementMoney.Round(collected.Amount);
        if (components != total)
        {
            throw new BusinessException(errorCode ?? SettlementSplitErrorCodes.FourWaySplitDoesNotBalance)
                .WithData("Collected", total)
                .WithData("Components", components)
                .WithData("Merchant", Merchant.Amount)
                .WithData("Delivery", Delivery.Amount)
                .WithData("ZahyMargin", ZahyMargin.Amount)
                .WithData("NetVat", NetVat.Amount);
        }
    }

    /// <summary>
    /// COD "net transferred to Zahy" — COMPUTED, never hand-seeded: collected − delivery fee (113 − 10 = 103).
    /// Mirrors FE codNetTransferred.
    /// </summary>
    public static decimal CodNetTransferred(Money collected, Money deliveryFee)
    {
        Check.NotNull(collected, nameof(collected));
        Check.NotNull(deliveryFee, nameof(deliveryFee));
        EnsureSameCurrency(collected, deliveryFee);
        return SettlementMoney.Round(collected.Amount - deliveryFee.Amount);
    }

    /// <summary>
    /// COD reconciliation invariant — proves no money goes missing. Mirrors FE assertCodReconciles:
    ///   1. Collected = Merchant + Delivery + ZahyMargin + NetVAT      (this split balances)
    ///   2. collected − delivery fee = net transferred                 (computed remittance == seeded)
    ///   3. net transferred = Merchant + ZahyMargin + NetVAT           (delivery retained, not transferred)
    /// Throws if the seeded transfer drifts from the computed one, or a stray (unaccounted) amount appears.
    /// </summary>
    public void EnsureCodReconciles(Money collected, Money deliveryFee, Money seededNetTransferred)
    {
        Check.NotNull(seededNetTransferred, nameof(seededNetTransferred));

        EnsureBalances(collected);

        var transferred = CodNetTransferred(collected, deliveryFee);
        var seeded = SettlementMoney.Round(seededNetTransferred.Amount);
        if (seeded != transferred)
        {
            throw new BusinessException(SettlementSplitErrorCodes.CodNetTransferMismatch)
                .WithData("Seeded", seeded)
                .WithData("Transferred", transferred);
        }

        var recipients = RecipientsExcludingDelivery();
        if (recipients != transferred)
        {
            throw new BusinessException(SettlementSplitErrorCodes.CodUnaccountedAmount)
                .WithData("Transferred", transferred)
                .WithData("Recipients", recipients)
                .WithData("Remainder", SettlementMoney.Round(transferred - recipients));
        }
    }

    /// <summary>
    /// Derive the four-way split from the real flow so the invariant holds BY CONSTRUCTION (mirrors FE
    /// deriveFourWaySplit): the resale leg = collected − merchant items; net + VAT are backed out of the
    /// sell (resale leg) and the partner buy.
    ///   merchant   = merchant items (pass-through, no Zahy VAT)
    ///   delivery   = partner buy gross (the partner's full VAT-inclusive take)
    ///   zahyMargin = sellNet − buyNet
    ///   netVat     = outputVat − inputVat
    /// Proof: merchant + buyGross + (sellNet−buyNet) + (outVat−inVat) = merchant + sellNet + outVat
    ///        = merchant + resaleLegGross = collected.
    /// </summary>
    public static SettlementFourWaySplit DeriveFromFlow(
        Money collected,
        Money merchantItems,
        Money partnerBuyGross,
        decimal vatRate)
    {
        Check.NotNull(collected, nameof(collected));
        Check.NotNull(merchantItems, nameof(merchantItems));
        Check.NotNull(partnerBuyGross, nameof(partnerBuyGross));
        VatMath.EnsureValidRate(vatRate);
        EnsureSameCurrency(collected, merchantItems);
        EnsureSameCurrency(collected, partnerBuyGross);

        var currency = collected.Currency;

        var resaleLegGross = SettlementMoney.Round(collected.Amount - merchantItems.Amount);
        var sellNet = VatMath.NetOfInclusive(resaleLegGross, vatRate);
        var outputVat = VatMath.VatOfInclusive(resaleLegGross, vatRate);
        var buyNet = VatMath.NetOfInclusive(partnerBuyGross.Amount, vatRate);
        var inputVat = VatMath.VatOfInclusive(partnerBuyGross.Amount, vatRate);

        return new SettlementFourWaySplit(
            Money.Of(merchantItems.Amount, currency, vatInclusive: true),
            Money.Of(partnerBuyGross.Amount, currency, vatInclusive: true),
            Money.Of(SettlementMoney.Round(sellNet - buyNet), currency, vatInclusive: true),
            Money.Of(SettlementMoney.Round(outputVat - inputVat), currency, vatInclusive: true));
    }

    private static void EnsureSameCurrency(Money left, Money right)
    {
        if (!string.Equals(left.Currency, right.Currency, StringComparison.Ordinal))
        {
            throw new BusinessException(SettlementErrorCodes.CurrencyMismatch)
                .WithData("Left", left.Currency)
                .WithData("Right", right.Currency);
        }
    }
}
