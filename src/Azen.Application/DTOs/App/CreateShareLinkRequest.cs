namespace Azen.Application.DTOs.App;

public class CreateShareLinkRequest
{
    public List<string> VisibleDocTypes { get; set; } = new();
    public int? ExpiresInDays { get; set; }
}