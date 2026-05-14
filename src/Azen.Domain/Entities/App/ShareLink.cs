namespace Azen.Domain.Entities.App;

public class ShareLink
{
    public Guid Id { get; set; }
    public string Token { get; set; } = string.Empty;
    public Guid ShipmentId { get; set; }
    public Guid CreatedByMemberId { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
    public string VisibleDocTypes { get; set; } = string.Empty;
    public int AccessCount { get; set; }
    public DateTime? LastAccessAt { get; set; }
    public DateTime CreatedAt { get; set; }

    //Navigations
    public Shipment Shipment { get; set; } = null!;
    public OrganisationMember CreatedByMember { get; set; } = null!;
}
