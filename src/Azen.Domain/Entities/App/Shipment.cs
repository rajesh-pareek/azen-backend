namespace Azen.Domain.Entities.App;

public class Shipment
{
    public Guid Id { get; set; }
    public Guid OrganisationId { get; set; }
    public string ReferenceNumber { get; set; } = string.Empty;

    //consignor/consignee (external)
    public string? ConsignorName { get; set; }
    public string? ConsignorPhone { get; set; }
    public string? ConsigneeName { get; set; }
    public string? ConsigneePhone { get; set; }

    //Shipment details
    public string? GoodsDescription { get; set; }
    public string? VehicleNumber { get; set; }

    //Staus
    public string Status { get; set; } = string.Empty; //"created" "assigned" "pod_uploaded" "shared"

    //fleet owner (null = not assigned)
    public Guid? FleetOwnerMemberId { get; set; }
    public string? FleetOwnerName { get; set; }
    public string? FleetOwnerPhone { get; set; }
    public bool FleetOwnerInSystem { get; set; }

    //Driver (Same Pattern)

    public Guid? DriverMemberId { get; set; }
    public string? DriverName { get; set; }
    public string? DriverPhone { get; set; }
    public bool DriverInSystem { get; set; }

    //MetaData
    public string? Notes { get; set; }

    //Audit

    public Guid CreatedByMemberId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    //Navigations
    public Organisation Organisation { get; set; } = null!;
    public OrganisationMember? FleetOwnerMember { get; set; }
    public OrganisationMember? DriverMember { get; set; }
    public OrganisationMember CreatedByMember { get; set; } = null!;

}
