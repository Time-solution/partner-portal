using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Zahy.Settlement;

/// <summary>
/// Content hash for statement import idempotency (the Webhook-Hub/idempotency-key pattern applied
/// to file content): SHA-256 over a canonical, culture-invariant rendering of the statement.
/// Identical content ⇒ identical key ⇒ re-import is a no-op with zero duplicate rows. Line order
/// does not change the hash (lines are canonically sorted), so a re-exported but reshuffled
/// statement still dedupes.
/// </summary>
public static class AggregatorStatementContentHash
{
    public sealed record LineContent(string ExternalOrderRef, DateTime OrderDate, decimal Gross, decimal AggregatorFee, decimal Net);

    public static string Compute(
        Guid partnerId,
        string source,
        DateTime periodFrom,
        DateTime periodTo,
        decimal declaredGross,
        decimal declaredFees,
        decimal declaredNet,
        string currency,
        IEnumerable<LineContent> lines)
    {
        var canonicalLines = (lines ?? Array.Empty<LineContent>())
            .Select(l => string.Join('|',
                (l.ExternalOrderRef ?? string.Empty).Trim().ToLowerInvariant(),
                l.OrderDate.ToString("O", CultureInfo.InvariantCulture),
                Canonical(l.Gross),
                Canonical(l.AggregatorFee),
                Canonical(l.Net)))
            .OrderBy(x => x, StringComparer.Ordinal);

        var payload = string.Join('\n',
            new[]
            {
                partnerId.ToString("D"),
                (source ?? string.Empty).Trim().ToLowerInvariant(),
                periodFrom.ToString("O", CultureInfo.InvariantCulture),
                periodTo.ToString("O", CultureInfo.InvariantCulture),
                Canonical(declaredGross),
                Canonical(declaredFees),
                Canonical(declaredNet),
                (currency ?? string.Empty).Trim().ToUpperInvariant()
            }.Concat(canonicalLines));

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string Canonical(decimal value) =>
        SettlementMoney.Round(value).ToString("0.00", CultureInfo.InvariantCulture);
}
