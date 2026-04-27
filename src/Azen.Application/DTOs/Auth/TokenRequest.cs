namespace Azen.Application.DTOs.Auth;

public class TokenRequest
{
    public Guid OrgId { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string AuthCode { get; set; } = string.Empty;

}