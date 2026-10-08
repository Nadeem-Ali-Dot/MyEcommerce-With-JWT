using System.ComponentModel.DataAnnotations;

namespace AuthServer.Entities
{
    public class UserSession
    {

        public Guid Id { get; set; }

        public Guid UserId { get; set; }
        public AppUser User { get; set; } = default!;

        public Guid ClientAppId { get; set; }
        public ClientApp ClientApp { get; set; } = default!;

        // Sent by client (stable per device install / browser profile)
        [MaxLength(100)]
        public string DeviceId { get; set; } = default!;

        [MaxLength(500)]
        public string? UserAgent { get; set; }

        [MaxLength(100)]
        public string? IpAddress { get; set; }

        [MaxLength(200)]
        public string? LoginLocation { get; set; } // city/state/country

        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
        public DateTime LastSeenUtc { get; set; } = DateTime.UtcNow;
        public DateTime? RevokedUtc { get; set; }
        public bool IsActive => RevokedUtc == null;

        public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();

    }
}
