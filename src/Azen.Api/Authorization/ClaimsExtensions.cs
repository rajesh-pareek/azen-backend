using System.Security.Claims;
using Azen.Application.Authorization;

namespace Azen.Api.Authorization;

/// <summary>
/// Bridges ASP.NET's ClaimsPrincipal into the policy layer's ShipmentAccessContext.
/// Keeps controllers free of FindFirst("orgId")!.Value boilerplate.
/// </summary>
public static class ClaimsExtensions
{
    /// <summary>
    /// Build a ShipmentAccessContext from the current request's JWT claims.
    /// Throws if any required claim is missing - which means the JWT pipeline is
    /// misconfigured (this should never happen behind [Authorize]).
    /// </summary>
    public static ShipmentAccessContext ToShipmentAccessContext(this ClaimsPrincipal user)
    {
        var userId = Guid.Parse(user.FindFirst("sub")!.Value);
        var orgId = Guid.Parse(user.FindFirst("orgId")!.Value);
        var memberId = Guid.Parse(user.FindFirst("member_id")!.Value);
        var role = user.FindFirst("user_role")!.Value;
        var subRole = user.FindFirst("sub_role")?.Value ?? "member";

        return new ShipmentAccessContext(userId, orgId, memberId, role, subRole);
    }
}
