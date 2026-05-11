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

    public ShipmentsController(AuthDbContext authDb, AppDbContext appDb, IShipmentRefService refService)
    {
        this.authDb = authDb;
        this.appDb = appDb;
        this.refService = refService;
    }

    //extrat jwt claims
    private Guid GetOrgId() => Guid.Parse(User.FindFirst("orgId")!.Value);
    private Guid GetMemberId() => Guid.Parse(User.FindFirst("member_id")!.Value);
    private string GetRole() => User.FindFirst("user_role")!.Value;


    [HttpPost]
    public async Task<IActionResult> CreateShipment([FromBody] CreateShipmentRequest request)
    {
        var role = GetRole();
        if (role != "transporter")
            return StatusCode(403, new { error = "FORBIDDEN", message = "Only transporters can create shipments" });

        var orgId = GetOrgId();
        var memberId = GetMemberId();

        //Auto generate reference number if not provided
        var refNumber = request.ReferenceNumber;
        if (string.IsNullOrWhiteSpace(refNumber))
        {
            refNumber = await refService.GenerateReferenceNumberAsync(orgId);
        }

        //check for duplicate reference number in this org
        var duplicate = await appDb.Shipments.AnyAsync(s => s.OrganisationId == orgId && s.ReferenceNumber == refNumber);

        if (duplicate)
        {
            return Conflict(new { error = "DUPLICATE_REFERENCE_NUMBER" });
        }

        var shipment = new Shipment
        {
            OrganisationId = orgId,
            ReferenceNumber = refNumber,
            ConsignorName = request.ConsignorName,
            ConsignorPhone = request.ConsignorPhone,
            ConsigneeName = request.ConsigneeName,
            ConsigneePhone = request.ConsigneePhone,
            GoodsDescription = request.GoodsDescription,
            Notes = request.Notes,
            Status = "created",
            CreatedByMemberId = memberId,
        };

        appDb.Shipments.Add(shipment);
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
        var orgId = GetOrgId();
        var memberId = GetMemberId();
        var role = GetRole();

        IQueryable<Shipment> query = appDb.Shipments.Where(s => s.OrganisationId == orgId);

        // Role -based filtering
        if (role == "fleet_owner")
            query = query.Where(s => s.FleetOwnerMemberId == memberId);
        else if (role == "driver")
            query = query.Where(s => s.DriverMemberId == memberId);

        //transporter sees all shipment in org - no extra filter

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
        var orgId = GetOrgId();
        var memberId = GetMemberId();
        var role = GetRole();

        var shipment = await appDb.Shipments.FirstOrDefaultAsync(s => s.Id == id && s.OrganisationId == orgId);

        if (shipment == null)
            return NotFound(new { error = "SHIPMENT_NOT_FOUND" });

        // role based access check

        if (role == "fleet_owner" && shipment.FleetOwnerMemberId != memberId)
            return StatusCode(403, new { error = "FORBIDDEN" });

        if (role == "driver" && shipment.DriverMemberId != memberId)
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
        var orgId = GetOrgId();
        var memberId = GetMemberId();
        var role = GetRole();

        var shipment = await appDb.Shipments.FirstOrDefaultAsync(s => s.Id == id && s.OrganisationId == orgId);

        if (shipment == null)
            return NotFound(new { error = "SHIPMENT_NOT_FOUND" });

        //transporter can edit all fields; fleet owner can edit vehicle n umber and notes only
        if (role == "transporter")
        {
            if (request.ConsignorName != null) shipment.ConsignorName = request.ConsignorName;
            if (request.ConsignorPhone != null) shipment.ConsignorPhone = request.ConsignorPhone;
            if (request.ConsigneeName != null) shipment.ConsigneeName = request.ConsigneeName;
            if (request.ConsigneePhone != null) shipment.ConsigneePhone = request.ConsigneePhone;
            if (request.GoodsDescription != null) shipment.GoodsDescription = request.GoodsDescription;
            if (request.VehicleNumber != null) shipment.VehicleNumber = request.VehicleNumber;
            if (request.Notes != null) shipment.Notes = request.Notes;
        }
        else if (role == "fleet_owner" && shipment.FleetOwnerMemberId == memberId)
        {
            if (request.VehicleNumber != null) shipment.VehicleNumber = request.VehicleNumber;
            if (request.Notes != null) shipment.Notes = request.Notes;
        }
        else
        {
            return StatusCode(403, new { error = "FORBIDDEN" });
        }

        shipment.UpdatedAt = DateTime.UtcNow;
        await appDb.SaveChangesAsync();
        return Ok(new { message = "Shipment_Updated" });
    }

    [HttpPost("{id}/assign-fleet-owner")]
    public async Task<IActionResult> AssignFleetOwner(Guid id, [FromBody] AssignFleetOwnerRequest request)
    {
        var orgId = GetOrgId();
        var role = GetRole();

        if (role != "transporter")
            return StatusCode(403, new { error = "FORBIDDEN" });

        var shipment = await appDb.Shipments.FirstOrDefaultAsync(s => s.Id == id && s.OrganisationId == orgId);

        if (shipment == null)
            return NotFound(new { error = "SHIPMENT_NOT_FOUND" });

        if (request.MemberId != null)
        {
            //in system assignment
            var member = await appDb.OrganisationMembers.FirstOrDefaultAsync(m => m.Id == request.MemberId && m.OrganisationId == orgId && m.IsActive);
            if (member == null)
                return NotFound(new { error = "MEMBER_NOT_FOUND" });

            var memberUser = await authDb.Users.FindAsync(member.UserId);

            shipment.FleetOwnerMemberId = member.Id;
            shipment.FleetOwnerName = memberUser?.Name; //will be looked up from member record
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
        var orgId = GetOrgId();
        var memberId = GetMemberId();
        var role = GetRole();

        //transporter r assigned fleet owner can assign driver
        var shipment = await appDb.Shipments.FirstOrDefaultAsync(s => s.Id == id && s.OrganisationId == orgId);
        if (shipment == null)
            return NotFound(new { error = "SHIPMENT_NOT_FOUND" });

        if (role == "fleet_owner" && shipment.FleetOwnerMemberId != memberId)
        {
            return StatusCode(403, new { error = "FORBIDDEN" });
        }
        if (role != "transporter" && role != "fleet_owner")
            return StatusCode(403, new { error = "FORBIDDEN" });

        if (request.MemberId != null)
        {
            var member = await appDb.OrganisationMembers.FirstOrDefaultAsync(m => m.Id == request.MemberId && m.OrganisationId == orgId && m.IsActive);

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

        if (request.VehicleNumber != null)
            shipment.VehicleNumber = request.VehicleNumber;

        shipment.UpdatedAt = DateTime.UtcNow;
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
        var orgId = GetOrgId();
        var role = GetRole();

        //only transporters can manually advance status
        if (role != "transporter")
            return StatusCode(403, new { error = "FORBIDDEN" });

        var shipment = await appDb.Shipments.FirstOrDefaultAsync(s => s.Id == id && s.OrganisationId == orgId);
        if (shipment == null)
        {
            return NotFound(new { error = "SHIPMENT_NOT_FOUND" });
        }

        //validate forawrd-only transitions

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

        shipment.Status = request.Status;
        shipment.UpdatedAt = DateTime.UtcNow;
        await appDb.SaveChangesAsync();

        return Ok(new
        {
            shipment_id = shipment.Id,
            status = shipment.Status
        });
    }
}