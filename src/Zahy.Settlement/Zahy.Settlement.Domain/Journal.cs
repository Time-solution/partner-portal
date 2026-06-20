using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace Zahy.Settlement;

/// <summary>
/// An append-only double-entry journal. It is created balanced or not at all: the factory rejects
/// degenerate (fewer than two legs), multi-currency, and unbalanced (debits != credits) journals.
/// Posted journals are never mutated; corrections are made by posting a <see cref="Reverse"/> journal.
/// </summary>
public class Journal : AggregateRoot<Guid>
{
    private readonly List<JournalLine> _lines = new();

    public string Currency { get; private set; } = SettlementConsts.DefaultCurrency;

    public DateTime PostedAt { get; private set; }

    public string? Reference { get; private set; }

    /// <summary>When set, this journal reverses the referenced journal (correction trail).</summary>
    public Guid? ReversesJournalId { get; private set; }

    public IReadOnlyList<JournalLine> Lines => new ReadOnlyCollection<JournalLine>(_lines);

    protected Journal()
    {
    }

    private Journal(
        Guid id,
        DateTime postedAt,
        string currency,
        string? reference,
        Guid? reversesJournalId,
        List<JournalLine> lines)
        : base(id)
    {
        PostedAt = postedAt;
        Currency = currency;
        Reference = reference;
        ReversesJournalId = reversesJournalId;
        _lines = lines;
    }

    public static Journal Create(
        Guid id,
        DateTime postedAt,
        IEnumerable<JournalLine> lines,
        string? reference = null) =>
        CreateInternal(id, postedAt, lines, reference, reversesJournalId: null);

    private static Journal CreateInternal(
        Guid id,
        DateTime postedAt,
        IEnumerable<JournalLine> lines,
        string? reference,
        Guid? reversesJournalId)
    {
        var lineList = lines?.ToList() ?? new List<JournalLine>();

        if (lineList.Count < 2)
        {
            throw new BusinessException(SettlementErrorCodes.DegenerateJournal)
                .WithData("LineCount", lineList.Count);
        }

        var currency = lineList[0].Amount.Currency;
        if (lineList.Any(l => !string.Equals(l.Amount.Currency, currency, StringComparison.Ordinal)))
        {
            throw new BusinessException(SettlementErrorCodes.CurrencyMismatch)
                .WithData("Currencies", string.Join(",", lineList.Select(l => l.Amount.Currency).Distinct()));
        }

        var debits = SettlementMoney.Round(lineList
            .Where(l => l.Direction == EntryDirection.Debit)
            .Sum(l => l.Amount.Amount));

        var credits = SettlementMoney.Round(lineList
            .Where(l => l.Direction == EntryDirection.Credit)
            .Sum(l => l.Amount.Amount));

        if (debits != credits)
        {
            throw new BusinessException(SettlementErrorCodes.UnbalancedJournal)
                .WithData("TotalDebits", debits)
                .WithData("TotalCredits", credits);
        }

        return new Journal(id, postedAt, currency, reference, reversesJournalId, lineList);
    }

    public Money TotalDebits =>
        Money.Of(_lines.Where(l => l.Direction == EntryDirection.Debit).Sum(l => l.Amount.Amount), Currency);

    public Money TotalCredits =>
        Money.Of(_lines.Where(l => l.Direction == EntryDirection.Credit).Sum(l => l.Amount.Amount), Currency);

    public bool IsBalanced => TotalDebits.Amount == TotalCredits.Amount;

    /// <summary>
    /// Returns a new balanced journal that reverses this one — every leg keeps its account and amount
    /// but flips Debit&lt;-&gt;Credit. This is the only sanctioned correction mechanism (no in-place edits).
    /// </summary>
    public Journal Reverse(Guid newId, DateTime postedAt, string? reference = null)
    {
        var reversedLines = _lines.Select(l => JournalLine.Of(
            l.Account,
            l.Direction == EntryDirection.Debit ? EntryDirection.Credit : EntryDirection.Debit,
            l.Amount));

        return CreateInternal(
            newId,
            postedAt,
            reversedLines,
            reference ?? $"Reversal of {Id:D}",
            reversesJournalId: Id);
    }
}
