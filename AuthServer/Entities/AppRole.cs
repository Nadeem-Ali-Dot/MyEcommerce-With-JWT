using System.ComponentModel.DataAnnotations;

namespace AuthServer.Entities
{
    public class AppRole
    {
        public Guid Id { get; set; }
        [MaxLength(50)]
        public string Name { get; set; } = default!;
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public ICollection<AppUserRole> UserRoles { get; set; } = new List<AppUserRole>();

    }
}
