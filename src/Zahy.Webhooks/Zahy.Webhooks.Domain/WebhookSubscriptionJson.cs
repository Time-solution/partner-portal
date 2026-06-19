using System.Text.Json;

namespace Zahy.Webhooks;

internal static class WebhookSubscriptionJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string ToJson<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static List<string> ParseStringList(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<string>>(json, Options) ?? [];
    }

    public static WebhookFilterRules? ParseFilterRules(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<WebhookFilterRules>(json, Options);
    }
}
