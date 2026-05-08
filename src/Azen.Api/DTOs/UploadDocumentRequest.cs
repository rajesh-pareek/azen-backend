
namespace Azen.Api.DTOs;

public class UploadDocumentRequest
{
    public IFormFile File { get; set; } = null!;
    public string DocType { get; set; } = string.Empty;
}