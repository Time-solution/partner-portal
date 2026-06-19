namespace Zahy.Finance;

public interface IFinanceInvoicePdfGenerator
{
    byte[] GenerateInvoice(
        FinanceBrandingOptions branding,
        FinancePostingLedgerSnapshot ledger,
        FinanceDocumentKycBlockDto verifiedKyc,
        ZatcaFatooraInvoiceDto zatca);
}

public class DefaultFinanceInvoicePdfGenerator : IFinanceInvoicePdfGenerator
{
    public byte[] GenerateInvoice(
        FinanceBrandingOptions branding,
        FinancePostingLedgerSnapshot ledger,
        FinanceDocumentKycBlockDto verifiedKyc,
        ZatcaFatooraInvoiceDto zatca) =>
        FinancePdfDocumentGenerator.GenerateInvoice(branding, ledger, verifiedKyc, zatca);
}

public class ConfigurableFinanceInvoicePdfGenerator : IFinanceInvoicePdfGenerator
{
    private readonly IFinanceInvoicePdfGenerator _inner;

    public ConfigurableFinanceInvoicePdfGenerator(DefaultFinanceInvoicePdfGenerator inner)
    {
        _inner = inner;
    }

    public bool FailNext { get; set; }

    public byte[] GenerateInvoice(
        FinanceBrandingOptions branding,
        FinancePostingLedgerSnapshot ledger,
        FinanceDocumentKycBlockDto verifiedKyc,
        ZatcaFatooraInvoiceDto zatca)
    {
        if (FailNext)
        {
            FailNext = false;
            throw new InvalidOperationException("Simulated invoice PDF generation failure.");
        }

        return _inner.GenerateInvoice(branding, ledger, verifiedKyc, zatca);
    }
}
