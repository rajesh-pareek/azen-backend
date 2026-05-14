namespace Azen.Application.DTOs.App;

public class CreateShipmentRequest
{
    public string? ReferenceNumber { get; set; }
    public string? ConsignorName { get; set; }
    public string? ConsignorPhone { get; set; }

    public string? ConsigneeName { get; set; }
    public string? ConsigneePhone { get; set; }
    public string? GoodsDescription { get; set; }
    public string? Notes { get; set; }
}