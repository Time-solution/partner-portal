using Volo.Abp.Timing;

namespace Zahy.Connectors;

public sealed class MockThreePLConnector : MockConnectorBase, IThreePLConnector
{
    private readonly InMemoryThreePLConnectorTransport _transport;
    private readonly IClock _clock;

    public MockThreePLConnector(
        InMemoryThreePLConnectorTransport transport,
        IClock clock)
        : base(new ConnectorDescriptor
        {
            ConnectorCode = ConnectorConsts.MockThreePLCode,
            Kind = ConnectorKind.ThreePL,
            DisplayName = "Mock 3PL",
            SupportedOperations = ConnectorOperations.ThreePLDefault
        })
    {
        _transport = transport;
        _clock = clock;
    }

    public override Task<ConnectorResult<CanonicalOrder>> ReceiveOrderAsync(
        ConnectorContext context,
        ReceiveOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Supports(ConnectorOperations.ReceiveOrder))
        {
            return Task.FromResult(ConnectorResult<CanonicalOrder>.NotSupported(nameof(ReceiveOrderAsync)));
        }

        if (request.Intent != ReceiveOrderIntent.OutboundToPartner)
        {
            return Task.FromResult(ConnectorResult<CanonicalOrder>.Fail(
                ConnectorErrorCode.InvalidRequest,
                "Mock 3PL only supports outbound fulfillment pushes."));
        }

        if (!_transport.TryGetOrder(request.ExternalOrderId, out var fulfillment))
        {
            return Task.FromResult(ConnectorResult<CanonicalOrder>.Fail(
                ConnectorErrorCode.NotFound,
                $"Fulfillment order '{request.ExternalOrderId}' was not found."));
        }

        if (fulfillment.PartnerId != context.PartnerId || fulfillment.TenantId != context.TenantId)
        {
            return Task.FromResult(ConnectorResult<CanonicalOrder>.Fail(
                ConnectorErrorCode.InvalidRequest,
                "Fulfillment order scope does not match connector context."));
        }

        _transport.MarkPushed(request.ExternalOrderId);
        return Task.FromResult(ConnectorResult<CanonicalOrder>.Ok(MapToCanonical(fulfillment, version: 1)));
    }

    public override Task<ConnectorResult<CanonicalOrderStatusUpdate>> UpdateStatusAsync(
        ConnectorContext context,
        CanonicalOrderStatusUpdate update,
        CancellationToken cancellationToken = default)
    {
        if (!Supports(ConnectorOperations.UpdateStatus))
        {
            return Task.FromResult(ConnectorResult<CanonicalOrderStatusUpdate>.NotSupported(nameof(UpdateStatusAsync)));
        }

        if (!_transport.TryGetOrder(update.ExternalOrderId, out var fulfillment))
        {
            return Task.FromResult(ConnectorResult<CanonicalOrderStatusUpdate>.Fail(
                ConnectorErrorCode.NotFound,
                $"Fulfillment order '{update.ExternalOrderId}' was not found."));
        }

        var threePlStatus = MapToThreePLStatus(update.NewStatus);
        var tracking = update.NewStatus == CanonicalOrderStatus.InTransit ? $"3PL-TRK-{update.ExternalOrderId}" : null;
        _transport.UpdateStatus(update.ExternalOrderId, threePlStatus, tracking);

        return Task.FromResult(ConnectorResult<CanonicalOrderStatusUpdate>.Ok(new CanonicalOrderStatusUpdate
        {
            ExternalOrderId = update.ExternalOrderId,
            Version = update.Version,
            NewStatus = update.NewStatus,
            PaymentState = update.PaymentState,
            OccurredAt = _clock.Now
        }));
    }

    public override Task<ConnectorResult<CanonicalTrackingInfo>> GetTrackingAsync(
        ConnectorContext context,
        OrderActionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Supports(ConnectorOperations.GetTracking))
        {
            return Task.FromResult(ConnectorResult<CanonicalTrackingInfo>.NotSupported(nameof(GetTrackingAsync)));
        }

        if (!_transport.TryGetOrder(request.ExternalOrderId, out var fulfillment))
        {
            return Task.FromResult(ConnectorResult<CanonicalTrackingInfo>.Fail(
                ConnectorErrorCode.NotFound,
                $"Fulfillment order '{request.ExternalOrderId}' was not found."));
        }

        return Task.FromResult(ConnectorResult<CanonicalTrackingInfo>.Ok(new CanonicalTrackingInfo
        {
            ExternalOrderId = request.ExternalOrderId,
            Status = MapFromThreePLStatus(fulfillment.Status),
            TrackingNumber = fulfillment.TrackingNumber,
            CarrierName = Descriptor.DisplayName,
            LastUpdatedAt = _clock.Now
        }));
    }

    public override Task<ConnectorResult<CanonicalReturnRequest>> HandleReturnAsync(
        ConnectorContext context,
        CanonicalReturnRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Supports(ConnectorOperations.HandleReturn))
        {
            return Task.FromResult(ConnectorResult<CanonicalReturnRequest>.NotSupported(nameof(HandleReturnAsync)));
        }

        if (!_transport.TryGetOrder(request.ExternalOrderId, out _))
        {
            return Task.FromResult(ConnectorResult<CanonicalReturnRequest>.Fail(
                ConnectorErrorCode.NotFound,
                $"Fulfillment order '{request.ExternalOrderId}' was not found."));
        }

        _transport.UpdateStatus(request.ExternalOrderId, ThreePLFulfillmentStatus.Returned);
        _transport.RecordReturn(new ThreePLReturnRecord
        {
            ExternalOrderId = request.ExternalOrderId,
            Reason = request.Reason,
            RecordedAt = _clock.Now
        });

        return Task.FromResult(ConnectorResult<CanonicalReturnRequest>.Ok(request));
    }

    public Task<ConnectorResult<CanonicalInventoryReconcileResult>> ReconcileInventoryAsync(
        ConnectorContext context,
        CanonicalInventoryReconcileRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Supports(ConnectorOperations.ReconcileInventory))
        {
            return Task.FromResult(ConnectorResult<CanonicalInventoryReconcileResult>.NotSupported(nameof(ReconcileInventoryAsync)));
        }

        var expected = _transport.GetExpectedInventory()
            .Where(x => !string.IsNullOrWhiteSpace(x.Sku))
            .ToDictionary(x => x.Sku, x => x.ExpectedQuantity, StringComparer.OrdinalIgnoreCase);

        var reported = request.ReportedOnHand
            .Where(x => !string.IsNullOrWhiteSpace(x.Sku))
            .ToDictionary(x => x.Sku, x => x.ReportedQuantity, StringComparer.OrdinalIgnoreCase);

        var skus = expected.Keys.Union(reported.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(x => x);
        var lines = new List<CanonicalInventoryReconcileLine>();

        foreach (var sku in skus)
        {
            expected.TryGetValue(sku, out var expectedQty);
            reported.TryGetValue(sku, out var reportedQty);
            lines.Add(new CanonicalInventoryReconcileLine
            {
                Sku = sku,
                ExpectedQuantity = expectedQty,
                ReportedQuantity = reportedQty,
                Delta = reportedQty - expectedQty
            });
        }

        return Task.FromResult(ConnectorResult<CanonicalInventoryReconcileResult>.Ok(new CanonicalInventoryReconcileResult
        {
            WarehouseExternalId = request.WarehouseExternalId,
            Lines = lines,
            ReconciledAt = _clock.Now
        }));
    }

    internal static CanonicalOrder MapToCanonical(ThreePLFulfillmentOrder fulfillment, long version)
    {
        return new CanonicalOrder
        {
            ExternalOrderId = fulfillment.ExternalOrderId,
            Version = version,
            ConnectorCode = ConnectorConsts.MockThreePLCode,
            ConnectorKind = ConnectorKind.ThreePL,
            PartnerId = fulfillment.PartnerId,
            TenantId = fulfillment.TenantId,
            OutletExternalId = fulfillment.WarehouseExternalId,
            Direction = CanonicalOrderDirection.Outbound,
            Status = MapFromThreePLStatus(fulfillment.Status),
            PaymentState = CanonicalPaymentState.Paid,
            Currency = fulfillment.Currency,
            TotalAmount = fulfillment.TotalAmount,
            Subtotal = fulfillment.TotalAmount,
            PlacedAt = DateTime.UtcNow,
            Lines = fulfillment.Lines.Select(line => new CanonicalOrderLine
            {
                LineNumber = line.LineNumber,
                Sku = line.Sku,
                ProductName = line.ProductName,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                LineTotal = line.LineTotal
            }).ToList()
        };
    }

    private static ThreePLFulfillmentStatus MapToThreePLStatus(CanonicalOrderStatus status) =>
        status switch
        {
            CanonicalOrderStatus.InPreparation => ThreePLFulfillmentStatus.Picking,
            CanonicalOrderStatus.InTransit or CanonicalOrderStatus.ReadyForHandoff => ThreePLFulfillmentStatus.Shipped,
            CanonicalOrderStatus.Returned => ThreePLFulfillmentStatus.Returned,
            _ => ThreePLFulfillmentStatus.Pushed
        };

    private static CanonicalOrderStatus MapFromThreePLStatus(ThreePLFulfillmentStatus status) =>
        status switch
        {
            ThreePLFulfillmentStatus.Picking => CanonicalOrderStatus.InPreparation,
            ThreePLFulfillmentStatus.Shipped => CanonicalOrderStatus.InTransit,
            ThreePLFulfillmentStatus.Returned => CanonicalOrderStatus.Returned,
            _ => CanonicalOrderStatus.Accepted
        };
}
