using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace Zahy.Connectors;

/// <summary>Maps a partner connector outlet id to an internal Zahy outlet id.</summary>
public class ConnectorBranchMapping : FullAuditedAggregateRoot<Guid>
{
    public Guid PartnerId { get; private set; }

    public Guid TenantId { get; private set; }

    public string ConnectorCode { get; private set; } = string.Empty;

    public string ExternalOutletId { get; private set; } = string.Empty;

    public Guid InternalOutletId { get; private set; }

    public bool IsActive { get; private set; }

    protected ConnectorBranchMapping()
    {
    }

    public ConnectorBranchMapping(
        Guid id,
        Guid partnerId,
        Guid tenantId,
        string connectorCode,
        string externalOutletId,
        Guid internalOutletId)
    {
        Id = id;
        PartnerId = partnerId;
        TenantId = tenantId;
        SetConnectorCode(connectorCode);
        SetExternalOutletId(externalOutletId);
        InternalOutletId = internalOutletId;
        IsActive = true;
    }

    public void SetConnectorCode(string connectorCode)
    {
        Check.NotNullOrWhiteSpace(connectorCode, nameof(connectorCode));
        if (connectorCode.Length > ConnectorConsts.MaxConnectorCodeLength)
        {
            throw new BusinessException(ConnectorErrorCodes.InvalidConnectorCode);
        }

        ConnectorCode = connectorCode.Trim();
    }

    public void SetExternalOutletId(string externalOutletId)
    {
        Check.NotNullOrWhiteSpace(externalOutletId, nameof(externalOutletId));
        if (externalOutletId.Length > ConnectorConsts.MaxExternalIdLength)
        {
            throw new BusinessException(ConnectorErrorCodes.InvalidConnectorRegistration);
        }

        ExternalOutletId = externalOutletId.Trim();
    }

    public void SetInternalOutletId(Guid internalOutletId)
    {
        if (internalOutletId == Guid.Empty)
        {
            throw new BusinessException(ConnectorErrorCodes.InvalidConnectorRegistration);
        }

        InternalOutletId = internalOutletId;
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}
