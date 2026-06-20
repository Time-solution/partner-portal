namespace Zahy.Settlement;

/// <summary>
/// The VAT model for a settlement. Selected per partner type / book by CONFIGURATION — never
/// hardcoded — and pending the accountant's decision (DESIGN.md §9 / §11.4).
/// </summary>
public enum VatTreatment
{
    /// <summary>Output VAT on the platform's commission/margin only; no input VAT reclaim (cost passes through).</summary>
    Agent = 1,

    /// <summary>Output VAT on the full sell; input VAT reclaimed on the buy; net-to-ZATCA = output − input.</summary>
    Principal = 2
}
