using System;
using System.Collections.Generic;
using System.Linq;

namespace Zahy.Settlement;

/// <summary>
/// Pure registrar over the per-partner ledger sub-account registry (mirrors the bank registry's
/// next-free-slot assignment). Auto-assigns each partner a payable (2101+) and receivable (1251+)
/// sub-account the first time it is active in settlement, and resolves existing rows idempotently.
/// Records/derives intent ONLY — it posts no journal and mutates no production chart.
/// </summary>
public static class PartnerLedgerAccounts
{
    /// <summary>
    /// Idempotent: returns the SAME row if <paramref name="partnerId"/> already has one; otherwise mints
    /// a new <see cref="PartnerLedgerAccount"/> taking the next free payable + receivable sub-codes given
    /// <paramref name="existing"/>. The two ranges are assigned independently so a gap in one never skews
    /// the other.
    /// </summary>
    public static PartnerLedgerAccount EnsureFor(
        Guid id,
        Guid partnerId,
        string partnerName,
        IEnumerable<PartnerLedgerAccount> existing)
    {
        var rows = existing?.ToList() ?? new List<PartnerLedgerAccount>();

        var found = rows.FirstOrDefault(r => r.PartnerId == partnerId);
        if (found != null)
        {
            return found;
        }

        var payable = PartnerLedgerCoding.NextPayableCode(rows.Select(r => r.PayableCode));
        var receivable = PartnerLedgerCoding.NextReceivableCode(rows.Select(r => r.ReceivableCode));

        return new PartnerLedgerAccount(id, partnerId, partnerName, payable, receivable);
    }

    /// <summary>The payable sub-account a partner posts to, or the 2100 parent when it has no registry row.</summary>
    public static string PayableCodeFor(Guid partnerId, IEnumerable<PartnerLedgerAccount> rows) =>
        rows?.FirstOrDefault(r => r.PartnerId == partnerId)?.PayableCode ?? PartnerLedgerCoding.PayableParentCode;

    /// <summary>The receivable sub-account a partner posts to, or the 1250 parent when it has no registry row.</summary>
    public static string ReceivableCodeFor(Guid partnerId, IEnumerable<PartnerLedgerAccount> rows) =>
        rows?.FirstOrDefault(r => r.PartnerId == partnerId)?.ReceivableCode ?? PartnerLedgerCoding.ReceivableParentCode;
}
