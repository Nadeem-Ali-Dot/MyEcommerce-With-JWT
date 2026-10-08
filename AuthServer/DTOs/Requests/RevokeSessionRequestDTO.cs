using System.ComponentModel.DataAnnotations;

namespace AuthServer.DTOs.Requests
{
    public class RevokeSessionRequestDTO
    {
        [Required(ErrorMessage = "SessionId is required.")]
        public Guid SessionId { get; set; } = default!;

        [Required(ErrorMessage = "Reason is required.")]
        [StringLength(200, ErrorMessage = "Reason cannot exceed 200 characters.")]
        public string Reason { get; set; } = default!;
    }

}
