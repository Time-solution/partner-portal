using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Volo.Abp;

namespace Zahy.Settlement;

/// <summary>
/// The output of a posting template: the code-based journal lines (empty for a non-posting flow)
/// plus the DERIVED summary figures (margin, net VAT) — which are summaries, never journal lines.
/// A financial result is created balanced or not at all (debits = credits, single currency).
/// </summary>
public sealed class PostingResult
{
    private readonly List<PostingLine> _lines;

    public ParticipationMode Mode { get; }

    public IReadOnlyList<PostingLine> Lines => new ReadOnlyCollection<PostingLine>(_lines);

    /// <summary>Derived summary: net sell − net buy (0 when not applicable). Not a journal line.</summary>
    public Money Margin { get; }

    /// <summary>Derived summary: output VAT − input VAT (what is owed to ZATCA). Not a journal line.</summary>
    public Money NetVat { get; }

    /// <summary>True when this flow produces financial journal lines (Principal / SubscriptionFee).</summary>
    public bool IsFinancial => _lines.Count > 0;

    // Grouping tags (set via Tag) so reports can group by partner / merchant / period / order / batch.
    public Guid? PartnerId { get; private set; }

    public Guid? MerchantId { get; private set; }

    public SettlementPeriod? Period { get; private set; }

    public string? OrderRef { get; private set; }

    public string? BatchRef { get; private set; }

    private PostingResult(ParticipationMode mode, List<PostingLine> lines, Money margin, Money netVat)
    {
        Mode = mode;
        _lines = lines;
        Margin = margin;
        NetVat = netVat;
    }

    /// <summary>
    /// Returns a copy carrying the grouping tags (PartnerId, MerchantId, Period, OrderRef, optional
    /// BatchRef) so the read-model reports can group and filter. The journal lines are unchanged.
    /// </summary>
    public PostingResult Tag(
        Guid partnerId,
        Guid merchantId,
        SettlementPeriod period,
        string orderRef,
        string? batchRef = null)
    {
        return new PostingResult(Mode, _lines, Margin, NetVat)
        {
            PartnerId = partnerId,
            MerchantId = merchantId,
            Period = period,
            OrderRef = orderRef,
            BatchRef = batchRef
        };
    }

    public static PostingResult Financial(
        ParticipationMode mode,
        IEnumerable<PostingLine> lines,
        Money margin,
        Money netVat)
    {
        var lineList = lines?.ToList() ?? new List<PostingLine>();

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

        var debits = SettlementMoney.Round(lineList.Where(l => l.Direction == EntryDirection.Debit).Sum(l => l.Amount.Amount));
        var credits = SettlementMoney.Round(lineList.Where(l => l.Direction == EntryDirection.Credit).Sum(l => l.Amount.Amount));

        if (debits != credits)
        {
            throw new BusinessException(SettlementErrorCodes.UnbalancedJournal)
                .WithData("TotalDebits", debits)
                .WithData("TotalCredits", credits);
        }

        return new PostingResult(mode, lineList, margin, netVat);
    }

    public static PostingResult NonPosting(ParticipationMode mode, string currency = SettlementConsts.DefaultCurrency) =>
        new(mode, new List<PostingLine>(), Money.Zero(currency), Money.Zero(currency));

    public Money TotalDebits =>
        Money.Of(_lines.Where(l => l.Direction == EntryDirection.Debit).Sum(l => l.Amount.Amount),
            _lines.Count > 0 ? _lines[0].Amount.Currency : SettlementConsts.DefaultCurrency);

    public Money TotalCredits =>
        Money.Of(_lines.Where(l => l.Direction == EntryDirection.Credit).Sum(l => l.Amount.Amount),
            _lines.Count > 0 ? _lines[0].Amount.Currency : SettlementConsts.DefaultCurrency);

    public bool IsBalanced => TotalDebits.Amount == TotalCredits.Amount;

    /// <summary>The signed trial-balance total (debits positive, credits negative). Always zero when balanced.</summary>
    public decimal TrialBalanceNet =>
        SettlementMoney.Round(TotalDebits.Amount - TotalCredits.Amount);

    /// <summary>The single line posting to <paramref name="accountCode"/> on <paramref name="direction"/>, or null.</summary>
    public PostingLine? LineFor(string accountCode, EntryDirection direction) =>
        _lines.SingleOrDefault(l => l.AccountCode == accountCode && l.Direction == direction);
}
