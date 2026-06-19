namespace Zahy.Connectors;

public sealed class CanonicalOutlet
{
    public string ExternalOutletId { get; init; } = string.Empty;

    public Guid? InternalOutletId { get; init; }

    public Guid TenantId { get; init; }

    public string NameEn { get; init; } = string.Empty;

    public string? NameAr { get; init; }

    public bool IsActive { get; init; } = true;

    public string? Timezone { get; init; }
}
