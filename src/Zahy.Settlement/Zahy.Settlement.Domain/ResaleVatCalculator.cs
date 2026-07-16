using Volo.Abp;

namespace Zahy.Settlement;

public interface IResaleVatCalculator
{
    ResaleVatResult Compute(CostMarkupLine line, decimal vatRate, VatTreatment treatment);
}

/// <summary>
/// Computes margin + VAT for a cost/markup line. Round-per-line throughout (DESIGN.md §9.1).
/// KSA is PRINCIPAL-ONLY: output VAT on the sell, input VAT reclaimed on the buy; net-to-ZATCA =
/// output − input. The AGENT branch (output VAT on the commission only) is QUARANTINED (AF3): the
/// production <see cref="Compute"/> entry HARD-REJECTS it with <see cref="SettlementVatErrorCodes.AgentTreatmentNotSupportedInKsa"/>,
/// so no config/book-mapping wire can silently reach margin-only VAT. The math is preserved and
/// regression-covered only via the explicit test-only <see cref="ComputeAgentForVerification"/>.
/// </summary>
public sealed class ResaleVatCalculator : IResaleVatCalculator
{
    public ResaleVatResult Compute(CostMarkupLine line, decimal vatRate, VatTreatment treatment)
    {
        Check.NotNull(line, nameof(line));
        VatMath.EnsureValidRate(vatRate);

        var currency = line.Currency;
        var buyInclusive = line.BuyPrice.Amount;
        var sellInclusive = line.SellPrice.Amount;

        return treatment switch
        {
            VatTreatment.Principal => BuildPrincipal(currency, buyInclusive, sellInclusive, vatRate),
            // AF3 — Agent is not a valid production treatment in KSA; reject rather than compute.
            VatTreatment.Agent => throw new BusinessException(SettlementVatErrorCodes.AgentTreatmentNotSupportedInKsa)
                .WithData("Treatment", treatment.ToString()),
            _ => throw new BusinessException(SettlementVatErrorCodes.UnknownVatTreatment)
                .WithData("Treatment", treatment.ToString())
        };
    }

    /// <summary>
    /// TEST-ONLY entry preserving the Agent VAT math (never called from production — <see cref="Compute"/>
    /// rejects Agent). Kept so the quarantined branch's arithmetic stays regression-covered.
    /// </summary>
    public static ResaleVatResult ComputeAgentForVerification(CostMarkupLine line, decimal vatRate)
    {
        Check.NotNull(line, nameof(line));
        VatMath.EnsureValidRate(vatRate);
        return BuildAgent(line.Currency, line.BuyPrice.Amount, line.SellPrice.Amount, vatRate);
    }

    private static ResaleVatResult BuildPrincipal(string currency, decimal buyInclusive, decimal sellInclusive, decimal rate)
    {
        var netBuy = VatMath.NetOfInclusive(buyInclusive, rate);
        var netSell = VatMath.NetOfInclusive(sellInclusive, rate);
        var inputVat = SettlementMoney.Round(buyInclusive - netBuy);
        var outputVat = SettlementMoney.Round(sellInclusive - netSell);
        var margin = SettlementMoney.Round(netSell - netBuy);
        var netVatToZatca = SettlementMoney.Round(outputVat - inputVat);

        return new ResaleVatResult
        {
            Treatment = VatTreatment.Principal,
            NetBuy = Money.Of(netBuy, currency),
            NetSell = Money.Of(netSell, currency),
            InputVat = Money.Of(inputVat, currency),
            OutputVat = Money.Of(outputVat, currency),
            Margin = Money.Of(margin, currency),
            NetVatToZatca = Money.Of(netVatToZatca, currency)
        };
    }

    private static ResaleVatResult BuildAgent(string currency, decimal buyInclusive, decimal sellInclusive, decimal rate)
    {
        // The agent VATs only its commission — the gross spread — and never reclaims input VAT;
        // the underlying cost passes through to the principal supply.
        var grossSpread = SettlementMoney.Round(sellInclusive - buyInclusive);
        var netCommission = VatMath.NetOfInclusive(grossSpread, rate);
        var outputVat = SettlementMoney.Round(grossSpread - netCommission);

        return new ResaleVatResult
        {
            Treatment = VatTreatment.Agent,
            NetBuy = Money.Of(VatMath.NetOfInclusive(buyInclusive, rate), currency),
            NetSell = Money.Of(VatMath.NetOfInclusive(sellInclusive, rate), currency),
            InputVat = Money.Zero(currency),
            OutputVat = Money.Of(outputVat, currency),
            Margin = Money.Of(netCommission, currency),
            NetVatToZatca = Money.Of(outputVat, currency)
        };
    }
}
