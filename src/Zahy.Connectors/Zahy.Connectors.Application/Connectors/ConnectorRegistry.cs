using Volo.Abp;

namespace Zahy.Connectors;

public class ConnectorRegistry : IConnectorRegistry
{
    private readonly IReadOnlyDictionary<string, IPartnerConnector> _byCode;
    private readonly IReadOnlyDictionary<PartnerPlatform.Partners.PartnerType, string> _defaultCodeByPartnerType;

    public ConnectorRegistry(IEnumerable<IPartnerConnector> connectors)
    {
        Check.NotNull(connectors, nameof(connectors));

        _byCode = connectors.ToDictionary(
            x => x.Descriptor.ConnectorCode,
            x => x,
            StringComparer.OrdinalIgnoreCase);

        _defaultCodeByPartnerType = new Dictionary<PartnerPlatform.Partners.PartnerType, string>
        {
            [PartnerPlatform.Partners.PartnerType.Aggregator] = ConnectorConsts.MockAggregatorCode,
            [PartnerPlatform.Partners.PartnerType.ThreePL] = ConnectorConsts.MockThreePLCode,
            [PartnerPlatform.Partners.PartnerType.Carrier] = ConnectorConsts.MockCarrierCode
        };
    }

    public IPartnerConnector Resolve(string connectorCode)
    {
        Check.NotNullOrWhiteSpace(connectorCode, nameof(connectorCode));

        if (_byCode.TryGetValue(connectorCode, out var connector))
        {
            return connector;
        }

        throw new BusinessException(ConnectorErrorCodes.ConnectorNotFound)
            .WithData("ConnectorCode", connectorCode);
    }

    public IPartnerConnector ResolveByPartnerType(
        PartnerPlatform.Partners.PartnerType partnerType,
        string? connectorCode = null)
    {
        var resolvedCode = connectorCode;
        if (string.IsNullOrWhiteSpace(resolvedCode))
        {
            if (!_defaultCodeByPartnerType.TryGetValue(partnerType, out resolvedCode!))
            {
                throw new BusinessException(ConnectorErrorCodes.ConnectorNotSupportedForPartnerType)
                    .WithData("PartnerType", partnerType);
            }
        }

        return Resolve(resolvedCode);
    }

    public IReadOnlyList<ConnectorDescriptor> ListDescriptors() =>
        _byCode.Values.Select(x => x.Descriptor).OrderBy(x => x.ConnectorCode).ToList();
}
