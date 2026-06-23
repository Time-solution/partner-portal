using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Zahy.PartnerCatalog;

/// <summary>
/// R1 — immutable VALUE OBJECT describing the exact SCOPE a partner grants to a merchant: which
/// offerings/tiers are shared, which data fields flow each way (PDPL), what actions are allowed, within
/// which zones/limits and validity window. The merchant consents to a SPECIFIC scope CONTENT, captured by
/// <see cref="Hash"/>.
///
/// <para><see cref="Hash"/> is a pure function of the NORMALIZED content (lists trimmed, de-duplicated and
/// ordered; limit keys sorted), so it is stable across reordering / re-creation and changes ONLY when the
/// scope content actually changes. Two scopes are equal iff their hashes match.</para>
/// </summary>
public sealed class GrantedScope : IEquatable<GrantedScope>
{
    public IReadOnlyList<string> Offerings { get; }
    public IReadOnlyList<string> Tiers { get; }

    /// <summary>PDPL: merchant data fields the partner is allowed to receive.</summary>
    public IReadOnlyList<string> DataFieldsToPartner { get; }

    /// <summary>PDPL: partner data fields the merchant is allowed to receive.</summary>
    public IReadOnlyList<string> DataFieldsToMerchant { get; }

    public IReadOnlyList<string> AllowedActions { get; }
    public IReadOnlyList<string> Zones { get; }

    /// <summary>Named numeric limits (e.g. "MonthlyOrders" =&gt; 5000). Sorted by key for the hash.</summary>
    public IReadOnlyDictionary<string, decimal> Limits { get; }

    public DateTime? ValidFrom { get; }
    public DateTime? ValidUntil { get; }

    /// <summary>
    /// True = a PUBLIC / standing grant, where a merchant activation may stand in as the consent act
    /// (see <c>PartnerMerchantLink.AcceptByActivation</c>). False = a private grant requiring explicit consent.
    /// </summary>
    public bool IsStandingGrant { get; }

    /// <summary>Stable content hash (SHA-256, hex). Equal content =&gt; equal hash; any real change =&gt; new hash.</summary>
    public string Hash { get; }

    private GrantedScope(
        IReadOnlyList<string> offerings,
        IReadOnlyList<string> tiers,
        IReadOnlyList<string> dataFieldsToPartner,
        IReadOnlyList<string> dataFieldsToMerchant,
        IReadOnlyList<string> allowedActions,
        IReadOnlyList<string> zones,
        IReadOnlyDictionary<string, decimal> limits,
        DateTime? validFrom,
        DateTime? validUntil,
        bool isStandingGrant,
        string hash)
    {
        Offerings = offerings;
        Tiers = tiers;
        DataFieldsToPartner = dataFieldsToPartner;
        DataFieldsToMerchant = dataFieldsToMerchant;
        AllowedActions = allowedActions;
        Zones = zones;
        Limits = limits;
        ValidFrom = validFrom;
        ValidUntil = validUntil;
        IsStandingGrant = isStandingGrant;
        Hash = hash;
    }

    public static GrantedScope Create(
        IEnumerable<string>? offerings = null,
        IEnumerable<string>? tiers = null,
        IEnumerable<string>? dataFieldsToPartner = null,
        IEnumerable<string>? dataFieldsToMerchant = null,
        IEnumerable<string>? allowedActions = null,
        IEnumerable<string>? zones = null,
        IReadOnlyDictionary<string, decimal>? limits = null,
        DateTime? validFrom = null,
        DateTime? validUntil = null,
        bool isStandingGrant = false)
    {
        var off = NormalizeList(offerings);
        var tie = NormalizeList(tiers);
        var dfp = NormalizeList(dataFieldsToPartner);
        var dfm = NormalizeList(dataFieldsToMerchant);
        var act = NormalizeList(allowedActions);
        var zon = NormalizeList(zones);
        var lim = NormalizeLimits(limits);

        var hash = ComputeHash(off, tie, dfp, dfm, act, zon, lim, validFrom, validUntil, isStandingGrant);

        return new GrantedScope(off, tie, dfp, dfm, act, zon, lim, validFrom, validUntil, isStandingGrant, hash);
    }

    private static IReadOnlyList<string> NormalizeList(IEnumerable<string>? values) =>
        (values ?? Enumerable.Empty<string>())
            .Select(v => (v ?? string.Empty).Trim())
            .Where(v => v.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(v => v, StringComparer.Ordinal)
            .ToArray();

    private static IReadOnlyDictionary<string, decimal> NormalizeLimits(IReadOnlyDictionary<string, decimal>? limits)
    {
        var result = new SortedDictionary<string, decimal>(StringComparer.Ordinal);
        if (limits is null)
        {
            return result;
        }

        foreach (var kv in limits)
        {
            if (string.IsNullOrWhiteSpace(kv.Key))
            {
                continue;
            }

            result[kv.Key.Trim()] = kv.Value;
        }

        return result;
    }

    private static string ComputeHash(
        IReadOnlyList<string> offerings,
        IReadOnlyList<string> tiers,
        IReadOnlyList<string> dataFieldsToPartner,
        IReadOnlyList<string> dataFieldsToMerchant,
        IReadOnlyList<string> allowedActions,
        IReadOnlyList<string> zones,
        IReadOnlyDictionary<string, decimal> limits,
        DateTime? validFrom,
        DateTime? validUntil,
        bool isStandingGrant)
    {
        var sb = new StringBuilder();
        AppendList(sb, "off", offerings);
        AppendList(sb, "tie", tiers);
        AppendList(sb, "dfp", dataFieldsToPartner);
        AppendList(sb, "dfm", dataFieldsToMerchant);
        AppendList(sb, "act", allowedActions);
        AppendList(sb, "zon", zones);

        sb.Append("lim=");
        sb.Append(string.Join(
            ",",
            limits.Select(kv => kv.Key + ":" + kv.Value.ToString(CultureInfo.InvariantCulture))));
        sb.Append(';');

        sb.Append("vf=").Append(validFrom?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty).Append(';');
        sb.Append("vu=").Append(validUntil?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty).Append(';');
        sb.Append("std=").Append(isStandingGrant ? "1" : "0");

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(bytes);
    }

    private static void AppendList(StringBuilder sb, string name, IReadOnlyList<string> values)
    {
        sb.Append(name).Append('=').Append(string.Join(",", values)).Append(';');
    }

    public bool Equals(GrantedScope? other) =>
        other is not null && string.Equals(Hash, other.Hash, StringComparison.Ordinal);

    public override bool Equals(object? obj) => Equals(obj as GrantedScope);

    public override int GetHashCode() => Hash.GetHashCode(StringComparison.Ordinal);
}
