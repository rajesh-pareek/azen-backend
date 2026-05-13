using System.Security.Cryptography;
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
[Authorize]
public class ShareLinksController : ControllerBase
{
    private readonly AppDbContext appDb;
    private readonly IStorageService storageService;
    private readonly IShipmentEventService eventService;
    private readonly IShipmentAccessPolicy policy;

    public ShareLinksController(
        AppDbContext appDb,
        IStorageService storageService,
        IShipmentEventService eventService,
        IShipmentAccessPolicy policy)
    {
        this.appDb = appDb;
        this.storageService = storageService;
        this.eventService = eventService;
        this.policy = policy;
    }

    //generate a random base62 token (10 chars)
    private static string GenerateToken()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        var bytes = RandomNumberGenerator.GetBytes(10);
        var result = new char[10];
        for (int i = 0; i < 10; i++)
        {
            result[i] = chars[bytes[i] % chars.Length];
        }
        return new string(result);
    }

    [HttpPost("api/v1/shipments/{shipmentId}/share-links")]
    public async Task<IActionResult> CreateShareLink(Guid shipmentId, [FromBody] CreateShareLinkRequest request)
    {
        var ctx = User.ToShipmentAccessContext();

        var shipment = await appDb.Shipments.FirstOrDefaultAsync(s => s.Id == shipmentId && s.OrganisationId == ctx.OrgId);
        if (shipment == null) return NotFound(new { error = "SHIPMENT_NOT_FOUND" });

        // Policy enforces transporter-only AND status in (pod_uploaded, shared).
        if (!policy.CanGenerateShareLink(ctx, shipment))
        {
            // Distinguish the two failure modes so the client can react.
            if (ctx.Role != "transporter")
                return StatusCode(403, new { error = "FORBIDDEN" });
            return BadRequest(new { error = "INVALID_STATUS", message = "Shipment must have POD uploaded before sharing" });
        }

        var token = GenerateToken();

        var shareLink = new ShareLink
        {
            Token = token,
            ShipmentId = shipmentId,
            CreatedByMemberId = ctx.MemberId,
            ExpiresAt = DateTime.UtcNow.AddDays(request.ExpiresInDays ?? 30),
            VisibleDocTypes = System.Text.Json.JsonSerializer.Serialize(request.VisibleDocTypes)
        };

        appDb.ShareLinks.Add(shareLink);
        var previousStatus = shipment.Status;

        //auto-advance status to shared
        if (shipment.Status == "pod_uploaded")
            shipment.Status = "shared";

        shipment.UpdatedAt = DateTime.UtcNow;

        await eventService.LogAsync(
            shipment.Id, ShipmentEventType.ShareLinkGenerated, ctx.MemberId, ctx.Role,
            new
            {
                link_id = shareLink.Id,
                visible_doc_types = request.VisibleDocTypes,
                expires_at = shareLink.ExpiresAt
            });

        if (previousStatus != shipment.Status)
        {
            await eventService.LogAsync(
                shipment.Id, ShipmentEventType.StatusChanged, ctx.MemberId, ctx.Role,
                new { from = previousStatus, to = shipment.Status, trigger = "share_link_generated" });
        }
        await appDb.SaveChangesAsync();

        return Created("", new
        {
            id = shareLink.Id,
            token = shareLink.Token,
            url = $"{Request.Scheme}://{Request.Host}/api/v1/public/s/{token}",
            expires_at = shareLink.ExpiresAt,
            visible_doc_types = request.VisibleDocTypes
        });
    }

    [HttpGet("api/v1/shipments/{shipmentId}/share-links")]
    public async Task<IActionResult> ListShareLinks(Guid shipmentId)
    {
        var ctx = User.ToShipmentAccessContext();

        var shipment = await appDb.Shipments.FirstOrDefaultAsync(s => s.Id == shipmentId && s.OrganisationId == ctx.OrgId);
        if (shipment == null) return NotFound(new { error = "SHIPMENT_NOT_FOUND" });

        // Listing share links is a transporter-only audit-style action.
        // We reuse CanViewAuditTrail since the rules are identical (transporter, same org).
        if (!policy.CanViewAuditTrail(ctx, shipment))
            return StatusCode(403, new { error = "FORBIDDEN" });

        var links = await appDb.ShareLinks
          .Where(l => l.ShipmentId == shipmentId && !l.IsRevoked)
          .Select(l => new
          {
              id = l.Id,
              token = l.Token,
              expires_at = l.ExpiresAt,
              visible_doc_types = l.VisibleDocTypes,
              access_count = l.AccessCount,
              last_accessed_at = l.LastAccessAt,
              created_at = l.CreatedAt
          })
          .ToListAsync();

        return Ok(links);
    }

    [HttpDelete("api/v1/share-links/{linkId}")]
    public async Task<IActionResult> RevokeShareLink(Guid linkId)
    {
        var ctx = User.ToShipmentAccessContext();

        var link = await appDb.ShareLinks
            .Include(l => l.Shipment)
            .FirstOrDefaultAsync(l => l.Id == linkId && l.Shipment.OrganisationId == ctx.OrgId);

        if (link == null) return NotFound(new { error = "LINK_NOT_FOUND" });

        // Same-org is guaranteed by the query above; policy enforces transporter-only.
        if (!policy.CanViewAuditTrail(ctx, link.Shipment))
            return StatusCode(403, new { error = "FORBIDDEN" });

        link.IsRevoked = true;

        await eventService.LogAsync(
            link.ShipmentId, ShipmentEventType.ShareLinkRevoked, ctx.MemberId, ctx.Role,
            new { link_id = link.Id, token = link.Token });

        await appDb.SaveChangesAsync();
        return Ok(new { message = "Share link revoked" });
    }

    //public endpoint - no auth required
    [AllowAnonymous]
    [HttpGet("api/v1/public/s/{token}")]
    public async Task<IActionResult> ViewSharedShipment(string token)
    {
        var link = await appDb.ShareLinks
            .Include(link => link.Shipment)
            .FirstOrDefaultAsync(link => link.Token == token);
        if (link == null)
            return NotFound(new { error = "LINK_NOT_FOUND" });

        if (link.IsRevoked)
            return StatusCode(410, new { error = "LINK_REVOKED" });

        if (link.ExpiresAt < DateTime.UtcNow)
            return StatusCode(410, new { error = "LINK_EXPIRED" });

        //track access
        link.AccessCount++;
        link.LastAccessAt = DateTime.UtcNow;

        var shipment = link.Shipment;

        //get visible doc types
        var visibleDocTypes = System.Text.Json.JsonSerializer.Deserialize<List<string>>(link.VisibleDocTypes) ?? new List<string>();

        //fetch documents filtered by visible types
        var documents = await appDb.ShipmentDocuments
           .Where(d => d.ShipmentId == shipment.Id && !d.IsDeleted && visibleDocTypes.Contains(d.DocType))
           .ToListAsync();

        //Generate presigned URLs (30 minutes for public access)
        var docResults = documents.Select(d => new
        {
            id = d.Id,
            doc_type = d.DocType,
            original_filename = d.OriginalFileName,
            view_url = storageService.GetPresignedUrl(d.StorageKey, 30)
        }).ToList();

        await appDb.SaveChangesAsync();

        return Ok(new
        {
            shipment = new
            {
                reference_number = shipment.ReferenceNumber,
                consignor_name = shipment.ConsignorName,
                consignee_name = shipment.ConsigneeName,
                status = shipment.Status
            },
            documents = docResults,
            link = new
            {
                expires_at = link.ExpiresAt
            }
        });
    }
}
