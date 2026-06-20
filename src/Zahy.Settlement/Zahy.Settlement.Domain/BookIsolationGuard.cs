using System.Linq;
using Volo.Abp;

namespace Zahy.Settlement;

/// <summary>
/// Enforces book isolation: a journal posted within a book may only touch that book's account tree.
/// Cross-book postings are rejected, so each partner type's books stay independent (DESIGN.md §6).
/// Reuses the Phase-1 <see cref="Journal"/> primitive unchanged.
/// </summary>
public static class BookIsolationGuard
{
    public static void EnsureWithinBook(ISettlementFlowProfile profile, Journal journal)
    {
        var foreign = journal.Lines.FirstOrDefault(line => !profile.Owns(line.Account));
        if (foreign != null)
        {
            throw new BusinessException(SettlementCaseErrorCodes.AccountNotInBook)
                .WithData("Book", profile.Book.ToString())
                .WithData("Account", foreign.Account.ToString());
        }
    }
}
