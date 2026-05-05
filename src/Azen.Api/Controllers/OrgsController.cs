using System.Security.Cryptography;
using System.Text;
using Azen.Application.DTOs.App;
using Azen.Application.DTOs.Auth;
using Azen.Application.Interfaces;
using Azen.Domain.Entities.App;
using Azen.Domain.Entities.Auth;
using Azen.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration.UserSecrets;

namespace Azen.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class OrgsController : ControllerBase
{
    private readonly IOtpService otpService;
    private readonly IJwtService jwtService;
    private readonly AuthDbContext authDb;
    private readonly AppDbContext appDb;
    private readonly IConfiguration config;

    public OrgsController(IOtpService otpService, IJwtService jwtService, AuthDbContext authDb, AppDbContext appDb, IConfiguration config)
    {
        this.otpService = otpService;
        this.jwtService = jwtService;
        this.authDb = authDb;
        this.appDb = appDb;
        this.config = config;
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrg([FromBody] CreateOrgRequest request)
    {
        //1. Validate auth code - proves user verified their phone
        var isValid = await otpService.ValidateAuthCodeAsync(request.Phone, request.AuthCode);
        if (!isValid)
        {
            return Unauthorized(new { error = "INVALID_AUTH_CODE" });
        }

        //2. Look up user in AuthDb
        var user = await authDb.Users.FirstOrDefaultAsync(u => u.Phone == request.Phone);
        if (user == null) return NotFound(new { error = "USER_NOT_FOUND" });

        //3. Check slug isn't already taken
        var slugExists = await appDb.Organisations.AnyAsync(o => o.Slug == request.Slug);
        if (slugExists)
        {
            return Conflict(new { error = "SLUG_TAKEN", message = "This organisation slug is already in use" });
        }

        //4. Create the organisation
        var org = new Organisation
        {
            Name = request.Name,
            Slug = request.Slug
        };

        appDb.Organisations.Add(org);
        await appDb.SaveChangesAsync();

        //5. Create organisation member - user becomes the transporter
        var member = new OrganisationMember
        {
            OrganisationId = org.Id,
            UserId = user.Id,
            Role = "transporter",
            SubRole = "member"
        };

        appDb.OrganisationMembers.Add(member);

        //6. Create ref sequence for this org (for auto-generating shipment numbers)
        var refSeq = new ShipmentRefSequence
        {
            OrganisationId = org.Id
        };

        await appDb.SaveChangesAsync();

        //7. Issue Jwt tokens
        var accessToken = jwtService.GenerateAccessToken(
            userId: user.Id,
            orgId: org.Id,
            memberId: member.Id,
            role: "transporter",
            subRole: "member"
        );

        var refreshToken = jwtService.GenerateRefreshToken();
        var refreshTokenHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));

        authDb.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            OrgId = org.Id,
            TokenHash = refreshTokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(int.Parse(config["Jwt:RefreshTokenExpiryDays"]!))
        });

        await authDb.SaveChangesAsync();

        //8. return org + tokens
        return Created("", new
        {
            org = new { id = org.Id, name = org.Name, slug = org.Slug },
            access_token = accessToken,
            refresh_token = refreshToken,
            expires_in = 900
        });
    }

    [HttpPost("current/members/invite")]
    public async Task<IActionResult> InviteMember([FromBody] InviteMemberRequest request)
    {
        //1. get org_id and role from jwt
        var org_id = Guid.Parse(User.FindFirst("org_id")!.Value);
        var callerRole = User.FindFirst("role")!.Value;

        //2. only tranporters can invite

        if (callerRole != "transporter")
            return StatusCode(403, new { error = "FORBIDDEN", message = "Only transporters ca invite members" });

        //3. validate role

        if (request.Role != "fleet_owner" && request.Role != "driver")
        {
            return BadRequest(new { error = "INVALIID_ROLE", message = "Role must be fleet owner or driver " });
        }
        //4. check authDb for existing user - create if not exists
        var user = await authDb.Users.FirstOrDefaultAsync(u => u.Phone == request.Phone);
        if (user == null)
        {
            user = new User
            {
                Phone = request.Phone,
                Name = request.Name
            };
            authDb.Users.Add(user);
            await authDb.SaveChangesAsync();
        }

        //5. check if already a member of this org
        var existingMember = await appDb.OrganisationMembers.FirstOrDefaultAsync(m => m.OrganisationId == org_id && m.UserId == user.Id);

        if (existingMember != null)
            return Conflict(new { error = "Already_MEMBER", message = "This user is already a member of your organisation." });

        //6. create membership

        var member = new OrganisationMember
        {
            OrganisationId = org_id,
            UserId = user.Id,
            Role = request.Role,
            SubRole = "member"
        };

        appDb.OrganisationMembers.Add(member);
        await appDb.SaveChangesAsync();

        //7. Return
        return Created("", new
        {
            member_id = member.Id,
            user_id = user.Id,
            role = member.Role,
            is_new_user = user.CreatedAt > DateTime.UtcNow.AddSeconds(-5)//rough check
        });
    }

    [Authorize]
    [HttpGet("current")]
    public async Task<IActionResult> GetCurrentOrg()
    {
        var orgId = Guid.Parse(User.FindFirst("org_id")!.Value);
        var org = await appDb.Organisations.FindAsync(orgId);

        if (org == null)
        {
            return NotFound(new { error = "ORG_NOT_FOUND" });
        }

        return Ok(new
        {
            id = org.Id,
            name = org.Name,
            slug = org.Slug,
            plan = org.Plan,
            is_active = org.IsActive,
            created_at = org.CreatedAt
        });
    }

    [Authorize]
    [HttpGet("current/members")]
    public async Task<IActionResult> GetMembers()
    {
        var orgId = Guid.Parse(User.FindFirst("org_id")!.Value);
        var callerRole = User.FindFirst("role")!.Value;

        //only transporters can see full member list
        if (callerRole != "transporter")
            return StatusCode(403, new { error = "FORBIDDEN" });

        var members = await appDb.OrganisationMembers
        .Where(m => m.OrganisationId == orgId)
        .Select(m => new
        {
            member_id = m.Id,
            user_id = m.UserId,
            role = m.Role,
            sub_role = m.SubRole,
            joinedAt = m.JoinedAt
        })
        .ToListAsync();

        return Ok(members);
    }

}