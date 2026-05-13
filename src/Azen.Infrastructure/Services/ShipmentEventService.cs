using System.Text.Json;
using Azen.Application.Interfaces;
using Azen.Domain.Entities.App;
using Azen.Infrastructure.Persistence;

namespace Azen.Infrastructure.Services;

public class ShipmentEventService : IShipmentEventService
{
    private readonly AppDbContext appDb;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };

    public ShipmentEventService(AppDbContext appDb)
    {
        this.appDb = appDb;
    }

    public Task LogAsync(Guid shipmentId, ShipmentEventType type, Guid actorMemberId, string role, object? payload = null, CancellationToken ct = default)
    {
        var evt = new ShipmentEvent
        {
            ShipmentId = shipmentId,
            EventType = type.ToDbString(),
            ActorId = actorMemberId,
            ActorRole = string.IsNullOrWhiteSpace(role) ? "system" : role,
            Payload = payload is null ? "{}" : JsonSerializer.Serialize(payload, JsonOptions)
        };

        appDb.ShipmentEvents.Add(evt);
        return Task.CompletedTask;
    }

}