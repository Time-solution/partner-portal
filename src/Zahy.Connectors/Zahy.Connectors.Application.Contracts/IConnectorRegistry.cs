using System.Collections.Generic;
using Zahy.PartnerPlatform.Partners;

namespace Zahy.Connectors;

public interface IConnectorRegistry
{
    IPartnerConnector Resolve(string connectorCode);

    IPartnerConnector ResolveByPartnerType(PartnerType partnerType, string? connectorCode = null);

    IReadOnlyList<ConnectorDescriptor> ListDescriptors();
}
