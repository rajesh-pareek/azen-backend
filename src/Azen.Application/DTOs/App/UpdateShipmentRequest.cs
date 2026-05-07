namespace Azen.Application.DTOs.App;

public class UpdateShipmentRequest
{
    public string? ConsignorName { get; set; }
    public string? ConsignorPhone { get; set; }
    public string? ConsigneeName { get; set; }
    public string? ConsigneePhone { get; set; }
    public string? GoodsDescription { get; set; }
    public string? VehicleNumber { get; set; }
    public string? Notes { get; set; }
}