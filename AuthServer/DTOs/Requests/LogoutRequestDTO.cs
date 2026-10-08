using System.ComponentModel.DataAnnotations;

namespace AuthServer.DTOs.Requests
{
    public class LogoutRequestDTO
    {
        [Required(ErrorMessage = "ClientId is required.")]
        public string ClientId { get; set; } = default!;

        [Required(ErrorMessage = "ClientSecret is required.")]
        public string ClientSecret { get; set; } = default!;

        [Required(ErrorMessage = "RefreshToken is required.")]
        public string RefreshToken { get; set; } = default!;

        [Required(ErrorMessage = "DeviceId is required.")]
        public string DeviceId { get; set; } = default!;
    }

}
