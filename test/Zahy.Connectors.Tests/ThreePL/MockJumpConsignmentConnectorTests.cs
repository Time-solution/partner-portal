using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace Zahy.Connectors;

public class MockJumpConsignmentConnectorTests : ZahyConnectorsTestBase
{
    private const string Warehouse = "wh-jump-jeddah-1";

    private readonly IConsignmentConnector _connector;
    private readonly InMemoryJumpConsignmentTransport _transport;

    public MockJumpConsignmentConnectorTests()
    {
        _connector = GetRequiredService<MockJumpConsignmentConnector>();
        _transport = GetRequiredService<InMemoryJumpConsignmentTransport>();
    }

    [Fact]
    public async Task Should_Record_Stock_Transfer_ASN_And_Build_Warehouse_Stock()
    {
        var partnerId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _transport.Reset();

        var result = await _connector.ReceiveStockTransferAsync(
            CreateContext(partnerId, tenantId),
            new CanonicalStockTransferRequest
            {
                ExternalTransferId = "ASN-001",
                WarehouseExternalId = Warehouse,
                Ownership = ConsignmentStockOwnership.MerchantOwned,
                Lines =
                [
                    new CanonicalStockTransferLine { Sku = "SKU-A", ProductName = "Item A", Quantity = 10 },
                    new CanonicalStockTransferLine { Sku = "SKU-B", ProductName = "Item B", Quantity = 4 }
                ]
            });

        result.Success.ShouldBeTrue();
        result.Value!.Ownership.ShouldBe(ConsignmentStockOwnership.MerchantOwned);
        result.Value.StockLevels.First(x => x.Sku == "SKU-A").OnHandQuantity.ShouldBe(10);
        result.Value.StockLevels.First(x => x.Sku == "SKU-B").OnHandQuantity.ShouldBe(4);

        _transport.GetTransfers(partnerId, tenantId).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Repeated_Transfers_Accumulate_Custody_Stock()
    {
        var partnerId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _transport.Reset();

        await _connector.ReceiveStockTransferAsync(
            CreateContext(partnerId, tenantId),
            BuildTransfer("ASN-1", ("SKU-A", 10)));
        await _connector.ReceiveStockTransferAsync(
            CreateContext(partnerId, tenantId),
            BuildTransfer("ASN-2", ("SKU-A", 5)));

        var levels = _transport.GetStockLevels(partnerId, tenantId, Warehouse);
        levels.Single(x => x.Sku == "SKU-A").OnHandQuantity.ShouldBe(15);
        _transport.GetTransfers(partnerId, tenantId).Count.ShouldBe(2);
    }

    [Fact]
    public async Task Should_Reconcile_Custody_Stock_Against_Partner_Reported_OnHand()
    {
        var partnerId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _transport.Reset();
        await _connector.ReceiveStockTransferAsync(
            CreateContext(partnerId, tenantId),
            BuildTransfer("ASN-1", ("SKU-A", 10), ("SKU-B", 5)));

        var result = await _connector.ReconcileInventoryAsync(
            CreateContext(partnerId, tenantId),
            new CanonicalInventoryReconcileRequest
            {
                WarehouseExternalId = Warehouse,
                ReportedOnHand =
                [
                    new CanonicalInventoryReconcileLine { Sku = "SKU-A", ReportedQuantity = 8 },
                    new CanonicalInventoryReconcileLine { Sku = "SKU-B", ReportedQuantity = 5 }
                ]
            });

        result.Success.ShouldBeTrue();
        result.Value!.Lines.First(x => x.Sku == "SKU-A").ExpectedQuantity.ShouldBe(10);
        result.Value.Lines.First(x => x.Sku == "SKU-A").Delta.ShouldBe(-2);
        result.Value.Lines.First(x => x.Sku == "SKU-B").Delta.ShouldBe(0);
    }

    [Fact]
    public async Task Custody_Is_Isolated_Per_Partner_And_Tenant()
    {
        var partnerA = Guid.NewGuid();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        _transport.Reset();

        await _connector.ReceiveStockTransferAsync(
            CreateContext(partnerA, tenantA),
            BuildTransfer("ASN-A", ("SKU-A", 10)));

        // A different tenant sees no custody and reconciles against empty expected.
        _transport.GetStockLevels(partnerA, tenantB, Warehouse).ShouldBeEmpty();

        var result = await _connector.ReconcileInventoryAsync(
            CreateContext(partnerA, tenantB),
            new CanonicalInventoryReconcileRequest
            {
                WarehouseExternalId = Warehouse,
                ReportedOnHand = [new CanonicalInventoryReconcileLine { Sku = "SKU-A", ReportedQuantity = 10 }]
            });

        result.Success.ShouldBeTrue();
        // Other tenant's 10 units are invisible → expected 0, surfaced as a +10 discrepancy.
        result.Value!.Lines.Single(x => x.Sku == "SKU-A").ExpectedQuantity.ShouldBe(0);
        result.Value.Lines.Single(x => x.Sku == "SKU-A").Delta.ShouldBe(10);
        _transport.GetTransfers(partnerA, tenantB).ShouldBeEmpty();
    }

    private static CanonicalStockTransferRequest BuildTransfer(string id, params (string Sku, decimal Qty)[] lines) =>
        new()
        {
            ExternalTransferId = id,
            WarehouseExternalId = Warehouse,
            Ownership = ConsignmentStockOwnership.MerchantOwned,
            Lines = lines
                .Select(l => new CanonicalStockTransferLine { Sku = l.Sku, ProductName = l.Sku, Quantity = l.Qty })
                .ToList()
        };

    private static ConnectorContext CreateContext(Guid partnerId, Guid tenantId) =>
        new()
        {
            PartnerId = partnerId,
            TenantId = tenantId,
            ConnectorCode = ConnectorConsts.MockJumpConsignmentCode
        };
}
