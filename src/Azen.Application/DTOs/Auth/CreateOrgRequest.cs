namespace Azen.Application.DTOs.Auth;

public class CreateOrgRequest
{
    public string Phone { get; set; } = string.Empty;
    public string AuthCode { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
}