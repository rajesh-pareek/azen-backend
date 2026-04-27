namespace Azen.Application.Interfaces;

public interface IJwtService
{
    string GenerateAccessToken(Guid userId, Guid orgId, Guid memberId, string role, string subRole);
    string GenerateRefreshToken();
}