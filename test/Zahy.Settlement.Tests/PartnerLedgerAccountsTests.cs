using System;
using System.Collections.Generic;
using System.Linq;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace Zahy.Settlement;

/// <summary>
/// The per-partner ledger registrar: when a partner is active in settlement it is auto-assigned a
/// payable (2101+) and receivable (1251+) sub-account, idempotently. Multi-3PL (Salasa + Oto) each get
/// their OWN pair of sub-accounts — not a single shared 2100/1250 line.
/// </summary>
public class PartnerLedgerAccountsTests
{
    [Fact]
    public void An_Active_Partner_Gets_A_2101_And_1251_Sub_Account()
    {
        var salasa = Guid.NewGuid();

        var row = PartnerLedgerAccounts.EnsureFor(
            Guid.NewGuid(), salasa, "Salasa 3PL", Array.Empty<PartnerLedgerAccount>());

        row.PartnerId.ShouldBe(salasa);
        row.PayableCode.ShouldBe("2101");
        row.ReceivableCode.ShouldBe("1251");
        row.Status.ShouldBe(PartnerLedgerAccountStatus.Active);
        PartnerLedgerCoding.IsPayableSubAccount(row.PayableCode).ShouldBeTrue();
        PartnerLedgerCoding.IsReceivableSubAccount(row.ReceivableCode).ShouldBeTrue();
    }

    [Fact]
    public void EnsureFor_Is_Idempotent_For_The_Same_Partner()
    {
        var partner = Guid.NewGuid();
        var existing = new PartnerLedgerAccount(Guid.NewGuid(), partner, "Salasa", "2101", "1251");

        var again = PartnerLedgerAccounts.EnsureFor(
            Guid.NewGuid(), partner, "Salasa", new[] { existing });

        again.ShouldBeSameAs(existing);
        again.PayableCode.ShouldBe("2101");
    }

    [Fact]
    public void Multi_3PL_On_One_Merchant_Each_Get_Separate_Sub_Accounts()
    {
        var salasa = Guid.NewGuid();
        var oto = Guid.NewGuid();
        var rows = new List<PartnerLedgerAccount>();

        var salasaRow = PartnerLedgerAccounts.EnsureFor(Guid.NewGuid(), salasa, "Salasa", rows);
        rows.Add(salasaRow);
        var otoRow = PartnerLedgerAccounts.EnsureFor(Guid.NewGuid(), oto, "Oto", rows);
        rows.Add(otoRow);

        salasaRow.PayableCode.ShouldBe("2101");
        salasaRow.ReceivableCode.ShouldBe("1251");
        otoRow.PayableCode.ShouldBe("2102");
        otoRow.ReceivableCode.ShouldBe("1252");

        // Distinct ledger lines — no collision between the two 3PLs.
        rows.Select(r => r.PayableCode).Distinct().Count().ShouldBe(2);
        rows.Select(r => r.ReceivableCode).Distinct().Count().ShouldBe(2);
    }

    [Fact]
    public void PayableCodeFor_Falls_Back_To_The_2100_Parent_When_Unregistered()
    {
        var rows = new[] { new PartnerLedgerAccount(Guid.NewGuid(), Guid.NewGuid(), "Salasa", "2101", "1251") };

        PartnerLedgerAccounts.PayableCodeFor(Guid.NewGuid(), rows).ShouldBe("2100");
        PartnerLedgerAccounts.ReceivableCodeFor(Guid.NewGuid(), rows).ShouldBe("1250");
    }

    [Fact]
    public void Entity_Rejects_A_Code_Outside_The_Reserved_Range()
    {
        Should.Throw<BusinessException>(() =>
            new PartnerLedgerAccount(Guid.NewGuid(), Guid.NewGuid(), "Salasa", "2200", "1251"));

        Should.Throw<BusinessException>(() =>
            new PartnerLedgerAccount(Guid.NewGuid(), Guid.NewGuid(), "Salasa", "2101", "1300"));
    }

    [Fact]
    public void Entity_Rejects_An_Empty_Partner_Name()
    {
        Should.Throw<BusinessException>(() =>
                new PartnerLedgerAccount(Guid.NewGuid(), Guid.NewGuid(), "  ", "2101", "1251"))
            .Code.ShouldBe(SettlementPartnerLedgerErrorCodes.EmptyPartnerName);
    }
}
