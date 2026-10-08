using System.ComponentModel.DataAnnotations;

namespace AuthServer.DTOs.Requests
{
    public class RegisterRequestDTO
    {
        [Required(ErrorMessage = "FirstName is required.")]
        [StringLength(100, ErrorMessage = "FirstName cannot exceed 100 characters.")]
        public string FirstName { get; set; } = default!;

        [Required(ErrorMessage = "LastName is required.")]
        [StringLength(100, ErrorMessage = "LastName cannot exceed 100 characters.")]
        public string LastName { get; set; } = default!;

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Please enter a valid Email address.")]
        public string Email { get; set; } = default!;

        [Required(ErrorMessage = "Password is required.")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be between 6 and 100 characters.")]
        public string Password { get; set; } = default!;

        [Compare(nameof(Password), ErrorMessage = "ConfirmPassword must match Password.")]
        public string? ConfirmPassword { get; set; }

        [StringLength(20, ErrorMessage = "PhoneNumber cannot exceed 20 characters.")]
        public string? PhoneNumber { get; set; }
    }

}
