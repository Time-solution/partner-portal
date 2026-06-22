using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Zahy.Settlement;

public sealed record SettlementJournalLegDto
{
    public string Account { get; init; } = string.Empty;
    public string Direction { get; init; } = string.Empty;
    public decimal Amount { get; init; }
}

/// <summary>Serializable snapshot of a posted allocation/journal — persisted on the event log and returned by explain.</summary>
public sealed record SettlementAllocationSnapshot
{
    public string Currency { get; init; } = SettlementConsts.DefaultCurrency;
    public decimal MerchantPayout { get; init; }
    public decimal PlatformCommissionNet { get; init; }
    public decimal DeliveryCost { get; init; }
    public decimal VatOutput { get; init; }
    public decimal VatInput { get; init; }
    public decimal NetVatToZatca { get; init; }
    public decimal TotalDebits { get; init; }
    public decimal TotalCredits { get; init; }
    public List<SettlementJournalLegDto> Legs { get; init; } = new();

    public static SettlementAllocationSnapshot From(SettlementAllocationResult result)
    {
        var journal = result.Journal;
        return new SettlementAllocationSnapshot
        {
            Currency = journal.Currency,
            MerchantPayout = result.MerchantPayout.Amount,
            PlatformCommissionNet = result.PlatformCommissionNet.Amount,
            DeliveryCost = result.DeliveryCost.Amount,
            VatOutput = result.VatOutput.Amount,
            VatInput = result.VatInput.Amount,
            NetVatToZatca = result.NetVatToZatca.Amount,
            TotalDebits = journal.TotalDebits.Amount,
            TotalCredits = journal.TotalCredits.Amount,
            Legs = journal.Lines.Select(l => new SettlementJournalLegDto
            {
                Account = l.Account.ToString(),
                Direction = l.Direction.ToString(),
                Amount = l.Amount.Amount
            }).ToList()
        };
    }

    /// <summary>
    /// Produces the compensating mirror of this snapshot: every Dr leg becomes Cr (and vice versa),
    /// totals swap, and the summary aggregates negate. Posting this alongside the original nets every
    /// account back to zero — the append-only correction pattern.
    /// </summary>
    public SettlementAllocationSnapshot Invert() =>
        new()
        {
            Currency = Currency,
            MerchantPayout = -MerchantPayout,
            PlatformCommissionNet = -PlatformCommissionNet,
            DeliveryCost = -DeliveryCost,
            VatOutput = -VatOutput,
            VatInput = -VatInput,
            NetVatToZatca = -NetVatToZatca,
            TotalDebits = TotalCredits,
            TotalCredits = TotalDebits,
            Legs = Legs.Select(l => new SettlementJournalLegDto
            {
                Account = l.Account,
                Direction = string.Equals(l.Direction, "Debit", System.StringComparison.OrdinalIgnoreCase)
                    ? "Credit"
                    : "Debit",
                Amount = l.Amount
            }).ToList()
        };

    /// <summary>
    /// True when posting <paramref name="reversal"/> on top of this snapshot nets every account to
    /// zero: matching currency, mirrored totals, and per-account signed sums all zero.
    /// </summary>
    public bool NetsToZeroWith(SettlementAllocationSnapshot reversal)
    {
        if (reversal == null || !string.Equals(Currency, reversal.Currency, System.StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (TotalDebits != reversal.TotalCredits || TotalCredits != reversal.TotalDebits)
        {
            return false;
        }

        var net = new Dictionary<string, decimal>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var leg in Legs.Concat(reversal.Legs))
        {
            var signed = string.Equals(leg.Direction, "Debit", System.StringComparison.OrdinalIgnoreCase)
                ? leg.Amount
                : -leg.Amount;
            net[leg.Account] = net.TryGetValue(leg.Account, out var running) ? running + signed : signed;
        }

        return net.Values.All(v => v == 0m);
    }

    public string ToJson() => JsonSerializer.Serialize(this);

    public static SettlementAllocationSnapshot? FromJson(string? json) =>
        string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<SettlementAllocationSnapshot>(json!);
}
