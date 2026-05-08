namespace Azen.Application.Interfaces;

public interface IStorageService
{
    Task<string> UploadAsync(string key, Stream fileStream, string contentType);
    Task DeleteAsync(string key);
    string GetPresignedUrl(string key, int expiryMinutes = 60);
}
