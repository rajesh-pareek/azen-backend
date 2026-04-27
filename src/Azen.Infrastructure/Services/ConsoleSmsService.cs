using Azen.Application.Interfaces;

namespace Azen.Infrastructure.Services;

public class ConsoleSmsService : ISmsService
{
    public Task SendOtpAsync(string phone, string otp)
    {
        Console.WriteLine($"[Mock SMS] OTP for {phone} : {otp}");
        return Task.CompletedTask;
    }
}