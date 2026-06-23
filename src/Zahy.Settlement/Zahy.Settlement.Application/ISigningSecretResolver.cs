using System;
using System.Collections.Generic;
using Microsoft.Extensions.Options;

namespace Zahy.Settlement;

/// <summary>
/// Resolves the HMAC signing secret for an inbound settlement webhook from a trusted server-side store,
/// keyed by (Book, PartnerId). The secret is NEVER taken from the caller-supplied payload. The real
/// implementation reads a per-partner/per-provider vault; the config/mock impl below is the seam.
/// </summary>
public interface ISigningSecretResolver
{
    string? Resolve(SettlementBook book, Guid partnerId);
}

/// <summary>
/// Config-backed resolver: a per-book override map with a single default secret fallback. This is the
/// non-production seam — the live vault wire-up (per-partner secret rotation) is gated.
/// </summary>
public sealed class ConfigSigningSecretResolver : ISigningSecretResolver
{
    private readonly SettlementWebhookSecurityOptions _options;

    public ConfigSigningSecretResolver(IOptions<SettlementWebhookSecurityOptions> options)
    {
        _options = options.Value;
    }

    public string? Resolve(SettlementBook book, Guid partnerId)
    {
        if (_options.SecretsByBook.TryGetValue(book, out var bookSecret) && !string.IsNullOrEmpty(bookSecret))
        {
            return bookSecret;
        }

        return string.IsNullOrEmpty(_options.DefaultSecret) ? null : _options.DefaultSecret;
    }
}

public sealed class SettlementWebhookSecurityOptions
{
    public const string SectionName = "Settlement:WebhookSecurity";

    /// <summary>Replay window in seconds; a webhook whose timestamp drifts further than this is rejected.</summary>
    public int FreshnessWindowSeconds { get; set; } = 300;

    /// <summary>Fallback secret when no per-book secret is configured. Sourced from config/secret store, never code.</summary>
    public string? DefaultSecret { get; set; }

    /// <summary>Optional per-book secret overrides.</summary>
    public Dictionary<SettlementBook, string> SecretsByBook { get; set; } = new();
}
