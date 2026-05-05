using Azen.Application.DTOs.Auth;
using Azen.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;

namespace Azen.Api.Controllers;

public class UsersController : ControllerBase
{
    private readonly AuthDbContext authDb;

    public UsersController(AuthDbContext authDb)
    {
        this.authDb = authDb;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMe()
    {

        var userId = Guid.Parse(User.FindFirst("sub")!.Value);
        var user = await authDb.Users.FindAsync(userId);

        if (user == null)
        {
            return NotFound(new { error = "USER+NOT_FOUND" });
        }
        return Ok(new
        {
            id = user.Id,
            name = user.Name,
            phone = user.Phone,
            email = user.Email
        });
    }

    [HttpPatch("me")]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateMeRequest request)
    {
        var userId = Guid.Parse(User.FindFirst("sub")!.Value);
        var user = await authDb.Users.FindAsync(userId);

        if (user == null)
            return NotFound(new { error = "USER_NOT_FOUND" });

        if (request.Name != null)
        {
            user.Name = request.Name;
        }

        user.UpdatedAt = DateTime.UtcNow;
        await authDb.SaveChangesAsync();

        return Ok(new
        {
            id = user.Id,
            phone = user.Phone,
            name = user.Name,
            email = user.Email
        });

    }
}