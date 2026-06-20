using Shouldly;
using Volo.Abp;
using Xunit;

namespace Zahy.PartnerCatalog;

public class PartnerCatalogSnapshotIdempotencyTests
{
    [Fact]
    public void OrderLine_Trigger_Requires_OrderLineId()
    {
        Should.Throw<BusinessException>(() =>
                PartnerCatalogSnapshotIdempotency.BuildUniqueKeyComponents(
                    "order:ORD-1:v1",
                    orderLineId: null,
                    SettlementCostMarkupTrigger.OrderLine))
            .Code.ShouldBe(PartnerCatalogErrorCodes.MissingOrderLineId);
    }

    [Fact]
    public void Same_ExternalTxn_Different_Lines_Do_Not_Collide()
    {
        var ext = "order:ord-9001:v2";

        var a = PartnerCatalogSnapshotIdempotency.BuildUniqueKeyComponents(
            ext,
            "ORD-9001-L1",
            SettlementCostMarkupTrigger.OrderLine);

        var b = PartnerCatalogSnapshotIdempotency.BuildUniqueKeyComponents(
            ext,
            "ORD-9001-L2",
            SettlementCostMarkupTrigger.OrderLine);

        a.ExternalTransactionId.ShouldBe(b.ExternalTransactionId);
        a.OrderLineId.ShouldNotBe(b.OrderLineId);

        PartnerCatalogSnapshotIdempotency.BuildStorageKey(ext, "ORD-9001-L1")
            .ShouldNotBe(PartnerCatalogSnapshotIdempotency.BuildStorageKey(ext, "ORD-9001-L2"));
    }

    [Fact]
    public void PerOrder_Delivery_Allows_Null_OrderLineId()
    {
        var components = PartnerCatalogSnapshotIdempotency.BuildUniqueKeyComponents(
            "pcat:order:ORD-9001:v2:delivery",
            orderLineId: null,
            SettlementCostMarkupTrigger.Order);

        components.OrderLineId.ShouldBe(string.Empty);
    }

    [Fact]
    public void ExternalTransactionId_Is_Normalized()
    {
        PartnerCatalogSnapshotIdempotency.NormalizeExternalTransactionId("  Txn-1 ")
            .ShouldBe("txn-1");
    }
}
