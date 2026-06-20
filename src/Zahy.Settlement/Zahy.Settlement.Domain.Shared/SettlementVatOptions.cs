using System.Collections.Generic;
using Volo.Abp;

namespace Zahy.Settlement;

/// <summary>
/// VAT configuration. The rate and the per-book treatment (agent vs principal) are CONFIG — never
/// hardcoded. <see cref="TreatmentByBook"/> is empty by default and must be set by the accountant
/// before go-live (DESIGN.md §11.2/§11.4); resolving an unconfigured book is an explicit error,
/// not a silent default.
/// </summary>
public class SettlementVatOptions
{
    public const string SectionName = "Settlement:Vat";

    /// <summary>Standard VAT rate (e.g. 0.15). Overridable via config; not hardcoded in the engine.</summary>
    public decimal StandardRate { get; set; } = 0.15m;

    public Dictionary<SettlementBook, VatTreatment> TreatmentByBook { get; set; } = new();

    public VatTreatment ResolveTreatment(SettlementBook book) =>
        TreatmentByBook.TryGetValue(book, out var treatment)
            ? treatment
            : throw new BusinessException(SettlementVatErrorCodes.TreatmentNotConfigured)
                .WithData("Book", book.ToString());
}
