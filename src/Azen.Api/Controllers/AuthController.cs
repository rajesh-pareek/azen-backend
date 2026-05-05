using System.Security.Cryptography;
using System.Text;
using Azen.Application.DTOs;
using Azen.Application.DTOs.Auth;
using Azen.Application.Interfaces;
using Azen.Domain.Entities.Auth;
using Azen.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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
    private readonly AppDbContext appDb;

    public AuthController(IOtpService otpService, IJwtService jwtService, AuthDbContext authDb, IConfiguration config, AppDbContext appDb)
    {
        this.otpService = otpService;
        this.jwtService = jwtService;
        this.authDb = authDb;
        this.config = config;
        this.appDb = appDb;
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

        var memberships = await appDb.OrganisationMembers
        .Where(m => m.UserId == user.Id)
        .Join(appDb.Organisations,
        m => m.OrganisationId,
        o => o.Id,
        (m, o) => new { org_id = o.Id, name = o.Name, slug = o.Slug, role = m.Role })
        .ToListAsync();

        return Ok(new
        {
            auth_code = authCode,
            user = new { Id = user.Id, name = user.Name, email = user.Email },
            organisations = memberships

        });
    }

    private async Task<object> GenerateTokenPair(User user, Guid orgId, Guid memberId, string role, string subRole)
    {
        var accessToken = jwtService.GenerateAccessToken(user.Id, orgId, memberId, role, subRole);
        var refreshToken = jwtService.GenerateRefreshToken();

        var refreshTokenHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));

        var refreshTokenEntity = new RefreshToken
        {
            UserId = user.Id,
            OrgId = orgId,
            TokenHash = refreshTokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(int.Parse(config["Jwt:RefreshTokenExpiryDays"]!))
        };

        authDb.RefreshTokens.Add(refreshTokenEntity);
        await authDb.SaveChangesAsync();
        return new
        {
            access_token = accessToken,
            refresh_token = refreshToken,
            expires_in = 900,
        };
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
        /* var accessToken = jwtService.GenerateAccessToken(
            userId: user.Id,
            orgId: request.OrgId,
            memberId: Guid.Empty, //placeholder until appdb
            role: "transporter",
            subRole: "member"
        ); */

        /* var refreshToken = jwtService.GenerateRefreshToken();
        //Hash and store refresh token
        var refreshTokenHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));

        var refreshTokenEntity = new RefreshToken
        {
            UserId = user.Id,
            OrgId = request.OrgId,
            TokenHash = refreshTokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(int.Parse(config["Jwt:RefreshTokenExpiryDays"]!)),
        }; */

        /*  authDb.RefreshTokens.Add(refreshTokenEntity);
         await authDb.SaveChangesAsync(); */
        var member = await appDb.OrganisationMembers.FirstOrDefaultAsync(m => m.UserId == user.Id && m.OrganisationId == request.OrgId && m.IsActive);
        if (member == null)
        {
            return StatusCode(403, new { error = "NOT_A_MEMBER", messsage = "You are not a member of this organisation" });
        }
        var tokens = await GenerateTokenPair(user, request.OrgId, member.Id, member.Role, member.SubRole);
        return Ok(tokens);
    }

    [HttpPost("token/refresh")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest refreshTokenRequest)
    {
        var refreshTokenHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(refreshTokenRequest.RefreshToken)));

        var existingToken = await authDb.RefreshTokens.FirstOrDefaultAsync(r => r.TokenHash == refreshTokenHash && r.RevokedAt == null && r.ExpiresAt > DateTime.UtcNow);
        if (existingToken == null) return Unauthorized(new { error = "INVALID_REFRESH_TOKEN" });
        existingToken.RevokedAt = DateTime.UtcNow;

        var user = await authDb.Users.FindAsync(existingToken.UserId);
        if (user == null) return NotFound(new { error = "USER_NOT_FOUND" });

        //Look up membership in appDb

        var member = await appDb.OrganisationMembers.FirstOrDefaultAsync(m => m.UserId == user.Id && m.OrganisationId == existingToken.OrgId && m.IsActive);
        if (member == null)
        {
            return StatusCode(403, new { error = "NOT_A_MEMBER", messsage = "You are not a member of this organisation" });
        }

        var tokens = await GenerateTokenPair(user, existingToken.OrgId, member.Id, member.Role, member.SubRole);

        return Ok(tokens);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request)
    {
        var tokenHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(request.RefreshToken)));
        var existingToken = await authDb.RefreshTokens.FirstOrDefaultAsync(r => r.TokenHash == tokenHash && r.RevokedAt == null);

        if (existingToken == null) return Unauthorized(new { error = "INVALID_REFRESH_TOKEN" });

        existingToken.RevokedAt = DateTime.UtcNow;
        await authDb.SaveChangesAsync();

        return Ok(new { message = "Logged Out" });
    }

}