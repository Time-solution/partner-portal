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

    public string ToJson() => JsonSerializer.Serialize(this);

    public static SettlementAllocationSnapshot? FromJson(string? json) =>
        string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<SettlementAllocationSnapshot>(json!);
}
