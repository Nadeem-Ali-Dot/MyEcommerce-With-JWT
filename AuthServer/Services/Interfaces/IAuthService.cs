using AuthServer.DTOs.Requests;
using AuthServer.DTOs.Responses;

namespace AuthServer.Services.Interfaces
{
    public interface IAuthService
    {
        Task<TokenResponseDTO> LoginAsync(LoginRequestDTO dto, HttpContext httpContext);
        Task<TokenResponseDTO> RefreshAsync(RefreshRequestDTO dto, HttpContext httpContext);
        Task LogoutAsync(LogoutRequestDTO dto);
        Task LogoutAllAsync(Guid userId);
        Task RegisterAsync(RegisterRequestDTO dto);
        Task<List<SessionResponseDTO>> GetMySessionsAsync(Guid userId);
        Task RevokeSessionAsync(Guid userId, Guid sessionId, string reason);
    }

}
