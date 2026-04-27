namespace Azen.Application.Interfaces;

public interface IOtpService
{
    public Task<bool> SendOtpAsync(string phone);
    public Task<string?> VerifyOtpAsync(string phone, string otp);
    public Task<bool> ValidateAuthCodeAsync(string phone, string authCode);
}