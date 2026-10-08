using AuthServer.Common.Exceptions;
using AuthServer.Common.Results;
using AuthServer.DTOs.Requests;
using AuthServer.DTOs.Responses;
using AuthServer.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AuthServer.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IAuthService authService, ILogger<AuthController> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<string>>> Register([FromBody] RegisterRequestDTO dto)
        {
            // Do NOT log passwords
            _logger.LogInformation("Register request received. Email={Email}, FirstName={FirstName}, LastName={LastName}, Phone={Phone}",
                    dto.Email, dto.FirstName, dto.LastName, dto.PhoneNumber);

            await _authService.RegisterAsync(dto);

            _logger.LogInformation("Register completed. Email={Email}", dto.Email);

            return Ok(ApiResponse<string>.Success("User registered successfully."));
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<TokenResponseDTO>>> Login([FromBody] LoginRequestDTO dto)
        {
            _logger.LogInformation("Login request received. Email={Email}, ClientId={ClientId}, DeviceId={DeviceId}",
                dto.Email, dto.ClientId, dto.DeviceId);

            var result = await _authService.LoginAsync(dto, HttpContext);

            _logger.LogInformation("Login completed. Email={Email}, ClientId={ClientId}, SessionId={SessionId}",
                dto.Email, dto.ClientId, result.SessionId);

            return Ok(ApiResponse<TokenResponseDTO>.Success(result, "Login successful."));
        }

        [HttpPost("refresh")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<TokenResponseDTO>>> Refresh([FromBody] RefreshRequestDTO dto)
        {
            _logger.LogInformation("Refresh request received. ClientId={ClientId}, DeviceId={DeviceId}", dto.ClientId, dto.DeviceId);

            var result = await _authService.RefreshAsync(dto, HttpContext);

            _logger.LogInformation("Refresh completed. ClientId={ClientId}, SessionId={SessionId}", dto.ClientId, result.SessionId);

            return Ok(ApiResponse<TokenResponseDTO>.Success(result, "Token refreshed successfully."));
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<string>>> Logout([FromBody] LogoutRequestDTO dto)
        {
            var userId = GetUserId();

            _logger.LogInformation("Logout request received. UserId={UserId}, ClientId={ClientId}, DeviceId={DeviceId}",
                userId, dto.ClientId, dto.DeviceId);

            await _authService.LogoutAsync(dto);

            _logger.LogInformation("Logout completed. UserId={UserId}, ClientId={ClientId}, DeviceId={DeviceId}",
                userId, dto.ClientId, dto.DeviceId);

            return Ok(ApiResponse<string>.Success("Logged out from this device."));
        }

        [HttpPost("logout-all")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<string>>> LogoutAll()
        {
            var userId = GetUserId();

            _logger.LogInformation("LogoutAll request received. UserId={UserId}", userId);

            await _authService.LogoutAllAsync(userId);

            _logger.LogInformation("LogoutAll completed. UserId={UserId}", userId);

            return Ok(ApiResponse<string>.Success("Logged out from all devices."));
        }

        [HttpGet("sessions")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<List<SessionResponseDTO>>>> MySessions()
        {
            var userId = GetUserId();

            _logger.LogInformation("MySessions request received. UserId={UserId}", userId);

            var sessions = await _authService.GetMySessionsAsync(userId);

            _logger.LogInformation("MySessions completed. UserId={UserId}, Count={Count}", userId, sessions.Count);

            return Ok(ApiResponse<List<SessionResponseDTO>>.Success(sessions));
        }

        [HttpPost("sessions/revoke")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<string>>> RevokeSession([FromBody] RevokeSessionRequestDTO dto)
        {
            var userId = GetUserId();

            _logger.LogInformation(
                   "RevokeSession request received. UserId={UserId}, SessionId={SessionId}, Reason={Reason}",
                   userId, dto.SessionId, dto.Reason
               );

            await _authService.RevokeSessionAsync(userId, dto.SessionId, dto.Reason);

            _logger.LogInformation("RevokeSession completed. UserId={UserId}, SessionId={SessionId}", userId, dto.SessionId);

            return Ok(ApiResponse<string>.Success("Session revoked."));
        }

        private Guid GetUserId()
        {
            // Prefer NameIdentifier; fallback to "sub"
            var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");

            if (string.IsNullOrWhiteSpace(sub))
                throw new AppException("UserId claim missing.", 401);

            if (!Guid.TryParse(sub, out var userId))
                throw new AppException("Invalid UserId claim.", 401);

            return userId;
        }
    }

}
