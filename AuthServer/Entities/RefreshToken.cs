using System.ComponentModel.DataAnnotations;

namespace AuthServer.Entities
{
    public class RefreshToken
    {
        public Guid Id { get; set; }
        public Guid SessionId { get; set; }
        public UserSession Session { get; set; } = default!;

        // Store only hashed token (never store raw refresh token)
        [MaxLength(200)]
        public string TokenHash { get; set; } = default!;

        // Rotation tree
        public Guid? ParentTokenId { get; set; }
        public RefreshToken? ParentToken { get; set; }

        public Guid? ReplacedByTokenId { get; set; }
        public RefreshToken? ReplacedByToken { get; set; }

        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
        public DateTime ExpiresUtc { get; set; }
        public DateTime? RevokedUtc { get; set; }
        [MaxLength(200)]
        public string? RevokedReason { get; set; }

        public bool IsExpired => DateTime.UtcNow >= ExpiresUtc;
        public bool IsRevoked => RevokedUtc != null;
        public bool IsActive => !IsExpired && !IsRevoked;

    }
}
