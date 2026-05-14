namespace Azen.Domain.Entities.App;

public class ShipmentEvent
{
    public Guid Id { get; set; }
    public Guid ShipmentId { get; set; }
    public string EventType { get; set; } = string.Empty; //"shipment-created", "status changed"
    public Guid? ActorId { get; set; } //null for system events
    public string ActorRole { get; set; } = string.Empty; //"transporter" "fleet-owner"
    public string Payload { get; set; } = string.Empty; //json string

    public DateTime CreatedAt { get; set; }

    //Navigation
    public Shipment Shipment { get; set; } = null!;

}