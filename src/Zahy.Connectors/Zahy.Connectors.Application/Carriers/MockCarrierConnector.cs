using Volo.Abp.Timing;

namespace Zahy.Connectors;

public sealed class MockCarrierConnector : MockConnectorBase, ICarrierConnector
{
    private readonly InMemoryCarrierConnectorTransport _transport;
    private readonly IClock _clock;

    public MockCarrierConnector(
        InMemoryCarrierConnectorTransport transport,
        IClock clock)
        : base(new ConnectorDescriptor
        {
            ConnectorCode = ConnectorConsts.MockCarrierCode,
            Kind = ConnectorKind.Carrier,
            DisplayName = "Mock Carrier",
            SupportedOperations = ConnectorOperations.CarrierDefault
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

        if (request.Intent != ReceiveOrderIntent.CreateShipment)
        {
            return Task.FromResult(ConnectorResult<CanonicalOrder>.Fail(
                ConnectorErrorCode.InvalidRequest,
                "Mock carrier only supports shipment creation."));
        }

        if (!_transport.TryGetStaged(request.ExternalOrderId, out var staged))
        {
            return Task.FromResult(ConnectorResult<CanonicalOrder>.Fail(
                ConnectorErrorCode.NotFound,
                $"Shipment request '{request.ExternalOrderId}' was not found."));
        }

        if (staged.PartnerId != context.PartnerId || staged.TenantId != context.TenantId)
        {
            return Task.FromResult(ConnectorResult<CanonicalOrder>.Fail(
                ConnectorErrorCode.InvalidRequest,
                "Shipment request scope does not match connector context."));
        }

        var rate = CalculateRate(staged.WeightKg, staged.DestinationCity);
        var record = new CarrierShipmentRecord
        {
            ExternalOrderId = staged.ExternalOrderId,
            PartnerId = staged.PartnerId,
            TenantId = staged.TenantId,
            TrackingNumber = $"CR-{staged.ExternalOrderId}",
            LabelReference = $"LBL-{staged.ExternalOrderId}",
            RateAmount = rate,
            Currency = staged.Currency,
            Status = CanonicalOrderStatus.ReadyForHandoff
        };

        _transport.SaveShipment(record);

        return Task.FromResult(ConnectorResult<CanonicalOrder>.Ok(new CanonicalOrder
        {
            ExternalOrderId = staged.ExternalOrderId,
            Version = 1,
            ConnectorCode = ConnectorConsts.MockCarrierCode,
            ConnectorKind = ConnectorKind.Carrier,
            PartnerId = staged.PartnerId,
            TenantId = staged.TenantId,
            OutletExternalId = staged.PickupOutletExternalId,
            Direction = CanonicalOrderDirection.Outbound,
            Status = CanonicalOrderStatus.ReadyForHandoff,
            PaymentState = CanonicalPaymentState.Paid,
            Currency = staged.Currency,
            Subtotal = staged.DeclaredValue,
            TotalAmount = staged.DeclaredValue,
            PlacedAt = _clock.Now
        }));
    }

    public Task<ConnectorResult<CanonicalShipmentRate>> GetRateAsync(
        ConnectorContext context,
        CarrierRateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Supports(ConnectorOperations.GetRate))
        {
            return Task.FromResult(ConnectorResult<CanonicalShipmentRate>.NotSupported(nameof(GetRateAsync)));
        }

        var amount = CalculateRate(request.WeightKg, request.DestinationCity);

        return Task.FromResult(ConnectorResult<CanonicalShipmentRate>.Ok(new CanonicalShipmentRate
        {
            ExternalOrderId = request.ExternalOrderId,
            Amount = amount,
            Currency = ConnectorConsts.DefaultCurrency,
            ServiceLevel = "Standard",
            QuotedAt = _clock.Now
        }));
    }

    public Task<ConnectorResult<CanonicalShipmentLabel>> CreateLabelAsync(
        ConnectorContext context,
        CarrierLabelRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Supports(ConnectorOperations.CreateLabel))
        {
            return Task.FromResult(ConnectorResult<CanonicalShipmentLabel>.NotSupported(nameof(CreateLabelAsync)));
        }

        if (!_transport.TryGetShipment(request.ExternalOrderId, out var shipment))
        {
            return Task.FromResult(ConnectorResult<CanonicalShipmentLabel>.Fail(
                ConnectorErrorCode.NotFound,
                $"Shipment '{request.ExternalOrderId}' was not found."));
        }

        return Task.FromResult(ConnectorResult<CanonicalShipmentLabel>.Ok(new CanonicalShipmentLabel
        {
            ExternalOrderId = request.ExternalOrderId,
            TrackingNumber = shipment.TrackingNumber,
            LabelReference = shipment.LabelReference,
            RateAmount = shipment.RateAmount,
            Currency = shipment.Currency,
            CreatedAt = _clock.Now
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

        if (!_transport.TryGetShipment(request.ExternalOrderId, out var shipment))
        {
            return Task.FromResult(ConnectorResult<CanonicalTrackingInfo>.Fail(
                ConnectorErrorCode.NotFound,
                $"Shipment '{request.ExternalOrderId}' was not found."));
        }

        return Task.FromResult(ConnectorResult<CanonicalTrackingInfo>.Ok(new CanonicalTrackingInfo
        {
            ExternalOrderId = request.ExternalOrderId,
            Status = shipment.Status,
            TrackingNumber = shipment.TrackingNumber,
            CarrierName = Descriptor.DisplayName,
            LastUpdatedAt = _clock.Now
        }));
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

        if (!_transport.TryGetShipment(update.ExternalOrderId, out var shipment))
        {
            return Task.FromResult(ConnectorResult<CanonicalOrderStatusUpdate>.Fail(
                ConnectorErrorCode.NotFound,
                $"Shipment '{update.ExternalOrderId}' was not found."));
        }

        shipment.Status = update.NewStatus;

        return Task.FromResult(ConnectorResult<CanonicalOrderStatusUpdate>.Ok(new CanonicalOrderStatusUpdate
        {
            ExternalOrderId = update.ExternalOrderId,
            Version = update.Version,
            NewStatus = update.NewStatus,
            PaymentState = update.PaymentState,
            OccurredAt = _clock.Now
        }));
    }

    private static decimal CalculateRate(decimal weightKg, string destinationCity)
    {
        var baseRate = 18m;
        var weightComponent = Math.Max(1m, weightKg) * 4.5m;
        var cityComponent = destinationCity.Contains("Riyadh", StringComparison.OrdinalIgnoreCase) ? 0m : 7m;
        return baseRate + weightComponent + cityComponent;
    }
}
