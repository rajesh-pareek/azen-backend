namespace Azen.Domain.Entities.App;

public enum ShipmentEventType
{
    ShipmentCreated,
    StatusChanged,
    FleetOwnerAssigned,
    FleetOwnerReassigned,
    DriverAssigned,
    DriverReassigned,
    VehicleUpdated,
    DocumentUploaded,
    DocumentDeleted,
    ShareLinkGenerated,
    ShareLinkRevoked,
    MetadataUpdated
}

public static class ShipmentEventTypeExtensions
{
    //map to snake case db strings expected by db check constraint
    public static string ToDbString(this ShipmentEventType type) => type switch
    {
        ShipmentEventType.ShipmentCreated => "shipment_created",
        ShipmentEventType.StatusChanged => "status_changed",
        ShipmentEventType.FleetOwnerAssigned => "fleet_owner_assigned",
        ShipmentEventType.FleetOwnerReassigned => "fleet_owner_reassigned",
        ShipmentEventType.DriverAssigned => "driver_assigned",
        ShipmentEventType.DriverReassigned => "driver_reassigned",
        ShipmentEventType.VehicleUpdated => "vehicle_updated",
        ShipmentEventType.DocumentUploaded => "document_uploaded",
        ShipmentEventType.DocumentDeleted => "document_deleted",
        ShipmentEventType.ShareLinkGenerated => "share_link_generated",
        ShipmentEventType.ShareLinkRevoked => "share_link_revoked",
        ShipmentEventType.MetadataUpdated => "metadata_updated",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };

}