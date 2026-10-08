using AuthServer.Entities;

namespace AuthServer.Security
{
    public interface IJwtTokenService
    {
        (string token, DateTime expiresUtc) CreateAccessToken(AppUser user, IEnumerable<string> roles, string clientId, Guid sessionId);
    }

}
