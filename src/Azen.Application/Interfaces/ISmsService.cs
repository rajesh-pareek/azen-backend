namespace Azen.Application.Interfaces;

public interface ISmsService
{
    Task SendOtpAsync(string phone, string otp);
}