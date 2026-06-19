namespace Zahy.Connectors;

public abstract class MockConnectorBase : IPartnerConnector
{
    protected MockConnectorBase(ConnectorDescriptor descriptor)
    {
        Descriptor = descriptor;
    }

    public ConnectorDescriptor Descriptor { get; }

    public virtual Task<ConnectorResult<CanonicalMenuSyncResult>> SyncMenuAsync(
        ConnectorContext context,
        CanonicalMenuSyncRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Supports(ConnectorOperations.SyncMenu))
        {
            return Task.FromResult(ConnectorResult<CanonicalMenuSyncResult>.NotSupported(nameof(SyncMenuAsync)));
        }

        return Task.FromResult(ConnectorResult<CanonicalMenuSyncResult>.Ok(new CanonicalMenuSyncResult
        {
            Items = [],
            SyncedAt = DateTime.UtcNow
        }));
    }

    public virtual Task<ConnectorResult<CanonicalOutlet>> MapBranchAsync(
        ConnectorContext context,
        CanonicalOutlet outlet,
        CancellationToken cancellationToken = default)
    {
        if (!Supports(ConnectorOperations.MapBranch))
        {
            return Task.FromResult(ConnectorResult<CanonicalOutlet>.NotSupported(nameof(MapBranchAsync)));
        }

        return Task.FromResult(ConnectorResult<CanonicalOutlet>.Ok(new CanonicalOutlet
        {
            ExternalOutletId = outlet.ExternalOutletId,
            InternalOutletId = outlet.InternalOutletId ?? Guid.NewGuid(),
            TenantId = outlet.TenantId,
            NameEn = outlet.NameEn,
            NameAr = outlet.NameAr,
            IsActive = outlet.IsActive,
            Timezone = outlet.Timezone
        }));
    }

    public virtual Task<ConnectorResult<CanonicalOrder>> ReceiveOrderAsync(
        ConnectorContext context,
        ReceiveOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Supports(ConnectorOperations.ReceiveOrder))
        {
            return Task.FromResult(ConnectorResult<CanonicalOrder>.NotSupported(nameof(ReceiveOrderAsync)));
        }

        return Task.FromResult(ConnectorResult<CanonicalOrder>.NotSupported("ReceiveOrder must be implemented by a concrete mock connector."));
    }

    public virtual Task<ConnectorResult<CanonicalAcceptResult>> AcceptOrderAsync(
        ConnectorContext context,
        OrderActionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Supports(ConnectorOperations.AcceptOrder))
        {
            return Task.FromResult(ConnectorResult<CanonicalAcceptResult>.NotSupported(nameof(AcceptOrderAsync)));
        }

        return Task.FromResult(ConnectorResult<CanonicalAcceptResult>.Ok(new CanonicalAcceptResult
        {
            Outcome = CanonicalAcceptOutcome.Accepted,
            ExternalOrderId = request.ExternalOrderId,
            OccurredAt = DateTime.UtcNow
        }));
    }

    public virtual Task<ConnectorResult<CanonicalAcceptResult>> RejectOrderAsync(
        ConnectorContext context,
        OrderActionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Supports(ConnectorOperations.RejectOrder))
        {
            return Task.FromResult(ConnectorResult<CanonicalAcceptResult>.NotSupported(nameof(RejectOrderAsync)));
        }

        return Task.FromResult(ConnectorResult<CanonicalAcceptResult>.Ok(new CanonicalAcceptResult
        {
            Outcome = CanonicalAcceptOutcome.Rejected,
            ExternalOrderId = request.ExternalOrderId,
            OccurredAt = DateTime.UtcNow
        }));
    }

    public virtual Task<ConnectorResult<CanonicalOrderStatusUpdate>> UpdateStatusAsync(
        ConnectorContext context,
        CanonicalOrderStatusUpdate update,
        CancellationToken cancellationToken = default)
    {
        if (!Supports(ConnectorOperations.UpdateStatus))
        {
            return Task.FromResult(ConnectorResult<CanonicalOrderStatusUpdate>.NotSupported(nameof(UpdateStatusAsync)));
        }

        return Task.FromResult(ConnectorResult<CanonicalOrderStatusUpdate>.Ok(update));
    }

    public virtual Task<ConnectorResult<CanonicalTrackingInfo>> GetTrackingAsync(
        ConnectorContext context,
        OrderActionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Supports(ConnectorOperations.GetTracking))
        {
            return Task.FromResult(ConnectorResult<CanonicalTrackingInfo>.NotSupported(nameof(GetTrackingAsync)));
        }

        return Task.FromResult(ConnectorResult<CanonicalTrackingInfo>.Ok(new CanonicalTrackingInfo
        {
            ExternalOrderId = request.ExternalOrderId,
            Status = CanonicalOrderStatus.InTransit,
            TrackingNumber = "MOCK-TRACK-001",
            CarrierName = Descriptor.DisplayName,
            LastUpdatedAt = DateTime.UtcNow
        }));
    }

    public virtual Task<ConnectorResult<CanonicalReturnRequest>> HandleReturnAsync(
        ConnectorContext context,
        CanonicalReturnRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Supports(ConnectorOperations.HandleReturn))
        {
            return Task.FromResult(ConnectorResult<CanonicalReturnRequest>.NotSupported(nameof(HandleReturnAsync)));
        }

        return Task.FromResult(ConnectorResult<CanonicalReturnRequest>.Ok(request));
    }

    protected bool Supports(ConnectorOperations operation) =>
        Descriptor.SupportedOperations.HasFlag(operation);
}
