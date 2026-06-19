using System.Collections.Concurrent;

namespace Zahy.Connectors;

public sealed class InMemoryCarrierConnectorTransport
{
    private readonly ConcurrentDictionary<string, CarrierShipmentRequest> _staged = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, CarrierShipmentRecord> _shipments = new(StringComparer.OrdinalIgnoreCase);

    public void Reset()
    {
        _staged.Clear();
        _shipments.Clear();
    }

    public void StageShipment(CarrierShipmentRequest request) =>
        _staged[request.ExternalOrderId] = request;

    public bool TryGetStaged(string externalOrderId, out CarrierShipmentRequest request) =>
        _staged.TryGetValue(externalOrderId, out request!);

    public void SaveShipment(CarrierShipmentRecord record) =>
        _shipments[record.ExternalOrderId] = record;

    public bool TryGetShipment(string externalOrderId, out CarrierShipmentRecord record) =>
        _shipments.TryGetValue(externalOrderId, out record!);
}
