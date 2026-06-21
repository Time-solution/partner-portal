using System;
using System.Collections.Generic;
using System.Linq;
using Shouldly;
using Xunit;

namespace Zahy.Settlement;

/// <summary>
/// Phase D Task 3 — per-partner bulk invoice read model over SUCCESSFUL transactions.
/// Anchor: Chefz, 5 successful txns × 1.00 incl → 5.00 incl = 4.35 ex + 0.65 VAT. Display only.
/// </summary>
public class ActivationBulkInvoiceTests
{
    private static readonly SettlementPeriod Period = SettlementPeriod.Of(2026, 6);
    private static readonly Guid Chefz = Guid.NewGuid();
    private static readonly Guid Shawarma = Guid.NewGuid();
    private static readonly Guid Pasta = Guid.NewGuid();

    private static Money Fee(decimal amount) => Money.Of(amount, SettlementConsts.DefaultCurrency, vatInclusive: true);

    private static FeeBillableTransaction Txn(Guid merchant, string name, string orderRef, bool ok = true) =>
        new(merchant, name, orderRef, Fee(1.00m), ok);

    [Fact]
    public void Chefz_Five_Successful_Txns_Total_5_Incl_435_Ex_065_Vat()
    {
        var txns = new[]
        {
            Txn(Shawarma, "Shawarma House", "C-1"),
            Txn(Shawarma, "Shawarma House", "C-2"),
            Txn(Pasta, "Pasta Bar", "C-3"),
            Txn(Pasta, "Pasta Bar", "C-4"),
            Txn(Pasta, "Pasta Bar", "C-5"),
        };

        var invoice = ActivationBulkInvoice.Build(Chefz, "The Chefz", Period, txns);

        invoice.TransactionCount.ShouldBe(5);
        invoice.TotalInclusive.Amount.ShouldBe(5.00m);
        invoice.TotalNet.Amount.ShouldBe(4.35m);
        invoice.TotalVat.Amount.ShouldBe(0.65m);
        (invoice.TotalNet.Amount + invoice.TotalVat.Amount).ShouldBe(invoice.TotalInclusive.Amount);
    }

    [Fact]
    public void Drill_Down_Groups_Per_Merchant_With_Subtotals()
    {
        var txns = new[]
        {
            Txn(Shawarma, "Shawarma House", "C-1"),
            Txn(Shawarma, "Shawarma House", "C-2"),
            Txn(Pasta, "Pasta Bar", "C-3"),
            Txn(Pasta, "Pasta Bar", "C-4"),
            Txn(Pasta, "Pasta Bar", "C-5"),
        };

        var invoice = ActivationBulkInvoice.Build(Chefz, "The Chefz", Period, txns);

        invoice.Lines.Count.ShouldBe(2);
        var pasta = invoice.Lines.Single(l => l.MerchantId == Pasta);
        pasta.Transactions.Count.ShouldBe(3);
        pasta.SubtotalInclusive.Amount.ShouldBe(3.00m);

        var shawarma = invoice.Lines.Single(l => l.MerchantId == Shawarma);
        shawarma.Transactions.Count.ShouldBe(2);
        shawarma.SubtotalInclusive.Amount.ShouldBe(2.00m);
    }

    [Fact]
    public void Only_Successful_Transactions_Are_Billed()
    {
        var txns = new[]
        {
            Txn(Shawarma, "Shawarma House", "C-1"),
            Txn(Shawarma, "Shawarma House", "C-2", ok: false), // failed → not billed
            Txn(Pasta, "Pasta Bar", "C-3"),
        };

        var invoice = ActivationBulkInvoice.Build(Chefz, "The Chefz", Period, txns);

        invoice.TransactionCount.ShouldBe(2);
        invoice.TotalInclusive.Amount.ShouldBe(2.00m);
    }

    [Fact]
    public void Empty_When_No_Successful_Transactions()
    {
        var invoice = ActivationBulkInvoice.Build(
            Chefz, "The Chefz", Period,
            new List<FeeBillableTransaction> { Txn(Shawarma, "Shawarma House", "C-1", ok: false) });

        invoice.TransactionCount.ShouldBe(0);
        invoice.Lines.ShouldBeEmpty();
        invoice.TotalInclusive.Amount.ShouldBe(0m);
        invoice.TotalNet.Amount.ShouldBe(0m);
        invoice.TotalVat.Amount.ShouldBe(0m);
    }
}
