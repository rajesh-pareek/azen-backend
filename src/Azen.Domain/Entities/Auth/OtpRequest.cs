namespace Azen.Domain.Entities.Auth;

public class OtpRequest
{
    public Guid Id { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string OtpHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; } = false;
    public int AttemptCount { get; set; }

    // Auth Code Issued After Successful OTP verification
    public string AuthCodeHash { get; set; } = string.Empty;
    public DateTime AuthCodeExpiresAt { get; set; }
    public bool AuthCodeUsed { get; set; } = false;
    public DateTime CreatedAt { get; set; }
}