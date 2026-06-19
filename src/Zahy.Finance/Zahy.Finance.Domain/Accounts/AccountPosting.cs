using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace Zahy.Finance;

/// <summary>Append-only account ledger row — amounts immutable after insert.</summary>
public class AccountPosting : AggregateRoot<Guid>
{
    public FinanceAccountKind AccountKind { get; private set; }

    public Guid AccountId { get; private set; }

    public Guid? PartnerId { get; private set; }

    public Guid? TenantId { get; private set; }

    public decimal PostingAmount { get; private set; }

    public string Currency { get; private set; } = FinanceConsts.DefaultCurrency;

    public DateTime PostedAt { get; private set; }

    public FinancePostingSourceModule SourceModule { get; private set; }

    public string SourceType { get; private set; } = string.Empty;

    public string SourceId { get; private set; } = string.Empty;

    public Guid? SourceRowId { get; private set; }

    public string IdempotencyKey { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public Guid? ReversesPostingId { get; private set; }

    protected AccountPosting()
    {
    }

    public static AccountPosting Create(
        Guid id,
        FinanceAccountKind accountKind,
        Guid accountId,
        Guid? partnerId,
        Guid? tenantId,
        decimal postingAmount,
        string idempotencyKey,
        DateTime postedAt,
        FinancePostingSourceModule sourceModule,
        string sourceType,
        string sourceId,
        Guid? sourceRowId = null,
        string currency = FinanceConsts.DefaultCurrency,
        string? description = null,
        Guid? reversesPostingId = null)
    {
        ValidateIdempotencyKey(idempotencyKey);
        ValidateSource(sourceType, sourceId);

        return new AccountPosting
        {
            Id = id,
            AccountKind = accountKind,
            AccountId = accountId,
            PartnerId = partnerId,
            TenantId = tenantId,
            PostingAmount = FinanceMoney.RoundPosting(postingAmount),
            Currency = NormalizeCurrency(currency),
            PostedAt = postedAt,
            SourceModule = sourceModule,
            SourceType = sourceType.Trim(),
            SourceId = sourceId.Trim(),
            SourceRowId = sourceRowId,
            IdempotencyKey = idempotencyKey.Trim(),
            Description = description?.Trim(),
            ReversesPostingId = reversesPostingId
        };
    }

    private static void ValidateIdempotencyKey(string idempotencyKey)
    {
        Check.NotNullOrWhiteSpace(idempotencyKey, nameof(idempotencyKey));

        if (idempotencyKey.Length > FinanceConsts.MaxIdempotencyKeyLength)
        {
            throw new BusinessException(FinanceErrorCodes.InvalidPosting);
        }
    }

    private static void ValidateSource(string sourceType, string sourceId)
    {
        Check.NotNullOrWhiteSpace(sourceType, nameof(sourceType));
        Check.NotNullOrWhiteSpace(sourceId, nameof(sourceId));
    }

    private static string NormalizeCurrency(string currency)
    {
        Check.NotNullOrWhiteSpace(currency, nameof(currency));
        return currency.Trim().ToUpperInvariant();
    }
}
