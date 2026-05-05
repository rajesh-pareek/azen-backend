namespace Azen.Domain.Entities.App;

public class Organisation
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;  //unique eg. "Anil logistics
    public string Slug { get; set; } = string.Empty;
    public string Plan { get; set; } = string.Empty; // "mvp", "growth", "enterprise"
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
