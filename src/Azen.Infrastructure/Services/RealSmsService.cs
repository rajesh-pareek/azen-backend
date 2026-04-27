using Azen.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Azen.Infrastructure.Services;

public class RealSmsService : ISmsService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;

    public RealSmsService(HttpClient http, IConfiguration configuration)
    {
        _http = http;
        _config = configuration;
    }

    public async Task SendOtpAsync(string phone, string otp)
    {
        Console.WriteLine($"[Real SMS] otp for {phone}: {otp}");
        var apiKey = _config["SmsProvider:ApiKey"];
        var url = $"https://2factor.in/API/V1/{apiKey}/SMS/{phone}/{otp}";
        var response = await _http.GetAsync(url);

        response.EnsureSuccessStatusCode();
    }
}