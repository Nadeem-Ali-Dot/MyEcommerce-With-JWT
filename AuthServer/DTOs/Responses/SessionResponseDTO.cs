namespace AuthServer.DTOs.Responses
{
    public class SessionResponseDTO
    {
        public Guid SessionId { get; set; }
        public string DeviceId { get; set; } = default!;
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
        public string? LoginLocation { get; set; }
        public DateTime LastSeenUtc { get; set; }
        public bool IsActive { get; set; }
    }

}
