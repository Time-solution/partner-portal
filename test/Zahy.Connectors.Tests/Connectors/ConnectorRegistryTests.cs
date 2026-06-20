using Shouldly;
using Xunit;
using Zahy.PartnerPlatform.Partners;

namespace Zahy.Connectors;

public class ConnectorRegistryTests : ZahyConnectorsTestBase
{
    private readonly IConnectorRegistry _registry;

    public ConnectorRegistryTests()
    {
        _registry = GetRequiredService<IConnectorRegistry>();
    }

    [Theory]
    [InlineData(PartnerType.Aggregator, ConnectorConsts.MockAggregatorCode, ConnectorKind.Aggregator)]
    [InlineData(PartnerType.ThreePL, ConnectorConsts.MockThreePLCode, ConnectorKind.ThreePL)]
    [InlineData(PartnerType.Carrier, ConnectorConsts.MockCarrierCode, ConnectorKind.Carrier)]
    public void Should_Resolve_Correct_Connector_By_Partner_Type(
        PartnerType partnerType,
        string expectedCode,
        ConnectorKind expectedKind)
    {
        var connector = _registry.ResolveByPartnerType(partnerType);

        connector.Descriptor.ConnectorCode.ShouldBe(expectedCode);
        connector.Descriptor.Kind.ShouldBe(expectedKind);
    }

    [Fact]
    public void Should_Resolve_Connector_By_Explicit_Code()
    {
        var connector = _registry.Resolve(ConnectorConsts.MockThreePLCode);

        connector.Descriptor.Kind.ShouldBe(ConnectorKind.ThreePL);
        connector.Descriptor.SupportedOperations.HasFlag(ConnectorOperations.HandleReturn).ShouldBeTrue();
        connector.Descriptor.SupportedOperations.HasFlag(ConnectorOperations.ReconcileInventory).ShouldBeTrue();
        connector.Descriptor.SupportedOperations.HasFlag(ConnectorOperations.SyncMenu).ShouldBeFalse();
    }

    [Fact]
    public void Should_Resolve_Carrier_Connector_With_Rate_And_Label_Operations()
    {
        var connector = _registry.Resolve(ConnectorConsts.MockCarrierCode);

        connector.Descriptor.Kind.ShouldBe(ConnectorKind.Carrier);
        connector.Descriptor.SupportedOperations.HasFlag(ConnectorOperations.GetRate).ShouldBeTrue();
        connector.Descriptor.SupportedOperations.HasFlag(ConnectorOperations.CreateLabel).ShouldBeTrue();
        connector.Descriptor.SupportedOperations.HasFlag(ConnectorOperations.AcceptOrder).ShouldBeFalse();
    }

    [Fact]
    public void Should_List_All_Registered_Connector_Descriptors()
    {
        var descriptors = _registry.ListDescriptors();

        descriptors.Count.ShouldBe(4);
        descriptors.Select(x => x.ConnectorCode).ShouldContain(ConnectorConsts.MockAggregatorCode);
        descriptors.Select(x => x.ConnectorCode).ShouldContain(ConnectorConsts.MockThreePLCode);
        descriptors.Select(x => x.ConnectorCode).ShouldContain(ConnectorConsts.MockCarrierCode);
        descriptors.Select(x => x.ConnectorCode).ShouldContain(ConnectorConsts.MockJumpConsignmentCode);
    }

    [Fact]
    public void Should_Resolve_Jump_Consignment_Connector_With_Stock_Custody_Operations()
    {
        var connector = _registry.Resolve(ConnectorConsts.MockJumpConsignmentCode);

        connector.Descriptor.Kind.ShouldBe(ConnectorKind.ThreePL);
        connector.Descriptor.SupportedOperations.HasFlag(ConnectorOperations.ReceiveStockTransfer).ShouldBeTrue();
        connector.Descriptor.SupportedOperations.HasFlag(ConnectorOperations.ReconcileInventory).ShouldBeTrue();
        connector.ShouldBeAssignableTo<IConsignmentConnector>();
    }
}
