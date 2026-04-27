namespace Azen.Application.DTOs.Auth;

public class VerifyOtpRequest
{
    public string Phone { get; set; } = string.Empty;
    public string Otp { get; set; } = string.Empty;
}