namespace Azen.Application.DTOs.App;

public class InviteMemberRequest
{
    public string Phone { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty; //fleet-owner or driver
}