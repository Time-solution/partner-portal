using System;
using System.Text.Json;
using System.Security.Cryptography;

namespace Zahy.Webhooks;

public static class WebhookRetryPolicy
{
    private static readonly TimeSpan[] DefaultDelays =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(2),
        TimeSpan.FromHours(12)
    ];

    public static DateTime GetNextAttemptUtc(int attemptCountAfterFailure, DateTime utcNow)
    {
        var index = Math.Clamp(attemptCountAfterFailure - 1, 0, DefaultDelays.Length - 1);
        return utcNow.Add(DefaultDelays[index]);
    }

    public static bool HasExceededMaxAttempts(int attemptCount, int maxAttempts = WebhookConsts.DefaultMaxDeliveryAttempts) =>
        attemptCount >= maxAttempts;
}

public static class WebhookPayloadFilter
{
    public static bool Matches(WebhookFilterRules? rules, string payloadJson)
    {
        if (rules == null)
        {
            return true;
        }

        using var document = JsonDocument.Parse(payloadJson);
        var root = document.RootElement;

        if (rules.TenantIds is { Count: > 0 })
        {
            if (!TryGetGuid(root, "tenantId", out var tenantId) || !rules.TenantIds.Contains(tenantId))
            {
                return false;
            }
        }

        if (rules.Directions is { Count: > 0 })
        {
            if (!TryGetString(root, "direction", out var direction) ||
                !rules.Directions.Contains(direction, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        if (rules.MinAmountSar is > 0)
        {
            if (!TryGetDecimal(root, "totalAmount", out var amount) && !TryGetDecimal(root, "totalSar", out amount))
            {
                return false;
            }

            if (amount < rules.MinAmountSar.Value)
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryGetGuid(JsonElement root, string property, out Guid value)
    {
        value = default;
        if (!root.TryGetProperty(property, out var element))
        {
            return false;
        }

        return element.ValueKind == JsonValueKind.String && Guid.TryParse(element.GetString(), out value);
    }

    private static bool TryGetString(JsonElement root, string property, out string value)
    {
        value = string.Empty;
        if (!root.TryGetProperty(property, out var element) || element.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = element.GetString() ?? string.Empty;
        return true;
    }

    private static bool TryGetDecimal(JsonElement root, string property, out decimal value)
    {
        value = default;
        if (!root.TryGetProperty(property, out var element))
        {
            return false;
        }

        return element.ValueKind == JsonValueKind.Number && element.TryGetDecimal(out value);
    }
}

public static class WebhookSecretGenerator
{
    public static string Generate(int byteLength = 32) =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(byteLength));
}
