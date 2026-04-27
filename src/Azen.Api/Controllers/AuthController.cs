using System.Security.Cryptography;
using System.Text;
using Azen.Application.DTOs.Auth;
using Azen.Application.Interfaces;
using Azen.Domain.Entities.Auth;
using Azen.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.EntityFrameworkCore;
[ApiController]
[Route("api/v1/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IOtpService otpService;
    private readonly IJwtService jwtService;
    private readonly AuthDbContext authDb;
    private readonly IConfiguration config;

    public AuthController(IOtpService otpService, IJwtService jwtService, AuthDbContext authDb, IConfiguration config)
    {
        this.otpService = otpService;
        this.jwtService = jwtService;
        this.authDb = authDb;
        this.config = config;
    }

    [HttpPost("otp/send")]
    public async Task<IActionResult> SendOtp([FromBody] SendOtpRequest request)
    {
        await otpService.SendOtpAsync(request.Phone);
        return Ok();
    }

    [HttpPost("otp/verify")]
    public async Task<IActionResult> VerifyOtpAsync([FromBody] VerifyOtpRequest request)
    {
        var authCode = await otpService.VerifyOtpAsync(request.Phone, request.Otp);
        if (authCode == null) return Unauthorized(new { error = "INVALID_OTP" });

        var user = authDb.Users.FirstOrDefault(u => u.Phone == request.Phone);
        if (user == null)
        {
            user = new User
            {
                Phone = request.Phone
            };
            authDb.Users.Add(user);
            await authDb.SaveChangesAsync();
        }

        return Ok(new
        {
            auth_code = authCode,
            user = new { Id = user.Id, name = user.Name, email = user.Email },
            organisations = new List<object>() //empty for now since AppDb will be setup later });

        });
    }

    [HttpPost("token/issue")]
    public async Task<IActionResult> IssueToken([FromBody] TokenRequest request)
    {
        var isValid = await otpService.ValidateAuthCodeAsync(request.Phone, request.AuthCode);
        if (!isValid)
        {
            return Unauthorized(new { error = "INVALID_AUTH_CODE" });
        }

        var user = await authDb.Users.FirstOrDefaultAsync(u => u.Phone == request.Phone);

        if (user == null) return NotFound(new { error = "USER_NOT_FOUND" });

        // Generate Access Tokens
        //To Do: Replace Dummy Values With Real Org Lookup once AppDb is setup
        var accessToken = jwtService.GenerateAccessToken(
            userId: user.Id,
            orgId: request.OrgId,
            memberId: Guid.Empty, //placeholder until appdb
            role: "transporter",
            subRole: "member"
        );

        var refreshToken = jwtService.GenerateRefreshToken();
        //Hash and store refresh token
        var refreshTokenHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));

        var refreshTokenEntity = new RefreshToken
        {
            UserId = user.Id,
            OrgId = request.OrgId,
            TokenHash = refreshTokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(int.Parse(config["Jwt:RefreshTokenExpiryDays"]!)),
        };

        authDb.RefreshTokens.Add(refreshTokenEntity);
        await authDb.SaveChangesAsync();

        return Ok(
            new
            {
                access_token = accessToken,
                refresh_token = refreshToken,
                expires_in = 900,
            }
        );
    }
}