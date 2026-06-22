using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;
using Zahy.Settlement.Read;

namespace Zahy.Settlement;

/// <summary>
/// DB round-trip for the per-receipt destination + method added so the backend persists what the
/// frontend already captures (InvoicePayment.bankAccountId/.method). Proves a recorded receipt REMEMBERS
/// its bank (110x) and method across a real save/load, that two receipts on one invoice keep their own
/// destinations, and that the ledger still accumulates to Paid across a mix of methods. Posting stays
/// COMPUTE-ONLY (PostingEnabled OFF); nothing is disbursed here.
/// </summary>
public class PaymentPersistenceTests : ZahySettlementReadTestBase
{
    private readonly IRepository<Payment, Guid> _payments;

    public PaymentPersistenceTests()
    {
        _payments = GetRequiredService<IRepository<Payment, Guid>>();
    }

    private static Money Incl(decimal amount) =>
        Money.Of(amount, SettlementConsts.DefaultCurrency, vatInclusive: true);

    [Fact]
    public async Task Receipt_Persists_Its_Bank_Destination_And_Method()
    {
        var id = Guid.NewGuid();
        var payerId = Guid.NewGuid();

        await WithUnitOfWorkAsync(async () =>
        {
            var receipt = Payment.Record(
                id, "STL-1", PaymentPayer.Merchant, payerId, Incl(100.00m), DateTime.UtcNow,
                method: PaymentMethod.Transfer, bankAccountCode: "1102", idempotencyKey: "RCPT-1");
            await _payments.InsertAsync(receipt, autoSave: true);
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var loaded = await _payments.GetAsync(id);
            loaded.BankAccountCode.ShouldBe("1102");
            loaded.Method.ShouldBe(PaymentMethod.Transfer);
            loaded.Amount.ShouldBe(100.00m);
        });
    }

    [Fact]
    public async Task Two_Receipts_On_One_Invoice_To_Different_Banks_Persist_Their_Own_Destination()
    {
        var ar = Incl(100.00m);
        var payerId = Guid.NewGuid();
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();

        await WithUnitOfWorkAsync(async () =>
        {
            var first = Payment.Record(
                firstId, "STL-2", PaymentPayer.Merchant, payerId, Incl(60.00m), DateTime.UtcNow,
                arTotal: ar, existingForRef: Array.Empty<Payment>(),
                method: PaymentMethod.Cash, bankAccountCode: "1101", idempotencyKey: "RCPT-A");
            await _payments.InsertAsync(first, autoSave: true);

            var existing = await _payments.GetListAsync();
            var second = Payment.Record(
                secondId, "STL-2", PaymentPayer.Merchant, payerId, Incl(40.00m), DateTime.UtcNow,
                arTotal: ar, existingForRef: existing,
                method: PaymentMethod.Card, bankAccountCode: "1102", idempotencyKey: "RCPT-B");
            await _payments.InsertAsync(second, autoSave: true);
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var rows = (await _payments.GetListAsync())
                .Where(p => p.AgainstRef == "STL-2")
                .ToList();

            rows.Single(p => p.Id == firstId).BankAccountCode.ShouldBe("1101");
            rows.Single(p => p.Id == firstId).Method.ShouldBe(PaymentMethod.Cash);
            rows.Single(p => p.Id == secondId).BankAccountCode.ShouldBe("1102");
            rows.Single(p => p.Id == secondId).Method.ShouldBe(PaymentMethod.Card);

            // The ledger accumulates to Paid across the mixed methods + destinations.
            var status = PaymentLedger.Status("STL-2", ar, rows);
            status.PaidToDate.Amount.ShouldBe(100.00m);
            status.State.ShouldBe(PaymentState.Paid);
        });
    }

    [Fact]
    public async Task A_Receipt_Without_A_Bank_Persists_Null_Code_And_Method()
    {
        var id = Guid.NewGuid();
        var payerId = Guid.NewGuid();

        await WithUnitOfWorkAsync(async () =>
        {
            var receipt = Payment.Record(
                id, "STL-3", PaymentPayer.Partner, payerId, Incl(5.00m), DateTime.UtcNow);
            await _payments.InsertAsync(receipt, autoSave: true);
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var loaded = await _payments.GetAsync(id);
            loaded.BankAccountCode.ShouldBeNull(); // → 1100 fallback
            loaded.Method.ShouldBeNull();
        });
    }
}
