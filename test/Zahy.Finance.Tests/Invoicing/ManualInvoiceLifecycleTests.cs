using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using Xunit;
using Zahy.Identity.Permissions;
using Zahy.Identity.Roles;

namespace Zahy.Finance;

/// <summary>
/// P4 — manual-invoice lifecycle (Draft → Issued, immutable afterwards), APP-MANAGED LineNo rules
/// (renumber-on-remove; validate-then-mutate leaves no gap on a failed edit), and the MAN-yyyy-####
/// internal non-fiscal numbering pool (own sequence, gap-free under rollback, ledger pool untouched).
/// </summary>
[Collection(PdfRenderCollection.Name)]
public class ManualInvoiceLifecycleTests : ZahyFinanceTestBase
{
    private const decimal VatRate = 0.15m;

    private readonly IFinanceDocumentAppService _manualInvoiceAppService;
    private readonly IFinanceInvoiceNumberAllocator _numberAllocator;
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    public ManualInvoiceLifecycleTests()
    {
        _manualInvoiceAppService = GetRequiredService<IFinanceDocumentAppService>();
        _numberAllocator = GetRequiredService<IFinanceInvoiceNumberAllocator>();
        _unitOfWorkManager = GetRequiredService<IUnitOfWorkManager>();
        GetRequiredService<TestCurrentPartner>().Id = null;
    }

    private static CreateManualInvoiceRequest Request(string key, params ManualInvoiceLineDto[] lines) =>
        new()
        {
            RecipientType = FinanceDocumentRecipientType.External,
            RecipientNameOverride = "Acme Consulting LLC",
            Lines = lines.ToList(),
            IdempotencyKey = key,
            Currency = "SAR"
        };

    private static ManualInvoiceLineDto Line(string description, decimal qty, decimal unitInclusive) =>
        new() { Description = description, Quantity = qty, UnitPriceInclusive = unitInclusive };

    private static FinanceInvoiceLine DomainLine(int lineNo, string description, decimal unitInclusive) =>
        FinanceInvoiceLine.Create(lineNo, description, 1m, unitInclusive, VatRate);

    private static FinanceDocument Draft(params FinanceInvoiceLine[] lines) =>
        FinanceDocument.CreateManualInvoice(
            Guid.NewGuid(),
            FinanceDocumentRecipientType.External,
            null,
            "Acme Consulting LLC",
            $"lifecycle:{Guid.NewGuid():N}",
            FinanceInvoiceNumberFormat.FormatManual(2026, 1),
            2026,
            1,
            lines,
            lines.Sum(l => l.LineTotalInclusive),
            new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc));

    // ---- lifecycle -----------------------------------------------------------------------------

    [Fact]
    public async Task Create_Starts_As_Draft_And_Issue_Transitions_Once()
    {
        var created = await _manualInvoiceAppService.CreateManualInvoiceAsync(
            Request($"p4-draft:{Guid.NewGuid():N}", Line("Consulting", 1m, 115.00m)));
        created.Status.ShouldBe(FinanceDocumentStatus.Draft);

        var issued = await _manualInvoiceAppService.IssueManualInvoiceAsync(created.Id);
        issued.Status.ShouldBe(FinanceDocumentStatus.Issued);

        // Second Issue is an illegal transition (009) — exactly one Draft → Issued edge exists.
        var ex = await Should.ThrowAsync<BusinessException>(() =>
            _manualInvoiceAppService.IssueManualInvoiceAsync(created.Id));
        ex.Code.ShouldBe(FinanceErrorCodes.IllegalStatusTransition);
    }

    [Fact]
    public void LedgerDerived_Documents_Are_Born_Issued()
    {
        var document = FinanceDocument.CreateInvoice(
            Guid.NewGuid(), FinanceAccountKind.Partner, Guid.NewGuid(), Guid.NewGuid(),
            $"ledger:{Guid.NewGuid():N}", "ZAHY-INV-2026-000001", 2026, 1, 100m,
            new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc));

        document.Status.ShouldBe(FinanceDocumentStatus.Issued);
    }

    [Fact]
    public void Issued_Manual_Invoice_Is_Immutable_082()
    {
        var draft = Draft(DomainLine(1, "Setup", 115.00m));
        draft.Issue();

        var ex = Should.Throw<BusinessException>(() =>
            draft.ReplaceLines(new[] { DomainLine(1, "Changed", 230.00m) }));
        ex.Code.ShouldBe(FinanceErrorCodes.ManualInvoiceImmutable);
    }

    // ---- LineNo rules (renumber-on-remove; no gap on failure) ----------------------------------

    [Fact]
    public void ReplaceLines_Renumbers_On_Remove_Contiguous_1_To_N()
    {
        var draft = Draft(
            DomainLine(1, "Keep A", 115.00m),
            DomainLine(2, "Remove me", 57.50m),
            DomainLine(3, "Keep B", 230.00m));

        // RENUMBER-ON-REMOVE: survivors are re-built 1..N (drafts are pre-audit; the Issued
        // snapshot is the immutable record, so pre-issue renumbering breaks no history).
        draft.ReplaceLines(new[]
        {
            DomainLine(1, "Keep A", 115.00m),
            DomainLine(2, "Keep B", 230.00m),
        });

        draft.Lines.Select(l => l.LineNo).ShouldBe(new[] { 1, 2 });
        draft.PostingSum.ShouldBe(345.00m); // totals recomputed from the surviving lines
    }

    [Fact]
    public void Failed_Line_Edit_Leaves_No_Gap_In_LineNo()
    {
        var draft = Draft(DomainLine(1, "Original A", 115.00m), DomainLine(2, "Original B", 57.50m));

        // Validate-then-mutate: a non-contiguous set (1,2,4 — a "rolled-back add") throws BEFORE
        // any mutation, so the existing lines stay exactly 1..N with no gap.
        Should.Throw<BusinessException>(() => draft.ReplaceLines(new[]
            {
                DomainLine(1, "Original A", 115.00m),
                DomainLine(2, "Original B", 57.50m),
                DomainLine(4, "Bad add", 230.00m),
            }))
            .Code.ShouldBe(FinanceErrorCodes.ManualInvoiceInvalidLines);

        draft.Lines.Select(l => l.LineNo).ShouldBe(new[] { 1, 2 });
        draft.PostingSum.ShouldBe(172.50m); // untouched
    }

    [Fact]
    public void ReplaceLines_Rejects_Empty_So_A_Draft_Can_Never_Issue_Without_Lines()
    {
        var draft = Draft(DomainLine(1, "Only line", 115.00m));

        Should.Throw<BusinessException>(() => draft.ReplaceLines(Array.Empty<FinanceInvoiceLine>()))
            .Code.ShouldBe(FinanceErrorCodes.ManualInvoiceInvalidLines);

        draft.Issue(); // still valid with its original line
        draft.Status.ShouldBe(FinanceDocumentStatus.Issued);
    }

    // ---- MAN numbering (internal, non-fiscal, gap-free) ----------------------------------------

    [Fact]
    public async Task Manual_Numbers_Use_The_MAN_Pool_And_Ledger_Pool_Is_Untouched()
    {
        var first = await _manualInvoiceAppService.CreateManualInvoiceAsync(
            Request($"p4-man-1:{Guid.NewGuid():N}", Line("A", 1m, 115.00m)));
        var second = await _manualInvoiceAppService.CreateManualInvoiceAsync(
            Request($"p4-man-2:{Guid.NewGuid():N}", Line("B", 1m, 115.00m)));

        first.InvoiceNumber.ShouldMatch(@"^MAN-\d{4}-\d{4}$");
        second.SequenceNumber.ShouldBe(first.SequenceNumber + 1);

        // The fiscal ledger pool allocates independently — its own prefix and its own counter.
        FinanceInvoiceNumberAllocation ledger;
        using (var uow = _unitOfWorkManager.Begin(requiresNew: true))
        {
            ledger = await _numberAllocator.AllocateInvoiceNumberAsync(new DateTime(2026, 7, 15));
            await uow.CompleteAsync();
        }
        ledger.InvoiceNumber.ShouldStartWith(FinanceInvoiceNumberFormat.InvoicePrefix);
    }

    [Fact]
    public async Task Manual_Sequence_Rollback_Leaves_No_Gap()
    {
        var issueDate = new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc);

        // Allocate inside a UoW that is deliberately ABANDONED (never completed) — the increment
        // rolls back with it.
        int abandoned;
        using (var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: true))
        {
            abandoned = (await _numberAllocator.AllocateManualInvoiceNumberAsync(issueDate)).SequenceNumber;
            // no CompleteAsync — rollback on dispose
        }

        int next;
        using (var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: true))
        {
            next = (await _numberAllocator.AllocateManualInvoiceNumberAsync(issueDate)).SequenceNumber;
            await uow.CompleteAsync();
        }

        next.ShouldBe(abandoned); // the rolled-back number was re-issued — no gap
    }

    // ---- permission matrix exclusions + DTO annotation rule ------------------------------------

    [Fact]
    public void WriteManualInvoice_Granted_To_PlatformFinance_And_Admin_Only()
    {
        ZahyRoleRegistry.Find(ZahyRoles.PlatformFinance)!.Permissions
            .ShouldContain(ZahyPermissions.Finance.WriteManualInvoice);
        ZahyPermissions.All().ShouldContain(ZahyPermissions.Finance.WriteManualInvoice); // admin sweep

        // Partner, merchant and PartnerOps roles are deliberately excluded — every non-platform-finance
        // role definition in the registry must NOT carry the permission.
        foreach (var role in ZahyRoleRegistry.All.Where(r => r.Name != ZahyRoles.PlatformFinance &&
                                                             r.Name != ZahyRoles.PlatformSuperAdmin))
        {
            role.Permissions.ShouldNotContain(
                ZahyPermissions.Finance.WriteManualInvoice,
                $"role '{role.Name}' must not author manual invoices");
        }
    }

    [Fact]
    public void Manual_Invoice_DTO_Annotations_Carry_No_Real_Error_Codes()
    {
        // Established rule: the domain owns error codes; DTO validation attributes are shape-only.
        foreach (var dto in new[] { typeof(CreateManualInvoiceRequest), typeof(ManualInvoiceLineDto), typeof(FinanceDocumentDto) })
        {
            foreach (var property in dto.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                foreach (var attribute in property.GetCustomAttributes<ValidationAttribute>())
                {
                    (attribute.ErrorMessage ?? string.Empty).ShouldNotContain(FinanceErrorCodes.Namespace);
                }
            }
        }
    }
}
