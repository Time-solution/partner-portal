using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Timing;
using Zahy.Webhooks;

namespace Zahy.Connectors;

/// <summary>Inbound connector boundary security knobs. Verification is gated OFF until a live receive endpoint is wired.</summary>
public sealed class ConnectorInboundSecurityOptions
{
    public const string SectionName = "Connectors:InboundSecurity";

    /// <summary>When false, inbound HMAC verification is skipped (no live endpoint yet). Defaults OFF.</summary>
    public bool VerifySignature { get; set; }

    /// <summary>Replay window in seconds; a request whose timestamp drifts further than this is rejected.</summary>
    public int FreshnessWindowSeconds { get; set; } = 300;

    /// <summary>Optional config-backed secret map keyed by ConnectorRegistration.SecretReference (dev/mock only).</summary>
    public Dictionary<string, string> Secrets { get; set; } = new();
}

/// <summary>
/// Resolves a connector's signing secret from its <c>SecretReference</c> (a vault key, never the value).
/// The config impl below is the dev/mock seam; the live per-merchant credential vault is a gated wire-up.
/// </summary>
public interface IConnectorSecretResolver
{
    string? Resolve(string secretReference);
}

public sealed class ConfigConnectorSecretResolver : IConnectorSecretResolver, ITransientDependency
{
    private readonly ConnectorInboundSecurityOptions _options;

    public ConfigConnectorSecretResolver(IOptions<ConnectorInboundSecurityOptions> options)
    {
        _options = options.Value;
    }

    public string? Resolve(string secretReference)
    {
        if (string.IsNullOrWhiteSpace(secretReference))
        {
            return null;
        }

        return _options.Secrets.TryGetValue(secretReference, out var secret) && !string.IsNullOrEmpty(secret)
            ? secret
            : null;
    }
}

/// <summary>
/// Rejects replays by remembering processed (sourceSystem, nonce) pairs — mirrors Settlement's
/// (Book, ExternalEventId) dedupe. The in-memory impl is the seam; the persisted EF-backed store
/// (durable across restarts) is the gated production wire-up.
/// </summary>
public interface IConnectorReplayGuard
{
    /// <summary>Returns true if this (sourceSystem, nonce) is new; false if it was already seen (a replay).</summary>
    Task<bool> TryRegisterAsync(string sourceSystem, string nonce, CancellationToken cancellationToken = default);
}

public sealed class InMemoryConnectorReplayGuard : IConnectorReplayGuard, ISingletonDependency
{
    private readonly ConcurrentDictionary<string, byte> _seen = new(StringComparer.Ordinal);

    public Task<bool> TryRegisterAsync(string sourceSystem, string nonce, CancellationToken cancellationToken = default)
    {
        var key = $"{sourceSystem}|{nonce}";
        return Task.FromResult(_seen.TryAdd(key, 1));
    }
}

/// <summary>Outcome of verifying an inbound connector request.</summary>
public enum ConnectorSignatureStatus
{
    Valid = 0,
    Missing = 1,
    Invalid = 2,
    Stale = 3,
    NoSecret = 4,
    Replay = 5
}

/// <summary>
/// Verifies the HMAC of an inbound connector request over its raw body, enforces the replay window via an
/// injected clock, and dedupes the nonce. The secret is resolved from the registration's SecretReference —
/// never from caller input.
/// </summary>
public interface IConnectorInboundVerifier
{
    Task<ConnectorSignatureStatus> VerifyAsync(
        string secretReference,
        string sourceSystem,
        ReceiveOrderRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class ConnectorInboundVerifier : IConnectorInboundVerifier, ITransientDependency
{
    private readonly IConnectorSecretResolver _secretResolver;
    private readonly IConnectorReplayGuard _replayGuard;
    private readonly IClock _clock;
    private readonly ConnectorInboundSecurityOptions _options;

    public ConnectorInboundVerifier(
        IConnectorSecretResolver secretResolver,
        IConnectorReplayGuard replayGuard,
        IClock clock,
        IOptions<ConnectorInboundSecurityOptions> options)
    {
        _secretResolver = secretResolver;
        _replayGuard = replayGuard;
        _clock = clock;
        _options = options.Value;
    }

    public async Task<ConnectorSignatureStatus> VerifyAsync(
        string secretReference,
        string sourceSystem,
        ReceiveOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Signature))
        {
            return ConnectorSignatureStatus.Missing;
        }

        var secret = _secretResolver.Resolve(secretReference);
        if (string.IsNullOrEmpty(secret))
        {
            return ConnectorSignatureStatus.NoSecret;
        }

        var nowUnixSeconds = new DateTimeOffset(_clock.Now.ToUniversalTime()).ToUnixTimeSeconds();

        var outcome = WebhookHmacSigner.VerifyWithFreshness(
            secret,
            request.RawPayload,
            request.UnixTimestamp,
            request.Signature!,
            nowUnixSeconds,
            _options.FreshnessWindowSeconds);

        if (outcome == WebhookVerificationOutcome.InvalidSignature)
        {
            return ConnectorSignatureStatus.Invalid;
        }

        if (outcome == WebhookVerificationOutcome.Stale)
        {
            return ConnectorSignatureStatus.Stale;
        }

        var nonce = string.IsNullOrWhiteSpace(request.Nonce) ? request.ExternalOrderId : request.Nonce!;
        var isNew = await _replayGuard.TryRegisterAsync(sourceSystem, nonce, cancellationToken);
        return isNew ? ConnectorSignatureStatus.Valid : ConnectorSignatureStatus.Replay;
    }
}
