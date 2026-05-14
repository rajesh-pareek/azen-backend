namespace Azen.Application.DTOs.App;

public class AssignFleetOwnerRequest
{
    public Guid? MemberId { get; set; } //if in system
    public string? Name { get; set; } //if external
    public string? Phone { get; set; }
}