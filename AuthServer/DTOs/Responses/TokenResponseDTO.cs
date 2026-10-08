namespace AuthServer.DTOs.Responses
{
    public class TokenResponseDTO
    {
        public string AccessToken { get; set; } = default!;
        public DateTime AccessTokenExpiresUtc { get; set; }

        public string RefreshToken { get; set; } = default!;
        public DateTime RefreshTokenExpiresUtc { get; set; }

        public Guid SessionId { get; set; }
    }

}
