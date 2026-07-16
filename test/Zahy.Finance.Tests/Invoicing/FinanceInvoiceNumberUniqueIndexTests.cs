using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace Zahy.Finance;

/// <summary>
/// AF3 — the unique index on <c>FinDocuments.InvoiceNumber</c> is the atomic backstop behind the P5
/// :085 duplicate-reference gate. The gate's `.Any()` still gives the friendly verdict on the normal
/// path; this proves a second document sharing an invoice number can NEVER be persisted even if two
/// callers race past the pre-check (the constraint the app-level `.Any()` alone could not guarantee).
/// </summary>
[Collection(PdfRenderCollection.Name)]
public class FinanceInvoiceNumberUniqueIndexTests : ZahyFinanceTestBase
{
    private readonly IRepository<FinanceDocument, Guid> _documentRepository;

    public FinanceInvoiceNumberUniqueIndexTests()
    {
        _documentRepository = GetRequiredService<IRepository<FinanceDocument, Guid>>();
        GetRequiredService<TestCurrentPartner>().Id = null;
    }

    private static FinanceDocument Ledger(string invoiceNumber, string idem) =>
        FinanceDocument.CreateInvoice(
            Guid.NewGuid(), FinanceAccountKind.Partner, Guid.NewGuid(), Guid.NewGuid(),
            idem, invoiceNumber, 2026, 1, 100m,
            new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc));

    [Fact]
    public async Task Duplicate_InvoiceNumber_Insert_Hits_The_Unique_Backstop()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            await _documentRepository.InsertAsync(
                Ledger("ZAHY-INV-2026-009001", "af3:first"), autoSave: true);
        });

        // Distinct id + distinct idempotency key, SAME invoice number → only the unique index can stop it.
        var ex = await Should.ThrowAsync<Exception>(async () =>
        {
            await WithUnitOfWorkAsync(async () =>
            {
                await _documentRepository.InsertAsync(
                    Ledger("ZAHY-INV-2026-009001", "af3:second"), autoSave: true);
            });
        });

        // ABP surfaces the provider unique-violation as a DbUpdateException (SQLite constraint).
        (ex is DbUpdateException || ex.InnerException is DbUpdateException ||
         ex.GetType().Name.Contains("DbUpdate")).ShouldBeTrue($"expected a unique-constraint failure, got {ex.GetType().Name}");
    }

    [Fact]
    public async Task Distinct_InvoiceNumbers_Coexist()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            await _documentRepository.InsertAsync(Ledger("ZAHY-INV-2026-009100", "af3:a"), autoSave: true);
            await _documentRepository.InsertAsync(Ledger("MAN-2026-9101", "af3:b"), autoSave: true);
        });

        await WithUnitOfWorkAsync(async () =>
        {
            (await _documentRepository.CountAsync(x =>
                x.InvoiceNumber == "ZAHY-INV-2026-009100" || x.InvoiceNumber == "MAN-2026-9101")).ShouldBe(2);
        });
    }
}
