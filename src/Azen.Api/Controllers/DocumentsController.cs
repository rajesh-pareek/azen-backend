using Azen.Api.Authorization;
using Azen.Api.DTOs;
using Azen.Application.Authorization;
using Azen.Application.Interfaces;
using Azen.Domain.Entities.App;
using Azen.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Azen.Api.Controllers;

[ApiController]
[Route("api/v1/shipments/{shipmentId}/documents")]
[Authorize]
public class DocumentsController : ControllerBase
{
    private readonly AppDbContext appDb;
    private readonly IStorageService storageService;
    private readonly IShipmentEventService eventService;
    private readonly IShipmentAccessPolicy policy;

    public DocumentsController(
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

    private static readonly HashSet<string> AllowedMimeTypes = new()
    {
        "image/jpeg", "image/png", "image/webp", "application/pdf"
    };

    private static readonly HashSet<string> AllowedDocTypes = new()
    {
        "pod", "invoice", "lr", "weightbridge", "eway_bill", "consignment_note", "custom"
    };

    private const int MaxFileSizeBytes = 5 * 1024 * 1024; //5MB

    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadDocument(Guid shipmentId, [FromForm] UploadDocumentRequest request)
    {
        var ctx = User.ToShipmentAccessContext();
        var file = request.File;
        var docType = request.DocType;

        //1. Validate doc type
        if (!AllowedDocTypes.Contains(docType))
            return BadRequest(new { error = "INVALID_DOC_TYPE", allowed = AllowedDocTypes });
        //2. Validate file
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { error = "NO_FILE" });
        }

        if (file.Length > MaxFileSizeBytes)
        {
            return StatusCode(413, new { error = "FILE_TOO_LARGE", max_size_mb = 5 });
        }

        if (!AllowedMimeTypes.Contains(file.ContentType))
        {
            return StatusCode(415, new { error = "UNSUPPORTED_FILE_TYPE", allowed = AllowedMimeTypes });
        }

        //3. Find shipment
        var shipment = await appDb.Shipments.FirstOrDefaultAsync(s => s.Id == shipmentId && s.OrganisationId == ctx.OrgId);
        if (shipment == null) return NotFound(new { error = "SHIPMENT_NOT_FOUND" });

        //4. ABAC check
        if (!policy.CanUploadDocument(ctx, shipment))
            return StatusCode(403, new { error = "FORBIDDEN" });

        //5. Upload to storage
        var fileExtension = Path.GetExtension(file.FileName);
        var storageKey = $"{ctx.OrgId}/{shipmentId}/{Guid.NewGuid()}{fileExtension}";

        using var stream = file.OpenReadStream();
        await storageService.UploadAsync(storageKey, stream, file.ContentType);

        //6. Save metadata to DB
        var document = new ShipmentDocument
        {
            ShipmentId = shipmentId,
            DocType = docType,
            StorageKey = storageKey,
            OriginalFileName = file.FileName,
            FileSizeBytes = (int)file.Length,
            MimeType = file.ContentType,
            UploadedByMemberId = ctx.MemberId,
            UploaderRole = ctx.Role
        };

        appDb.ShipmentDocuments.Add(document);
        var previousStatus = shipment.Status;

        //7. Auto-advance status to pod_uploaded if POD is uploaded
        if (docType == "pod" && (shipment.Status == "created" || shipment.Status == "assigned"))
            shipment.Status = "pod_uploaded";

        shipment.UpdatedAt = DateTime.UtcNow;

        await eventService.LogAsync(
            shipment.Id, ShipmentEventType.DocumentUploaded, ctx.MemberId, ctx.Role,
            new
            {
                doc_id = document.Id,
                doc_type = document.DocType,
                filename = document.OriginalFileName,
                file_size_bytes = document.FileSizeBytes
            });

        if (previousStatus != shipment.Status)
        {
            await eventService.LogAsync(
                shipment.Id, ShipmentEventType.StatusChanged, ctx.MemberId, ctx.Role,
                new { from = previousStatus, to = shipment.Status, trigger = "pod_uploaded" });
        }
        await appDb.SaveChangesAsync();

        return Created("", new
        {
            id = document.Id,
            doc_type = document.DocType,
            original_filename = document.OriginalFileName,
            file_size_bytes = document.FileSizeBytes,
            mime_type = document.MimeType,
            uploader_role = document.UploaderRole,
            created_at = document.CreatedAt
        });
    }

    [HttpGet]
    public async Task<IActionResult> ListDocuments(Guid shipmentId)
    {
        var ctx = User.ToShipmentAccessContext();

        var shipment = await appDb.Shipments.FirstOrDefaultAsync(s => s.Id == shipmentId && s.OrganisationId == ctx.OrgId);
        if (shipment == null) return NotFound(new { error = "SHIPMENT_NOT_FOUND" });

        // Document listing follows the same visibility rules as the shipment itself.
        if (!policy.CanView(ctx, shipment))
            return StatusCode(403, new { error = "FORBIDDEN" });

        var documents = await appDb.ShipmentDocuments
            .Where(d => d.ShipmentId == shipmentId && !d.IsDeleted)
            .Select(d => new
            {
                id = d.Id,
                doc_type = d.DocType,
                original_filename = d.OriginalFileName,
                file_size_bytes = d.FileSizeBytes,
                mime_type = d.MimeType,
                uploader_role = d.UploaderRole,
                view_url = storageService.GetPresignedUrl(d.StorageKey, 60),
                created_at = d.CreatedAt
            })
            .ToListAsync();
        return Ok(documents);
    }

    [HttpDelete("{docId}")]
    public async Task<IActionResult> DeleteDocument(Guid shipmentId, Guid docId)
    {
        var ctx = User.ToShipmentAccessContext();

        var shipment = await appDb.Shipments.FirstOrDefaultAsync(s => s.Id == shipmentId && s.OrganisationId == ctx.OrgId);
        if (shipment == null) return NotFound(new { error = "SHIPMENT_NOT_FOUND" });

        if (!policy.CanDeleteDocument(ctx, shipment))
            return StatusCode(403, new { error = "FORBIDDEN" });

        var document = await appDb.ShipmentDocuments.FirstOrDefaultAsync(d => d.Id == docId && d.ShipmentId == shipmentId && !d.IsDeleted);
        if (document == null) return NotFound(new { error = "DOCUMENT_NOT_FOUND" });

        //soft delete - don't remove from storage
        document.IsDeleted = true;

        await eventService.LogAsync(
            shipment.Id, ShipmentEventType.DocumentDeleted, ctx.MemberId, ctx.Role,
            new { doc_id = document.Id, doc_type = document.DocType });
        await appDb.SaveChangesAsync();

        return Ok(new { message = "Document deleted" });
    }
}
