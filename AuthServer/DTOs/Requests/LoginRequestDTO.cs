using System.ComponentModel.DataAnnotations;

namespace AuthServer.DTOs.Requests
{
    public class LoginRequestDTO
    {
        //User: Email, Password,
        //Client Application (Web, Android, iOS): ClientId, ClientSecret, DeviceId
        [Required(ErrorMessage = "ClientId is required.")]
        [StringLength(100, ErrorMessage = "ClientId cannot exceed 100 characters.")]
        public string ClientId { get; set; } = default!;

        [Required(ErrorMessage = "ClientSecret is required.")]
        [StringLength(200, ErrorMessage = "ClientSecret cannot exceed 200 characters.")]
        public string ClientSecret { get; set; } = default!; //Not Hash

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Please enter a valid Email address.")]
        public string Email { get; set; } = default!;

        [Required(ErrorMessage = "Password is required.")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be between 6 and 100 characters.")]
        public string Password { get; set; } = default!; //Not Hash

        [Required(ErrorMessage = "DeviceId is required.")]
        [StringLength(100, ErrorMessage = "DeviceId cannot exceed 100 characters.")]
        public string DeviceId { get; set; } = default!;
    }

}
