using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace Zahy.Connectors;

/// <summary>
/// Per-partner connector registration metadata. Secrets are referenced, never stored in SQL.
/// </summary>
public class ConnectorRegistration : FullAuditedAggregateRoot<Guid>
{
    public Guid PartnerId { get; private set; }

    public Guid TenantId { get; private set; }

    public string ConnectorCode { get; private set; } = string.Empty;

    public ConnectorKind ConnectorKind { get; private set; }

    public bool IsEnabled { get; private set; }

    public string DisplayName { get; private set; } = string.Empty;

    public string? ConfigJson { get; private set; }

    /// <summary>Vault or configuration key reference — never the secret value.</summary>
    public string SecretReference { get; private set; } = string.Empty;

    protected ConnectorRegistration()
    {
    }

    public ConnectorRegistration(
        Guid id,
        Guid partnerId,
        Guid tenantId,
        string connectorCode,
        ConnectorKind connectorKind,
        string displayName,
        string secretReference,
        string? configJson = null)
    {
        Id = id;
        PartnerId = partnerId;
        TenantId = tenantId;
        ConnectorKind = connectorKind;
        SetConnectorCode(connectorCode);
        SetDisplayName(displayName);
        SetSecretReference(secretReference);
        SetConfigJson(configJson);
        IsEnabled = true;
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

    public void SetDisplayName(string displayName)
    {
        Check.NotNullOrWhiteSpace(displayName, nameof(displayName));
        if (displayName.Length > ConnectorConsts.MaxDisplayNameLength)
        {
            throw new BusinessException(ConnectorErrorCodes.InvalidConnectorRegistration);
        }

        DisplayName = displayName.Trim();
    }

    public void SetSecretReference(string secretReference)
    {
        Check.NotNullOrWhiteSpace(secretReference, nameof(secretReference));
        if (secretReference.Length > ConnectorConsts.MaxSecretReferenceLength)
        {
            throw new BusinessException(ConnectorErrorCodes.InvalidConnectorRegistration);
        }

        SecretReference = secretReference.Trim();
    }

    public void SetConfigJson(string? configJson)
    {
        if (configJson != null && configJson.Length > ConnectorConsts.MaxConfigJsonLength)
        {
            throw new BusinessException(ConnectorErrorCodes.InvalidConnectorRegistration);
        }

        ConfigJson = string.IsNullOrWhiteSpace(configJson) ? null : configJson.Trim();
    }

    public void Enable() => IsEnabled = true;

    public void Disable() => IsEnabled = false;
}
