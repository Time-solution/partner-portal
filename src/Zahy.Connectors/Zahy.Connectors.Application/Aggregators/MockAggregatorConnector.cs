using Volo.Abp.Timing;

namespace Zahy.Connectors;

public sealed class MockAggregatorConnector : MockConnectorBase
{
    private readonly InMemoryAggregatorConnectorTransport _transport;
    private readonly IClock _clock;
    private readonly IAcceptancePolicyEvaluator _acceptancePolicy;

    public MockAggregatorConnector(
        InMemoryAggregatorConnectorTransport transport,
        IClock clock,
        IAcceptancePolicyEvaluator acceptancePolicy)
        : base(new ConnectorDescriptor
        {
            ConnectorCode = ConnectorConsts.MockAggregatorCode,
            Kind = ConnectorKind.Aggregator,
            DisplayName = "Mock Aggregator",
            SupportedOperations = ConnectorOperations.AggregatorDefault
        })
    {
        _transport = transport;
        _clock = clock;
        _acceptancePolicy = acceptancePolicy;
    }

    public override Task<ConnectorResult<CanonicalMenuSyncResult>> SyncMenuAsync(
        ConnectorContext context,
        CanonicalMenuSyncRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Supports(ConnectorOperations.SyncMenu))
        {
            return Task.FromResult(ConnectorResult<CanonicalMenuSyncResult>.NotSupported(nameof(SyncMenuAsync)));
        }

        var items = _transport.GetMenuItems(request.OutletExternalId)
            .Select(item => new CanonicalMenuItem
            {
                ExternalItemId = item.ExternalItemId,
                Sku = item.Sku,
                NameEn = item.NameEn,
                NameAr = item.NameAr,
                Price = item.Price,
                Currency = item.Currency,
                IsAvailable = item.IsAvailable,
                OutletExternalId = item.OutletExternalId
            })
            .ToList();

        return Task.FromResult(ConnectorResult<CanonicalMenuSyncResult>.Ok(new CanonicalMenuSyncResult
        {
            Items = items,
            SyncedAt = _clock.Now
        }));
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

        if (request.Intent != ReceiveOrderIntent.InboundFromPartner)
        {
            return Task.FromResult(ConnectorResult<CanonicalOrder>.Fail(
                ConnectorErrorCode.InvalidRequest,
                "Mock aggregator only supports inbound orders."));
        }

        if (!_transport.TryGetOrder(request.ExternalOrderId, out var inbound))
        {
            return Task.FromResult(ConnectorResult<CanonicalOrder>.Fail(
                ConnectorErrorCode.NotFound,
                $"Order '{request.ExternalOrderId}' was not found in the mock transport."));
        }

        if (inbound.PartnerId != context.PartnerId || inbound.TenantId != context.TenantId)
        {
            return Task.FromResult(ConnectorResult<CanonicalOrder>.Fail(
                ConnectorErrorCode.InvalidRequest,
                "Order partner/tenant scope does not match connector context."));
        }

        return Task.FromResult(ConnectorResult<CanonicalOrder>.Ok(MapToCanonical(inbound)));
    }

    public override Task<ConnectorResult<CanonicalAcceptResult>> AcceptOrderAsync(
        ConnectorContext context,
        OrderActionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Supports(ConnectorOperations.AcceptOrder))
        {
            return Task.FromResult(ConnectorResult<CanonicalAcceptResult>.NotSupported(nameof(AcceptOrderAsync)));
        }

        return Task.FromResult(EvaluateAcceptance(context, request, accept: true));
    }

    public override Task<ConnectorResult<CanonicalAcceptResult>> RejectOrderAsync(
        ConnectorContext context,
        OrderActionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Supports(ConnectorOperations.RejectOrder))
        {
            return Task.FromResult(ConnectorResult<CanonicalAcceptResult>.NotSupported(nameof(RejectOrderAsync)));
        }

        if (!_transport.TryGetOrder(request.ExternalOrderId, out var inbound))
        {
            return Task.FromResult(ConnectorResult<CanonicalAcceptResult>.Fail(
                ConnectorErrorCode.NotFound,
                $"Order '{request.ExternalOrderId}' was not found."));
        }

        if (inbound.Status != AggregatorInboundOrderStatus.New)
        {
            return Task.FromResult(ConnectorResult<CanonicalAcceptResult>.Fail(
                ConnectorErrorCode.InvalidRequest,
                "Only pending orders can be rejected."));
        }

        _transport.UpdateOrderStatus(request.ExternalOrderId, AggregatorInboundOrderStatus.Rejected);

        return Task.FromResult(ConnectorResult<CanonicalAcceptResult>.Ok(new CanonicalAcceptResult
        {
            Outcome = CanonicalAcceptOutcome.Rejected,
            ExternalOrderId = request.ExternalOrderId,
            OccurredAt = _clock.Now
        }));
    }

    private ConnectorResult<CanonicalAcceptResult> EvaluateAcceptance(
        ConnectorContext context,
        OrderActionRequest request,
        bool accept)
    {
        if (!_transport.TryGetOrder(request.ExternalOrderId, out var inbound))
        {
            return ConnectorResult<CanonicalAcceptResult>.Fail(
                ConnectorErrorCode.NotFound,
                $"Order '{request.ExternalOrderId}' was not found.");
        }

        if (inbound.PartnerId != context.PartnerId || inbound.TenantId != context.TenantId)
        {
            return ConnectorResult<CanonicalAcceptResult>.Fail(
                ConnectorErrorCode.InvalidRequest,
                "Order partner/tenant scope does not match connector context.");
        }

        var canonical = MapToCanonical(inbound);

        if (_acceptancePolicy.IsExpired(canonical, _clock.Now))
        {
            return ConnectorResult<CanonicalAcceptResult>.Ok(new CanonicalAcceptResult
            {
                Outcome = CanonicalAcceptOutcome.Expired,
                ExternalOrderId = request.ExternalOrderId,
                OccurredAt = _clock.Now
            });
        }

        if (inbound.Status != AggregatorInboundOrderStatus.New)
        {
            return ConnectorResult<CanonicalAcceptResult>.Fail(
                ConnectorErrorCode.InvalidRequest,
                "Order is no longer pending acceptance.");
        }

        if (accept)
        {
            _transport.UpdateOrderStatus(request.ExternalOrderId, AggregatorInboundOrderStatus.Accepted);
        }

        return ConnectorResult<CanonicalAcceptResult>.Ok(new CanonicalAcceptResult
        {
            Outcome = accept ? CanonicalAcceptOutcome.Accepted : CanonicalAcceptOutcome.Rejected,
            ExternalOrderId = request.ExternalOrderId,
            OccurredAt = _clock.Now
        });
    }

    internal static CanonicalOrder MapToCanonical(AggregatorInboundOrder inbound)
    {
        var status = inbound.Status switch
        {
            AggregatorInboundOrderStatus.Accepted => CanonicalOrderStatus.Accepted,
            AggregatorInboundOrderStatus.Rejected => CanonicalOrderStatus.Rejected,
            _ => CanonicalOrderStatus.PendingAcceptance
        };

        return new CanonicalOrder
        {
            ExternalOrderId = inbound.ExternalOrderId,
            Version = 1,
            ConnectorCode = ConnectorConsts.MockAggregatorCode,
            ConnectorKind = ConnectorKind.Aggregator,
            PartnerId = inbound.PartnerId,
            TenantId = inbound.TenantId,
            OutletExternalId = inbound.OutletExternalId,
            Direction = CanonicalOrderDirection.Inbound,
            Status = status,
            PaymentState = inbound.PaymentState,
            Currency = inbound.Currency,
            Subtotal = inbound.Subtotal,
            TaxAmount = inbound.TaxAmount,
            DeliveryFee = inbound.DeliveryFee,
            TotalAmount = inbound.TotalAmount,
            PlacedAt = inbound.PlacedAt,
            AcceptDeadlineUtc = inbound.AcceptDeadlineUtc,
            CustomerNotes = inbound.CustomerNotes,
            Lines = inbound.Lines.Select(line => new CanonicalOrderLine
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
}
