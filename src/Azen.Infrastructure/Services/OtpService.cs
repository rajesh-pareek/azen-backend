using Azen.Application.Interfaces;
using Azen.Domain.Entities.Auth;
using Azen.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Azen.Infrastructure.Services;

public class OtpService : IOtpService
{
    private readonly AuthDbContext _authDb;
    private readonly ISmsService _smsService;

    public OtpService(AuthDbContext authDb, ISmsService smsService)
    {
        _authDb = authDb;
        _smsService = smsService;
    }
    public async Task<bool> SendOtpAsync(string phone)
    {
        var otp = Random.Shared.Next(100000, 999999).ToString();

        var otpHash = BCrypt.Net.BCrypt.HashPassword(otp);

        var otpRequest = new OtpRequest
        {
            Phone = phone,
            OtpHash = otpHash,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10)
        };

        _authDb.OtpRequests.Add(otpRequest);
        await _authDb.SaveChangesAsync();

        await _smsService.SendOtpAsync(phone, otp);
        return true;
    }

    public async Task<string?> VerifyOtpAsync(string phone, string otp)
    {
        var otpRequest = await _authDb.OtpRequests
            .Where(o => o.Phone == phone && !o.IsUsed && o.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync();

        if (otpRequest == null) return null;


        if (otpRequest.AttemptCount >= 5) return null;

        if (!BCrypt.Net.BCrypt.Verify(otp, otpRequest.OtpHash))
        {
            otpRequest.AttemptCount++;
            await _authDb.SaveChangesAsync();
            return null;
        }

        var authCode = Guid.NewGuid().ToString();
        otpRequest.AuthCodeHash = BCrypt.Net.BCrypt.HashPassword(authCode);
        otpRequest.AuthCodeExpiresAt = DateTime.UtcNow.AddMinutes(5);
        await _authDb.SaveChangesAsync();

        return authCode;
    }

    public async Task<bool> ValidateAuthCodeAsync(string phone, string authCode)
    {
        var otpRequest = await _authDb.OtpRequests
            .Where(o => o.Phone == phone
            && !o.AuthCodeUsed && o.AuthCodeHash != null && o.AuthCodeExpiresAt != null && o.AuthCodeExpiresAt > DateTime.UtcNow
            )
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync();


        if (otpRequest == null || otpRequest.AuthCodeHash == null) return false;

        if (!BCrypt.Net.BCrypt.Verify(authCode, otpRequest.AuthCodeHash)) return false;

        otpRequest.AuthCodeUsed = true;
        await _authDb.SaveChangesAsync();

        return true;

    }

}