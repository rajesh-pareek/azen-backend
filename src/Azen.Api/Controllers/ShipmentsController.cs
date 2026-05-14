using Azen.Api.Authorization;
using Azen.Application.Authorization;
using Azen.Application.DTOs.App;
using Azen.Application.Interfaces;
using Azen.Domain.Entities.App;
using Azen.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Azen.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class ShipmentsController : ControllerBase
{
    private readonly AuthDbContext authDb;
    private readonly AppDbContext appDb;
    private readonly IShipmentRefService refService;
    private readonly IShipmentEventService eventService;
    private readonly IShipmentAccessPolicy policy;

    public ShipmentsController(
        AuthDbContext authDb,
        AppDbContext appDb,
        IShipmentRefService refService,
        IShipmentEventService eventService,
        IShipmentAccessPolicy policy)
    {
        this.authDb = authDb;
        this.appDb = appDb;
        this.refService = refService;
        this.eventService = eventService;
        this.policy = policy;
    }

    [HttpPost]
    public async Task<IActionResult> CreateShipment([FromBody] CreateShipmentRequest request)
    {
        var ctx = User.ToShipmentAccessContext();

        if (!policy.CanCreateShipment(ctx))
            return StatusCode(403, new { error = "FORBIDDEN", message = "Only transporters can create shipments" });

        //Auto generate reference number if not provided
        var refNumber = request.ReferenceNumber;
        if (string.IsNullOrWhiteSpace(refNumber))
        {
            refNumber = await refService.GenerateReferenceNumberAsync(ctx.OrgId);
        }

        //check for duplicate reference number in this org
        var duplicate = await appDb.Shipments.AnyAsync(s => s.OrganisationId == ctx.OrgId && s.ReferenceNumber == refNumber);

        if (duplicate)
        {
            return Conflict(new { error = "DUPLICATE_REFERENCE_NUMBER" });
        }

        var shipment = new Shipment
        {
            OrganisationId = ctx.OrgId,
            ReferenceNumber = refNumber,
            ConsignorName = request.ConsignorName,
            ConsignorPhone = request.ConsignorPhone,
            ConsigneeName = request.ConsigneeName,
            ConsigneePhone = request.ConsigneePhone,
            GoodsDescription = request.GoodsDescription,
            Notes = request.Notes,
            Status = "created",
            CreatedByMemberId = ctx.MemberId,
        };

        appDb.Shipments.Add(shipment);

        await eventService.LogAsync(shipment.Id, ShipmentEventType.ShipmentCreated, ctx.MemberId, ctx.Role,
        new { reference_number = shipment.ReferenceNumber });
        await appDb.SaveChangesAsync();

        return Created("", new
        {
            id = shipment.Id,
            reference_number = shipment.ReferenceNumber,
            status = shipment.Status,
            created_at = shipment.CreatedAt
        });
    }

    [HttpGet]
    public async Task<IActionResult> ListShipments()
    {
        var ctx = User.ToShipmentAccessContext();

        var query = policy.FilterVisible(appDb.Shipments, ctx);

        var shipments = await query
           .OrderByDescending(s => s.CreatedAt)
           .Select(s => new
           {
               id = s.Id,
               reference_number = s.ReferenceNumber,
               status = s.Status,
               consignor_name = s.ConsignorName,
               consignee_name = s.ConsigneeName,
               fleet_owner_name = s.FleetOwnerName,
               driver_name = s.DriverName,
               vehicle_number = s.VehicleNumber,
               created_at = s.CreatedAt
           })
           .ToListAsync();

        return Ok(shipments);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetShipment(Guid id)
    {
        var ctx = User.ToShipmentAccessContext();

        var shipment = await appDb.Shipments.FirstOrDefaultAsync(s => s.Id == id && s.OrganisationId == ctx.OrgId);
        if (shipment == null)
            return NotFound(new { error = "SHIPMENT_NOT_FOUND" });

        if (!policy.CanView(ctx, shipment))
            return StatusCode(403, new { error = "FORBIDDEN" });

        return Ok(new
        {
            id = shipment.Id,
            reference_number = shipment.ReferenceNumber,
            status = shipment.Status,
            consignor_name = shipment.ConsignorName,
            consignor_phone = shipment.ConsignorPhone,
            consignee_name = shipment.ConsigneeName,
            consignee_phone = shipment.ConsigneePhone,
            goods_description = shipment.GoodsDescription,
            vehicle_number = shipment.VehicleNumber,
            fleet_owner_name = shipment.FleetOwnerName,
            fleet_owner_phone = shipment.FleetOwnerPhone,
            fleet_owner_in_system = shipment.FleetOwnerInSystem,
            driver_name = shipment.DriverName,
            driver_phone = shipment.DriverPhone,
            driver_in_system = shipment.DriverInSystem,
            notes = shipment.Notes,
            created_at = shipment.CreatedAt,
            updated_at = shipment.UpdatedAt
        });
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> UpdateShipment(Guid id, [FromBody] UpdateShipmentRequest request)
    {
        var ctx = User.ToShipmentAccessContext();

        var shipment = await appDb.Shipments.FirstOrDefaultAsync(s => s.Id == id && s.OrganisationId == ctx.OrgId);
        if (shipment == null)
            return NotFound(new { error = "SHIPMENT_NOT_FOUND" });

        var canEditAll = policy.CanEditAllFields(ctx, shipment);
        var canEditOps = policy.CanEditOperationalFields(ctx, shipment);

        if (!canEditAll && !canEditOps)
            return StatusCode(403, new { error = "FORBIDDEN" });

        var oldVehicleNumber = shipment.VehicleNumber;
        var changedFields = new List<string>();

        if (canEditAll)
        {
            if (request.ConsignorName != null) { shipment.ConsignorName = request.ConsignorName; changedFields.Add("consignor_name"); }
            if (request.ConsignorPhone != null) { shipment.ConsignorPhone = request.ConsignorPhone; changedFields.Add("consignor_phone"); }
            if (request.ConsigneeName != null) { shipment.ConsigneeName = request.ConsigneeName; changedFields.Add("consignee_name"); }
            if (request.ConsigneePhone != null) { shipment.ConsigneePhone = request.ConsigneePhone; changedFields.Add("consignee_phone"); }
            if (request.GoodsDescription != null) { shipment.GoodsDescription = request.GoodsDescription; changedFields.Add("goods_description"); }
            if (request.VehicleNumber != null) { shipment.VehicleNumber = request.VehicleNumber; changedFields.Add("vehicle_number"); }
            if (request.Notes != null) { shipment.Notes = request.Notes; changedFields.Add("notes"); }
        }
        else // canEditOps only - vehicle + notes
        {
            if (request.VehicleNumber != null) { shipment.VehicleNumber = request.VehicleNumber; changedFields.Add("vehicle_number"); }
            if (request.Notes != null) { shipment.Notes = request.Notes; changedFields.Add("notes"); }
        }

        shipment.UpdatedAt = DateTime.UtcNow;

        if (changedFields.Contains("vehicle_number"))
        {
            await eventService.LogAsync(shipment.Id, ShipmentEventType.VehicleUpdated, ctx.MemberId, ctx.Role,
            new { from = oldVehicleNumber, to = shipment.VehicleNumber });
        }

        var nonVehicleChanges = changedFields.Where(f => f != "vehicle_number").ToList();
        if (nonVehicleChanges.Count > 0)
        {
            await eventService.LogAsync(shipment.Id, ShipmentEventType.MetadataUpdated, ctx.MemberId, ctx.Role,
            new { fields = nonVehicleChanges });
        }

        await appDb.SaveChangesAsync();
        return Ok(new { message = "Shipment_Updated" });
    }

    [HttpPost("{id}/assign-fleet-owner")]
    public async Task<IActionResult> AssignFleetOwner(Guid id, [FromBody] AssignFleetOwnerRequest request)
    {
        var ctx = User.ToShipmentAccessContext();

        var shipment = await appDb.Shipments.FirstOrDefaultAsync(s => s.Id == id && s.OrganisationId == ctx.OrgId);
        if (shipment == null)
            return NotFound(new { error = "SHIPMENT_NOT_FOUND" });

        if (!policy.CanAssignFleetOwner(ctx, shipment))
            return StatusCode(403, new { error = "FORBIDDEN" });

        var wasAlreadyAssigned = shipment.FleetOwnerMemberId != null || !string.IsNullOrEmpty(shipment.FleetOwnerName);
        var previousStatus = shipment.Status;

        if (request.MemberId != null)
        {
            //in system assignment
            var member = await appDb.OrganisationMembers.FirstOrDefaultAsync(m => m.Id == request.MemberId && m.OrganisationId == ctx.OrgId && m.IsActive);
            if (member == null)
                return NotFound(new { error = "MEMBER_NOT_FOUND" });

            var memberUser = await authDb.Users.FindAsync(member.UserId);

            shipment.FleetOwnerMemberId = member.Id;
            shipment.FleetOwnerName = memberUser?.Name;
            shipment.FleetOwnerPhone = memberUser?.Phone;
            shipment.FleetOwnerInSystem = true;
        }
        else if (!string.IsNullOrWhiteSpace(request.Name) && !string.IsNullOrWhiteSpace(request.Phone))
        {
            //External assignment
            shipment.FleetOwnerMemberId = null;
            shipment.FleetOwnerName = request.Name;
            shipment.FleetOwnerPhone = request.Phone;
            shipment.FleetOwnerInSystem = false;
        }
        else
        {
            return BadRequest(new { error = "INVALID_REQUEST", message = "Provide either memberId or name + phone" });
        }

        //auto-advance status
        if (shipment.Status == "created")
            shipment.Status = "assigned";

        shipment.UpdatedAt = DateTime.UtcNow;

        var assignmentType = wasAlreadyAssigned ? ShipmentEventType.FleetOwnerReassigned : ShipmentEventType.FleetOwnerAssigned;
        await eventService.LogAsync(shipment.Id, assignmentType, ctx.MemberId, ctx.Role,
        new
        {
            in_system = shipment.FleetOwnerInSystem,
            member_id = shipment.FleetOwnerMemberId,
            name = shipment.FleetOwnerName
        });

        if (previousStatus != shipment.Status)
        {
            await eventService.LogAsync(shipment.Id, ShipmentEventType.StatusChanged, ctx.MemberId, ctx.Role,
            new
            {
                from = previousStatus,
                to = shipment.Status,
                trigger = "fleet_owner_assigned"
            });
        }

        await appDb.SaveChangesAsync();
        return Ok(new
        {
            shipment_id = shipment.Id,
            fleet_owner_name = shipment.FleetOwnerName,
            fleet_owner_in_system = shipment.FleetOwnerInSystem,
            status = shipment.Status
        });
    }

    [HttpPost("{id}/assign-driver")]
    public async Task<IActionResult> AssignDriver(Guid id, [FromBody] AssignDriverRequest request)
    {
        var ctx = User.ToShipmentAccessContext();

        var shipment = await appDb.Shipments.FirstOrDefaultAsync(s => s.Id == id && s.OrganisationId == ctx.OrgId);
        if (shipment == null)
            return NotFound(new { error = "SHIPMENT_NOT_FOUND" });

        if (!policy.CanAssignDriver(ctx, shipment))
            return StatusCode(403, new { error = "FORBIDDEN" });

        var wasAlreadyAssigned = shipment.DriverMemberId != null || !string.IsNullOrEmpty(shipment.DriverName);
        var previousStatus = shipment.Status;
        var oldVehicleNumber = shipment.VehicleNumber;

        if (request.MemberId != null)
        {
            var member = await appDb.OrganisationMembers.FirstOrDefaultAsync(m => m.Id == request.MemberId && m.OrganisationId == ctx.OrgId && m.IsActive);
            if (member == null) return NotFound(new { error = "MEMBER_NOT_FOUND" });

            var memberUser = await authDb.Users.FindAsync(member.UserId);

            shipment.DriverMemberId = member.Id;
            shipment.DriverName = memberUser?.Name;
            shipment.DriverPhone = memberUser?.Phone;
            shipment.DriverInSystem = true;
        }
        else if (!string.IsNullOrWhiteSpace(request.Name) && !string.IsNullOrWhiteSpace(request.Phone))
        {
            shipment.DriverMemberId = null;
            shipment.DriverName = request.Name;
            shipment.DriverPhone = request.Phone;
            shipment.DriverInSystem = false;
        }
        else
        {
            return BadRequest(new { error = "INVALID_REQUEST", message = "Provide either memberId or name + Phone." });
        }

        var vehicleChanged = false;
        if (request.VehicleNumber != null && request.VehicleNumber != oldVehicleNumber)
        {
            shipment.VehicleNumber = request.VehicleNumber;
            vehicleChanged = true;
        }

        // auto-advance: design allows status to flow on assignment
        if (shipment.Status == "created")
            shipment.Status = "assigned";

        shipment.UpdatedAt = DateTime.UtcNow;

        var assignmentType = wasAlreadyAssigned
            ? ShipmentEventType.DriverReassigned
            : ShipmentEventType.DriverAssigned;
        await eventService.LogAsync(
            shipment.Id, assignmentType, ctx.MemberId, ctx.Role,
            new
            {
                in_system = shipment.DriverInSystem,
                member_id = shipment.DriverMemberId,
                name = shipment.DriverName
            });

        if (vehicleChanged)
        {
            await eventService.LogAsync(
                shipment.Id, ShipmentEventType.VehicleUpdated, ctx.MemberId, ctx.Role,
                new { from = oldVehicleNumber, to = shipment.VehicleNumber });
        }

        if (previousStatus != shipment.Status)
        {
            await eventService.LogAsync(
                shipment.Id, ShipmentEventType.StatusChanged, ctx.MemberId, ctx.Role,
                new { from = previousStatus, to = shipment.Status, trigger = "driver_assigned" });
        }

        await appDb.SaveChangesAsync();

        return Ok(new
        {
            shipment_id = shipment.Id,
            driver_name = shipment.DriverName,
            driver_in_system = shipment.DriverInSystem,
            vehicle_number = shipment.VehicleNumber,
            status = shipment.Status
        });
    }

    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateStatusRequest request)
    {
        var ctx = User.ToShipmentAccessContext();

        var shipment = await appDb.Shipments.FirstOrDefaultAsync(s => s.Id == id && s.OrganisationId == ctx.OrgId);
        if (shipment == null)
            return NotFound(new { error = "SHIPMENT_NOT_FOUND" });

        if (!policy.CanChangeStatus(ctx, shipment))
            return StatusCode(403, new { error = "FORBIDDEN" });

        //validate forward-only transitions
        var validTransitions = new Dictionary<string, List<string>>
        {
            {"created", new List<string> {"assigned", "pod_uploaded"}},
            {"assigned", new List<string> {"pod_uploaded"}},
            {"pod_uploaded", new List<string> {"shared"}}
        };

        if (!validTransitions.ContainsKey(shipment.Status) || !validTransitions[shipment.Status].Contains(request.Status))
        {
            return BadRequest(new
            {
                error = "INVALID_TRANSITION",
                message = $"Cannot transition from '{shipment.Status}' to '{request.Status}'"
            });
        }

        var previousStatus = shipment.Status;
        shipment.Status = request.Status;
        shipment.UpdatedAt = DateTime.UtcNow;

        await eventService.LogAsync(
            shipment.Id, ShipmentEventType.StatusChanged, ctx.MemberId, ctx.Role,
            new { from = previousStatus, to = shipment.Status, trigger = "manual" });

        await appDb.SaveChangesAsync();

        return Ok(new
        {
            shipment_id = shipment.Id,
            status = shipment.Status
        });
    }

    // Audit trail read endpoint
    [HttpGet("{id}/events")]
    public async Task<IActionResult> GetShipmentEvents(Guid id)
    {
        var ctx = User.ToShipmentAccessContext();

        var shipment = await appDb.Shipments
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id && s.OrganisationId == ctx.OrgId);
        if (shipment == null) return NotFound(new { error = "SHIPMENT_NOT_FOUND" });

        if (!policy.CanViewAuditTrail(ctx, shipment))
            return StatusCode(403, new { error = "FORBIDDEN" });

        var events = await appDb.ShipmentEvents
            .AsNoTracking()
            .Where(e => e.ShipmentId == id)
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => new
            {
                id = e.Id,
                event_type = e.EventType,
                actor_id = e.ActorId,
                actor_role = e.ActorRole,
                payload = e.Payload,
                created_at = e.CreatedAt
            })
            .ToListAsync();

        return Ok(events);
    }
}
