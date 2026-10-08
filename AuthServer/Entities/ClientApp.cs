using System.ComponentModel.DataAnnotations;

namespace AuthServer.Entities
{
    public class ClientApp
    {
        public Guid Id { get; set; }
        [MaxLength(100)]
        public string ClientId { get; set; } = default!;     // Public Identifier
        [MaxLength(200)]
        public string Name { get; set; } = default!;
        [MaxLength(200)]
        public string ClientSecretHash { get; set; } = default!;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    }

}
