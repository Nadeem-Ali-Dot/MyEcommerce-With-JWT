using System.ComponentModel.DataAnnotations;

namespace AuthServer.Entities
{
    public class AppUser
    {
        public Guid Id { get; set; }
        [MaxLength(100)]
        public string FirstName { get; set; } = default!;
        [MaxLength(100)]
        public string LastName { get; set; } = default!;
        public string Email { get; set; } = default!;
        [MaxLength(20)]
        public string? PhoneNumber { get; set; }
        [MaxLength(300)]
        public string PasswordHash { get; set; } = default!;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        public ICollection<AppUserRole> UserRoles { get; set; } = new List<AppUserRole>();
        public ICollection<UserSession> Sessions { get; set; } = new List<UserSession>();

    }
}
