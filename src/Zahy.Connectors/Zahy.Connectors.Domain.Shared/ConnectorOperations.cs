namespace Zahy.Connectors;

[Flags]
public enum ConnectorOperations
{
    None = 0,
    SyncMenu = 1 << 0,
    ReceiveOrder = 1 << 1,
    AcceptOrder = 1 << 2,
    RejectOrder = 1 << 3,
    UpdateStatus = 1 << 4,
    GetTracking = 1 << 5,
    HandleReturn = 1 << 6,
    MapBranch = 1 << 7,
    ReconcileInventory = 1 << 8,
    GetRate = 1 << 9,
    CreateLabel = 1 << 10,
    ReceiveStockTransfer = 1 << 11,

    AggregatorDefault = SyncMenu | ReceiveOrder | AcceptOrder | RejectOrder | UpdateStatus | MapBranch,
    ThreePLDefault = ReceiveOrder | UpdateStatus | GetTracking | HandleReturn | MapBranch | ReconcileInventory,
    CarrierDefault = ReceiveOrder | UpdateStatus | GetTracking | MapBranch | GetRate | CreateLabel,

    // JUMP "Fulfilled by" — 3PL fulfilment plus inbound stock custody (ASN) into the partner warehouse.
    JumpConsignmentDefault = ThreePLDefault | ReceiveStockTransfer
}
