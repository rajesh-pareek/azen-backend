namespace Azen.Domain.Entities.App;

public class ShipmentRefSequence
{
    public Guid OrganisationId { get; set; }
    public int LastSeq { get; set; }

    //Navigatiion
    public Organisation Organisation { get; set; } = null!;
}