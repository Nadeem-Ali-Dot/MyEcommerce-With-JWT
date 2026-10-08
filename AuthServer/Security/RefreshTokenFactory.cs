using JwtAuth.Shared.Options;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace AuthServer.Security
{
    // Generates secure refresh tokens and hashes them before storing in database.
    // This ensures refresh tokens remain protected even if database is compromised.
    public sealed class RefreshTokenService : IRefreshTokenService
    {
        private readonly RefreshTokenSettings _refreshSettings;

        // Read RefreshToken settings from appsettings.json using Options Pattern:
        // "RefreshToken": { "DaysToExpire": 14 }
        public RefreshTokenService(IOptions<RefreshTokenSettings> refreshOptions)
        {
            _refreshSettings = refreshOptions.Value;
        }

        // Generates a cryptographically strong random refresh token.
        // byteLength defines entropy (64 bytes is strong).
        public string GenerateRawToken(int byteLength = 64)
        {
            // Generate cryptographically secure random bytes
            var bytes = RandomNumberGenerator.GetBytes(byteLength);

            // Convert random bytes into URL-safe Base64 string
            return ToBase64Url(bytes);
        }

        // Hashes the raw refresh token before persisting in the database.
        // SHA-256 is used to ensure one-way irreversible hashing.
        public string HashToken(string rawToken)
        {
            // Convert token string to bytes
            var bytes = Encoding.UTF8.GetBytes(rawToken);

            // Generate SHA-256 hash of token
            var hash = SHA256.HashData(bytes);

            // Convert hash bytes to hexadecimal string for storage
            return Convert.ToHexString(hash);
        }

        // Real-time: Refresh token expiry should be decided using config.
        // Store only the expiry time in DB, not in the token itself.
        public DateTime GetExpiryUtc()
        {
            // Ensure config value is sensible (avoid negative/zero days)
            var days = _refreshSettings.DaysToExpire <= 0 ? 14 : _refreshSettings.DaysToExpire;

            return DateTime.UtcNow.AddDays(days);
        }

        // Converts byte array to Base64 URL-safe format
        // This avoids '+', '/', '=' which can break URLs and headers.
        private static string ToBase64Url(byte[] bytes)
        {
            return Convert.ToBase64String(bytes)
                .TrimEnd('=')       // Remove Base64 padding
                .Replace('+', '-')  // URL safe replacement
                .Replace('/', '_'); // URL safe replacement
        }
    }

}
