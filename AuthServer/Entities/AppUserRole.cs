namespace AuthServer.Entities
{
    public class AppUserRole
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public AppUser User { get; set; } = default!;
        public Guid RoleId { get; set; }
        public AppRole Role { get; set; } = default!;
        public Guid? AssignedByUserId { get; set; }
        public DateTime AssignedUtc { get; set; }
        public string? Notes { get; set; }

    }
}
