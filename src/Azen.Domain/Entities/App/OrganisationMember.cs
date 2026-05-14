namespace Azen.Domain.Entities.App;

public class OrganisationMember
{
    public Guid Id { get; set; }
    public Guid OrganisationId { get; set; }
    public Guid UserId { get; set; } // cross-db ref to AuthDb.users (no navigation)
    public string Role { get; set; } = string.Empty; //"transporter", "fleet-owner
    public string SubRole { get; set; } = string.Empty; //"member", "manager"
    public bool IsActive { get; set; } = true;
    public DateTime JoinedAt { get; set; }
    //Navigation

    public Organisation Organisation { get; set; } = null!;

}