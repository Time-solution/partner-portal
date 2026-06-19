using System.Text.Json;
using System.Text.Json.Serialization;
using Volo.Abp;

namespace Zahy.Commission;

internal static class CommissionBasisDefinitionJson
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string ToJson(CommissionBasisDefinition basisDefinition) =>
        JsonSerializer.Serialize(basisDefinition, JsonOptions);

    public static CommissionBasisDefinition Parse(string json)
    {
        Check.NotNullOrWhiteSpace(json, nameof(json));
        return JsonSerializer.Deserialize<CommissionBasisDefinition>(json, JsonOptions)
               ?? throw new BusinessException(CommissionErrorCodes.InvalidBasisDefinition);
    }
}
