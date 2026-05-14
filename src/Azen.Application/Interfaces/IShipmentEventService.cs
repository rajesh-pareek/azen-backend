using Azen.Domain.Entities.App;

namespace Azen.Application.Interfaces;

public interface IShipmentEventService
{
    Task LogAsync(Guid shipmentId, ShipmentEventType type, Guid actorMemberId, string role, object? payload = null, CancellationToken ct = default);
}