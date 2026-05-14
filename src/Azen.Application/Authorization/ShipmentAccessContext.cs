namespace Azen.Application.Authorization;

/// <summary>
/// Caller's identity for ABAC policy evaluation. Built once per request from JWT claims.
/// Immutable - never mutate; if you need a different context, build a new one.
/// </summary>
public sealed record ShipmentAccessContext(
    Guid UserId,
    Guid OrgId,
    Guid MemberId,
    string Role,      // "transporter" | "fleet_owner" | "driver"
    string SubRole    // "member" | "manager"
);
