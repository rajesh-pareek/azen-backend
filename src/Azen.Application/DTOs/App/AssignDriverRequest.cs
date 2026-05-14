namespace Azen.Application.DTOs.App;

public class AssignDriverRequest
{
    public Guid? MemberId { get; set; }
    public string? Name { get; set; }
    public string? Phone { get; set; }
    public string? VehicleNumber { get; set; }
}