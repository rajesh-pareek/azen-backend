using Azen.Application.Authorization;
using Azen.Domain.Entities.App;

namespace Azen.Infrastructure.Authorization;

/// <summary>
/// Default ABAC policy for shipments. Pure, stateless, registered as a Singleton.
/// Every method is the SOLE source of truth for that permission - controllers must
/// not re-check roles inline. If a rule changes, change it here and nowhere else.
/// </summary>
public sealed class ShipmentAccessPolicy : IShipmentAccessPolicy
{
    private const string RoleTransporter = "transporter";
    private const string RoleFleetOwner = "fleet_owner";
    private const string RoleDriver = "driver";

    private const string SubRoleManager = "manager";

    private const string StatusPodUploaded = "pod_uploaded";
    private const string StatusShared = "shared";

    public bool CanCreateShipment(ShipmentAccessContext ctx)
        => ctx.Role == RoleTransporter;

    public bool CanView(ShipmentAccessContext ctx, Shipment shipment)
    {
        if (!SameOrg(ctx, shipment)) return false;

        return ctx.Role switch
        {
            RoleTransporter => true,
            RoleFleetOwner when ctx.SubRole == SubRoleManager => true,
            RoleFleetOwner => shipment.FleetOwnerMemberId == ctx.MemberId,
            RoleDriver => shipment.DriverMemberId == ctx.MemberId,
            _ => false
        };
    }

    public bool CanEditAllFields(ShipmentAccessContext ctx, Shipment shipment)
        => SameOrg(ctx, shipment) && ctx.Role == RoleTransporter;

    public bool CanEditOperationalFields(ShipmentAccessContext ctx, Shipment shipment)
    {
        if (!SameOrg(ctx, shipment)) return false;
        if (ctx.Role == RoleTransporter) return true;
        if (ctx.Role == RoleFleetOwner && shipment.FleetOwnerMemberId == ctx.MemberId) return true;
        return false;
    }

    public bool CanUploadDocument(ShipmentAccessContext ctx, Shipment shipment)
    {
        if (!SameOrg(ctx, shipment)) return false;

        // Transporter override - always allowed (core flexibility rule).
        if (ctx.Role == RoleTransporter) return true;
        if (ctx.Role == RoleFleetOwner && shipment.FleetOwnerMemberId == ctx.MemberId) return true;
        if (ctx.Role == RoleDriver && shipment.DriverMemberId == ctx.MemberId) return true;
        return false;
    }

    public bool CanDeleteDocument(ShipmentAccessContext ctx, Shipment shipment)
        => SameOrg(ctx, shipment) && ctx.Role == RoleTransporter;

    public bool CanAssignFleetOwner(ShipmentAccessContext ctx, Shipment shipment)
        => SameOrg(ctx, shipment) && ctx.Role == RoleTransporter;

    public bool CanAssignDriver(ShipmentAccessContext ctx, Shipment shipment)
    {
        if (!SameOrg(ctx, shipment)) return false;
        if (ctx.Role == RoleTransporter) return true;
        if (ctx.Role == RoleFleetOwner && shipment.FleetOwnerMemberId == ctx.MemberId) return true;
        return false;
    }

    public bool CanChangeStatus(ShipmentAccessContext ctx, Shipment shipment)
        => SameOrg(ctx, shipment) && ctx.Role == RoleTransporter;

    public bool CanGenerateShareLink(ShipmentAccessContext ctx, Shipment shipment)
    {
        if (!SameOrg(ctx, shipment)) return false;
        if (ctx.Role != RoleTransporter) return false;
        return shipment.Status == StatusPodUploaded || shipment.Status == StatusShared;
    }

    public bool CanViewAuditTrail(ShipmentAccessContext ctx, Shipment shipment)
        => SameOrg(ctx, shipment) && ctx.Role == RoleTransporter;

    public IQueryable<Shipment> FilterVisible(IQueryable<Shipment> query, ShipmentAccessContext ctx)
    {
        // Always enforce same-org first.
        query = query.Where(s => s.OrganisationId == ctx.OrgId);

        return ctx.Role switch
        {
            RoleTransporter => query,
            RoleFleetOwner when ctx.SubRole == SubRoleManager => query,
            RoleFleetOwner => query.Where(s => s.FleetOwnerMemberId == ctx.MemberId),
            RoleDriver => query.Where(s => s.DriverMemberId == ctx.MemberId),
            // Unknown role - see nothing
            _ => query.Where(_ => false)
        };
    }

    private static bool SameOrg(ShipmentAccessContext ctx, Shipment shipment)
        => shipment.OrganisationId == ctx.OrgId;
}
