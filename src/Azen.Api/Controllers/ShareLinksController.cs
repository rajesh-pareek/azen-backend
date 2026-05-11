using System.Security.Cryptography;
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

    public ShareLinksController(AppDbContext appDb, IStorageService storageService)
    {
        this.appDb = appDb;
        this.storageService = storageService;
    }

    private Guid GetOrgId() => Guid.Parse(User.FindFirst("orgId")!.Value);
    private Guid GetMemberId() => Guid.Parse(User.FindFirst("member_id")!.Value);
    private string GetRole() => User.FindFirst("user_role")!.Value;

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
        var orgId = GetOrgId();
        var memberId = GetMemberId();
        var role = GetRole();

        if (role != "transporter") return StatusCode(403, new { error = "FORBIDDEN" });

        var shipment = await appDb.Shipments.FirstOrDefaultAsync(s => s.Id == shipmentId && s.OrganisationId == orgId);

        if (shipment == null) return NotFound(new { error = "SHIPMENT_NOT_FOUND" });

        //can only share when POD is uploaded or already shared
        if (shipment.Status != "pod_uploaded" && shipment.Status != "shared")
            return BadRequest(new { error = "INVALID_STATUS", message = "Shipment must have POD uploaded before sharing" });

        var token = GenerateToken();

        var shareLink = new ShareLink
        {
            Token = token,
            ShipmentId = shipmentId,
            CreatedByMemberId = memberId,
            ExpiresAt = DateTime.UtcNow.AddDays(request.ExpiresInDays ?? 30),
            VisibleDocTypes = System.Text.Json.JsonSerializer.Serialize(request.VisibleDocTypes)
        };

        appDb.ShareLinks.Add(shareLink);
        //auto-advance status to shared

        if (shipment.Status == "pod_uploaded")
            shipment.Status = "shared";

        shipment.UpdatedAt = DateTime.UtcNow;
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
        var orgId = GetOrgId();
        var role = GetRole();

        if (role != "transporter")
            return StatusCode(403, new { error = "FORBIDDEN" });

        var shipment = await appDb.Shipments.FirstOrDefaultAsync(s => s.Id == shipmentId && s.OrganisationId == orgId);

        if (shipment == null) return NotFound(new { error = "SHIPMENT_NOT_FOUND" });

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
        var orgId = GetOrgId();
        var role = GetRole();

        if (role != "transporter")
            return StatusCode(403, new { error = "FORBIDDEN" });

        var link = await appDb.ShareLinks
            .Include(l => l.Shipment)
            .FirstOrDefaultAsync(l => l.Id == linkId && l.Shipment.OrganisationId == orgId);

        if (link == null) return NotFound(new { error = "LINK_NOT_FOUND" });
        link.IsRevoked = true;
        await appDb.SaveChangesAsync();
        return Ok(new { message = "Share link revoked" });
    }

    //public endpoint - not auth required
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

        //Generate pre signed Urls (30 minutes for public access)
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