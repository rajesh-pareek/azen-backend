using Azen.Domain.Entities.App;

namespace Azen.Application.Authorization;

/// <summary>
/// ABAC policy engine for shipments. Pure functions - no DB calls inside.
/// Caller loads the shipment, hands it to the policy.
/// Mirrors rules from mvp-design.md §8.
/// </summary>
public interface IShipmentAccessPolicy
{
    /// <summary>Can the caller create a new shipment in their org? Transporter only.</summary>
    bool CanCreateShipment(ShipmentAccessContext ctx);

    /// <summary>Can the caller see this shipment in detail?</summary>
    bool CanView(ShipmentAccessContext ctx, Shipment shipment);

    /// <summary>Can the caller edit ALL fields (consignor, consignee, goods, etc.)?
    /// Transporter only.</summary>
    bool CanEditAllFields(ShipmentAccessContext ctx, Shipment shipment);

    /// <summary>Can the caller edit operational fields only (vehicle_number, notes)?
    /// Transporter (covered by CanEditAllFields too) or assigned fleet owner.</summary>
    bool CanEditOperationalFields(ShipmentAccessContext ctx, Shipment shipment);

    /// <summary>Can the caller upload a document?
    /// Transporter ALWAYS (core flexibility rule); fleet_owner/driver if assigned.</summary>
    bool CanUploadDocument(ShipmentAccessContext ctx, Shipment shipment);

    /// <summary>Can the caller (soft-)delete a document? Transporter only.</summary>
    bool CanDeleteDocument(ShipmentAccessContext ctx, Shipment shipment);

    /// <summary>Can the caller assign a fleet owner? Transporter only.</summary>
    bool CanAssignFleetOwner(ShipmentAccessContext ctx, Shipment shipment);

    /// <summary>Can the caller assign a driver? Transporter or assigned fleet_owner.</summary>
    bool CanAssignDriver(ShipmentAccessContext ctx, Shipment shipment);

    /// <summary>Can the caller manually advance the shipment status? Transporter only.</summary>
    bool CanChangeStatus(ShipmentAccessContext ctx, Shipment shipment);

    /// <summary>Can the caller generate or list share links?
    /// Transporter only AND status must be pod_uploaded or shared (per §8).</summary>
    bool CanGenerateShareLink(ShipmentAccessContext ctx, Shipment shipment);

    /// <summary>Can the caller see the audit trail? Transporter only.</summary>
    bool CanViewAuditTrail(ShipmentAccessContext ctx, Shipment shipment);

    /// <summary>
    /// Apply role-based visibility filter to a Shipments query for list endpoints.
    /// Mirrors §7.3 filtering logic. Always also enforces same-org.
    /// </summary>
    IQueryable<Shipment> FilterVisible(IQueryable<Shipment> query, ShipmentAccessContext ctx);
}
