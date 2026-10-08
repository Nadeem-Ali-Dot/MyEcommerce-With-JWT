using AuthServer.Entities;
using JwtAuth.Shared.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace AuthServer.Security
{
    // Service responsible for creating JWT access tokens.
    // Access tokens are:
    // - Short-lived
    // - Stateless (not stored server-side)
    // - Used by Resource Servers to authenticate and authorize requests
    public class JwtTokenService : IJwtTokenService
    {
        // Provides access to JWT configuration (Issuer, Audience, SigningKey, Expiry)
        private readonly JwtSettings _jwt;

        // IConfiguration is injected so token settings can be changed
        // without modifying code
        public JwtTokenService(IOptions<JwtSettings> jwtOptions)
        {
            _jwt = jwtOptions.Value;
        }

        // Creates a signed JWT access token for an authenticated user.
        // Parameters:
        // - user       : Authenticated user entity
        // - roles      : User roles for role-based authorization
        // - clientId   : Calling client application identifier
        // - sessionId  : Server-side session identifier (for logout / tracking)
        public (string token, DateTime expiresUtc) CreateAccessToken(
            AppUser user,
            IEnumerable<string> roles,
            string clientId,
            Guid sessionId)
        {
            // Read JWT configuration values
            var issuer = _jwt.Issuer!;
            var audience = _jwt.Audience!;
            var key = _jwt.SigningKey!;
            var minutes = _jwt.AccessTokenMinutes!;

            // Calculate token expiration time (short-lived by design)
            var expires = DateTime.UtcNow.AddMinutes(minutes);

            // Create a user-friendly full name claim (optional convenience claim)
            var fullName = $"{user.FirstName} {user.LastName}".Trim();

            // Build JWT Claims (Payload)
            var claims = new List<Claim>
            {
                // Primary user identifier used by ASP.NET Core authorization
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),

                // Standard JWT subject claim (user identity)
                new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),

                // User email claim (useful for logging and display)
                new(JwtRegisteredClaimNames.Email, user.Email),

                // Unique token identifier (prevents replay attacks)
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),

                // Identifies which client application requested this token
                new("client_id", clientId),

                // Session ID allows token-to-session binding
                // (used for logout and refresh-token validation)
                new("sid", sessionId.ToString()),

                // Optional custom claim for UI or audit purposes
                new("full_name", fullName),

                // Issued-at time (Unix timestamp)
                new(JwtRegisteredClaimNames.Iat,
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString())
            };

            // Add role claims so Resource Servers can enforce
            // [Authorize(Roles = "...")] policies
            foreach (var role in roles)
                claims.Add(new Claim(ClaimTypes.Role, role));

            // Create the key object used for signing.
            // SymmetricSecurityKey means: the same shared secret is used to SIGN and to VALIDATE the token.
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));

            // SigningCredentials tells ASP.NET Core how and with which key the JWT should be digitally signed.
            // A JWT has three parts: Header.Payload.Signature
            // The Signature is created using:
            //    A Secret Key(or Private Key)
            //    A Cryptographic Algorithm
            //That combination is wrapped inside SigningCredentials.
            var creds = new SigningCredentials(
                securityKey,
                SecurityAlgorithms.HmacSha256
            );

            // JwtSecurityToken represents the in-memory model of a JWT before it is serialized into a string.It represents:
            //   Header
            //   Payload(claims)
            //   Signature(not yet serialized)
            //Think of it like: A JWT document prepared but not yet printed.
            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: expires,
                signingCredentials: creds
            );

            // JwtSecurityTokenHandler is a Utility Class that knows how to:
            //    Create JWT strings
            //    Validate JWT tokens
            //    Read JWT tokens
            //    Serialize / Deserialize JWT
            //It acts as the JWT Encoder / Decoder.
            var jwtSecurityTokenHandler = new JwtSecurityTokenHandler();

            //This method:
            //  1.Takes the JWT object
            //  2.Creates header JSON
            //  3.Creates payload JSON
            //  4.Base64 - URL encodes both
            //  5.Signs them using SigningCredentials
            //  6.Builds the final compact JWT string: header.payload.signature
            var jwt = jwtSecurityTokenHandler.WriteToken(token);

            // Return the token string along with its expiry timestamp.
            return (jwt, expires);
        }
    }

}
