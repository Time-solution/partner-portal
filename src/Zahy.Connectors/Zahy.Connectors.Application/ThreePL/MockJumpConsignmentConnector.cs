using Volo.Abp.Timing;

namespace Zahy.Connectors;

/// <summary>
/// STUB noon JUMP "Fulfilled by" connector. No real noon API — maps JUMP's canonical custody events
/// onto the existing 3PL connector pattern: inbound stock transfer (ASN) into the partner warehouse
/// plus two-way inventory reconcile (reusing <see cref="CanonicalInventoryReconcileRequest"/>).
/// Every operation is partner/tenant scoped via <see cref="ConnectorContext"/> for isolation.
/// </summary>
public sealed class MockJumpConsignmentConnector : MockConnectorBase, IConsignmentConnector
{
    private readonly InMemoryJumpConsignmentTransport _transport;
    private readonly IClock _clock;

    public MockJumpConsignmentConnector(
        InMemoryJumpConsignmentTransport transport,
        IClock clock)
        : base(new ConnectorDescriptor
        {
            ConnectorCode = ConnectorConsts.MockJumpConsignmentCode,
            Kind = ConnectorKind.ThreePL,
            DisplayName = "Mock noon JUMP (Consignment)",
            SupportedOperations = ConnectorOperations.MapBranch
                | ConnectorOperations.ReconcileInventory
                | ConnectorOperations.ReceiveStockTransfer
        })
    {
        _transport = transport;
        _clock = clock;
    }

    public Task<ConnectorResult<CanonicalStockTransferResult>> ReceiveStockTransferAsync(
        ConnectorContext context,
        CanonicalStockTransferRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Supports(ConnectorOperations.ReceiveStockTransfer))
        {
            return Task.FromResult(ConnectorResult<CanonicalStockTransferResult>.NotSupported(nameof(ReceiveStockTransferAsync)));
        }

        if (context.PartnerId == Guid.Empty || context.TenantId == Guid.Empty)
        {
            return Task.FromResult(ConnectorResult<CanonicalStockTransferResult>.Fail(
                ConnectorErrorCode.InvalidRequest,
                "Stock transfer requires a partner and tenant scope."));
        }

        if (string.IsNullOrWhiteSpace(request.WarehouseExternalId) || request.Lines.Count == 0)
        {
            return Task.FromResult(ConnectorResult<CanonicalStockTransferResult>.Fail(
                ConnectorErrorCode.InvalidRequest,
                "Stock transfer requires a warehouse and at least one line."));
        }

        var receivedAt = _clock.Now;
        var stockLevels = _transport.ApplyStockTransfer(context.PartnerId, context.TenantId, request, receivedAt);

        return Task.FromResult(ConnectorResult<CanonicalStockTransferResult>.Ok(new CanonicalStockTransferResult
        {
            ExternalTransferId = request.ExternalTransferId,
            WarehouseExternalId = request.WarehouseExternalId,
            Ownership = request.Ownership,
            StockLevels = stockLevels,
            ReceivedAt = receivedAt
        }));
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

        if (context.PartnerId == Guid.Empty || context.TenantId == Guid.Empty)
        {
            return Task.FromResult(ConnectorResult<CanonicalInventoryReconcileResult>.Fail(
                ConnectorErrorCode.InvalidRequest,
                "Inventory reconcile requires a partner and tenant scope."));
        }

        // Two-way reconcile: custody on-hand (what Zahy expects) vs partner-reported on-hand.
        var expected = _transport
            .GetStockLevels(context.PartnerId, context.TenantId, request.WarehouseExternalId)
            .ToDictionary(x => x.Sku, x => x.OnHandQuantity, StringComparer.OrdinalIgnoreCase);

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
}
