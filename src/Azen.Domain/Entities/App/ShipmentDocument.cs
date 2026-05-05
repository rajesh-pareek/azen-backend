namespace Azen.Domain.Entities.App;

public class ShipmentDocument
{
    public Guid Id { get; set; }
    public Guid ShipmentId { get; set; }
    public string DocType { get; set; } = string.Empty; // "pod","LR", "invoice"
    public string StorageKey { get; set; } = string.Empty;

    public string OriginalFileName { get; set; } = string.Empty;

    public int FileSizeBytes { get; set; }
    public string MimeType { get; set; } = string.Empty;

    public Guid UploadedByMemberId { get; set; }
    public string UploaderRole { get; set; } = string.Empty;

    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }

    //Navigations
    public Shipment Shipment { get; set; } = null!;
    public OrganisationMember UploadedByMember { get; set; } = null!;
}