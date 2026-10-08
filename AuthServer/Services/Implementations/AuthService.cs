using AuthServer.Common.Exceptions;
using AuthServer.Data;
using AuthServer.DTOs.Requests;
using AuthServer.DTOs.Responses;
using AuthServer.Entities;
using AuthServer.Security;
using AuthServer.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace AuthServer.Services.Implementations
{
    // AuthService is the main service that handles:
    // - Register
    // - Login (create session + issue JWT + issue refresh token)
    // - Refresh (rotate refresh token + issue new JWT)
    // - Logout / LogoutAll (revoke session + revoke refresh tokens)
    // - Session listing and session revocation
    public class AuthService : IAuthService
    {
        // EF Core DbContext for reading/writing auth tables (Users, Roles, Sessions, RefreshTokens, etc.)
        private readonly AuthDbContext _dbContext;

        // Logger for audit/security logs (never log raw passwords or raw tokens)
        private readonly ILogger<AuthService> _logger;

        // Password hashing service (BCrypt) used during Register/Login verification
        private readonly IPasswordHasher _passwordHasher;

        // Service that creates JWT access tokens (adds claims + signs token)
        private readonly IJwtTokenService _jwtTokenService;

        // Service to generate refresh tokens and compute their hash for DB storage
        private readonly IRefreshTokenService _refreshTokenService;

        // Used to verify client secret (BCrypt verify against stored ClientSecretHash)
        private readonly IClientSecretHasher _clientSecretHasher;

        // HttpClient used for external calls (IP lookup and geo-location)
        private readonly HttpClient _httpClient;

        public AuthService(
            AuthDbContext dbContext,
            ILogger<AuthService> logger,
            IPasswordHasher passwordHasher,
            IJwtTokenService jwtTokenService,
            IRefreshTokenService refreshTokenService,
            IClientSecretHasher clientSecretHasher,
            IHttpClientFactory httpClientFactory)
        {
            _dbContext = dbContext;
            _logger = logger;
            _passwordHasher = passwordHasher;
            _jwtTokenService = jwtTokenService;
            _refreshTokenService = refreshTokenService;
            _clientSecretHasher = clientSecretHasher;

            // Prefer IHttpClientFactory to avoid socket exhaustion and manage DNS updates properly
            _httpClient = httpClientFactory.CreateClient();
        }

        public async Task RegisterAsync(RegisterRequestDTO dto)
        {
            try
            {
                // 1) Prevent duplicate registrations using the same email
                var emailExists = await _dbContext.Users.AnyAsync(x => x.Email == dto.Email);
                if (emailExists) throw new AppException("Email already exists.");

                // 2) Load the default role to be assigned during self-registration
                var defaultRole = await _dbContext.Roles.FirstOrDefaultAsync(r => r.Name == "Customer");
                if (defaultRole == null) throw new AppException("Default role not configured.", 500);

                // 3) Create the user record
                // Important: store only the password hash, never the raw password
                var user = new AppUser
                {
                    Id = Guid.NewGuid(),
                    FirstName = dto.FirstName,
                    LastName = dto.LastName,
                    Email = dto.Email,
                    PhoneNumber = dto.PhoneNumber,
                    PasswordHash = _passwordHasher.Hash(dto.Password),
                    // IsActive = true,
                    CreatedUtc = DateTime.UtcNow
                };

                _dbContext.Users.Add(user); //user => Entity State => Added
                user.IsActive = true;

                // 4) Assign default role to the user (Customer)
                var userDefaultRole = new AppUserRole
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    RoleId = defaultRole.Id,
                    AssignedByUserId = null,     // null indicates self-registration (not assigned by an admin)
                    AssignedUtc = DateTime.UtcNow,
                    Notes = "Self Registration"
                };
                _dbContext.UserRoles.Add(userDefaultRole); //userDefaultRole => Entity State => Added

                // 5) Persist changes
                await _dbContext.SaveChangesAsync(); //Insert Statement for both user and userDefaultRole entities

                // 6) Audit log (safe fields only)
                _logger.LogInformation("User registered successfully. UserId={UserId}, Email={Email}", user.Id, user.Email);
            }
            catch (AppException)
            {
                // AppException already has correct message/status code
                throw;
            }
            catch (Exception ex)
            {
                // Unexpected errors are logged and converted to a safe generic error message
                _logger.LogError(ex, "Error occurred while registering user. Email={Email}", dto.Email);
                throw new AppException("Unable to register user. Please try again.", 500);
            }
        }

        public async Task<TokenResponseDTO> LoginAsync(LoginRequestDTO dto, HttpContext httpContext)
        {
            try
            {
                // 1) Validate client app credentials (ClientId + ClientSecret)
                //    This ensures only trusted client applications can request tokens.
                var client = await ValidateClientCredentialsAsync(dto.ClientId, dto.ClientSecret);

                // 2) Load user + roles (roles are needed to create JWT claims)
                var user = await _dbContext.Users
                    .AsNoTracking()
                    .Include(u => u.UserRoles) //Eager Loading
                        .ThenInclude(ur => ur.Role) //Eager Loading
                    .FirstOrDefaultAsync(u => u.Email == dto.Email);

                // 3) Basic user validations
                if (user == null || !user.IsActive)
                    throw new AppException("Invalid credentials.");

                // 4) Verify password (BCrypt verify against stored hash)
                if (!_passwordHasher.Verify(dto.Password, user.PasswordHash))
                    throw new AppException("Invalid credentials.");

                // 5) Create a login session (one session per device)
                var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
                var userAgent = httpContext.Request.Headers.UserAgent.ToString();

                // Optional: resolve country/state/city based on IP (external call)
                (string? IpAddress, string location) = await GetLocationAsync(ipAddress);

                var session = new UserSession
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    ClientAppId = client.Id,
                    DeviceId = dto.DeviceId,
                    LoginLocation = location,
                    IpAddress = IpAddress,
                    UserAgent = userAgent,
                    CreatedUtc = DateTime.UtcNow,
                    LastSeenUtc = DateTime.UtcNow
                };

                _dbContext.UserSessions.Add(session);

                // 6) Create refresh token:
                //    - raw refresh token is returned to the client
                //    - only hashed refresh token is stored in DB (safer if DB is leaked)
                var rawRefreshToken = _refreshTokenService.GenerateRawToken();
                var refreshTokenHash = _refreshTokenService.HashToken(rawRefreshToken);
                var expiresAtUtc = _refreshTokenService.GetExpiryUtc();

                var refreshEntity = new RefreshToken
                {
                    Id = Guid.NewGuid(),
                    SessionId = session.Id,
                    TokenHash = refreshTokenHash,
                    CreatedUtc = DateTime.UtcNow,
                    ExpiresUtc = expiresAtUtc
                };

                _dbContext.RefreshTokens.Add(refreshEntity);

                // 7) Create JWT access token (short-lived) with role claims
                var roles = user.UserRoles.Select(x => x.Role.Name).ToList();
                var (jwt, jwtExp) = _jwtTokenService.CreateAccessToken(user, roles, dto.ClientId, session.Id);

                // 8) Persist session + refresh token
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation(
                    "Login success. ClientId={ClientId}, UserId={UserId}, SessionId={SessionId}, DeviceId={DeviceId}, Ip={Ip}",
                    dto.ClientId, user.Id, session.Id, dto.DeviceId, ipAddress);

                // 9) Return tokens to the client (access token + refresh token)
                return new TokenResponseDTO
                {
                    AccessToken = jwt,
                    AccessTokenExpiresUtc = jwtExp,
                    RefreshToken = rawRefreshToken,
                    RefreshTokenExpiresUtc = expiresAtUtc,
                    SessionId = session.Id
                };
            }
            catch (AppException) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login failed. Email={Email}, ClientId={ClientId}", dto.Email, dto.ClientId);
                throw new AppException("Unable to login. Please try again.", 500);
            }
        }

        public async Task<TokenResponseDTO> RefreshAsync(RefreshRequestDTO dto, HttpContext httpContext)
        {
            try
            {
                // 1) Validate client app first
                var client = await ValidateClientCredentialsAsync(dto.ClientId, dto.ClientSecret);

                // 2) Hash the incoming refresh token so we can find it in DB
                //    (DB stores only TokenHash, never the raw refresh token)
                var tokenHash = _refreshTokenService.HashToken(dto.RefreshToken);

                // 3) Start a transaction because refresh-token rotation must be atomic
                //    (revoke old token + create new token + update session)
                await using var tx = await _dbContext.Database.BeginTransactionAsync();

                // 4) Load refresh token along with session and user
                var existing = await _dbContext.RefreshTokens
                    .Include(rt => rt.Session)
                        .ThenInclude(s => s.User)
                    .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash);

                if (existing == null)
                    throw new AppException("Invalid refresh token.", 401);

                if (existing.Session == null)
                    throw new AppException("Invalid refresh token session.", 401);

                // 5) Ensure refresh token belongs to the same client app
                if (existing.Session.ClientAppId != client.Id)
                    throw new AppException("Refresh token does not belong to this client.", 401);

                // 6) Ensure session is still active and refresh token is tied to this device
                if (existing.Session.RevokedUtc != null)
                    throw new AppException("Session revoked. Please login again.", 401);

                if (!string.Equals(existing.Session.DeviceId, dto.DeviceId, StringComparison.Ordinal))
                    throw new AppException("Refresh token does not match this device.", 401);

                // 7) Validate user status
                if (existing.Session.User == null || !existing.Session.User.IsActive)
                    throw new AppException("User is inactive. Please contact support.", 401);

                // 8) Reuse detection:
                //    If a revoked token is used again, treat it as a possible stolen token.
                //    Action: revoke entire session + revoke remaining active refresh tokens.
                if (existing.IsRevoked)
                {
                    existing.Session.RevokedUtc = DateTime.UtcNow;

                    var activeSessionTokens = await _dbContext.RefreshTokens
                        .Where(x => x.SessionId == existing.SessionId && !x.IsRevoked)
                        .ToListAsync();

                    foreach (var t in activeSessionTokens)
                    {
                        t.RevokedUtc = DateTime.UtcNow;
                        t.RevokedReason = "ReuseDetected";
                    }

                    await _dbContext.SaveChangesAsync();
                    await tx.CommitAsync();

                    _logger.LogWarning(
                        "Refresh token reuse detected. Session revoked. SessionId={SessionId}, ClientId={ClientId}, DeviceId={DeviceId}",
                        existing.SessionId, dto.ClientId, dto.DeviceId
                    );

                    throw new AppException("Refresh token reuse detected. Please login again.", 401);
                }

                // 9) Ensure refresh token is not expired
                if (existing.IsExpired)
                    throw new AppException("Refresh token expired. Please login again.", 401);

                // 10) Rotate refresh token (create new token, revoke old token)
                var newRawRefreshToken = _refreshTokenService.GenerateRawToken();
                var newRawRefreshTokenHash = _refreshTokenService.HashToken(newRawRefreshToken);
                var expiresAtUtc = _refreshTokenService.GetExpiryUtc();

                var newEntity = new RefreshToken
                {
                    Id = Guid.NewGuid(),
                    SessionId = existing.SessionId,
                    TokenHash = newRawRefreshTokenHash,
                    ParentTokenId = existing.Id,          // link new token to old token (rotation chain)
                    CreatedUtc = DateTime.UtcNow,
                    ExpiresUtc = expiresAtUtc
                };

                // Mark old token as revoked and record that it was replaced
                existing.RevokedUtc = DateTime.UtcNow;
                existing.RevokedReason = "Rotated";
                existing.ReplacedByTokenId = newEntity.Id;
                // existing => Modified

                _dbContext.RefreshTokens.Add(newEntity); // newRawRefreshTokenHash => Added

                // 11) Update session telemetry for "last seen"
                existing.Session.LastSeenUtc = DateTime.UtcNow;

                existing.Session.UserAgent = httpContext.Request.Headers.UserAgent.ToString();
                var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
                (string? IpAddress, string location) = await GetLocationAsync(ipAddress);

                existing.Session.IpAddress = IpAddress;
                existing.Session.LoginLocation = location;

                // 12) Load roles for JWT creation
                var roles = await _dbContext.UserRoles
                    .Where(ur => ur.UserId == existing.Session.UserId)
                    .Join(_dbContext.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name)
                    .ToListAsync();

                // 13) Create new JWT access token
                var (jwt, jwtExp) = _jwtTokenService.CreateAccessToken(
                    existing.Session.User, roles, dto.ClientId, existing.SessionId);

                // 14) Save and commit transaction
                await _dbContext.SaveChangesAsync();
                await tx.CommitAsync();

                _logger.LogInformation(
                    "Refresh success. UserId={UserId}, SessionId={SessionId}, ClientId={ClientId}, DeviceId={DeviceId}",
                    existing.Session.UserId, existing.SessionId, dto.ClientId, dto.DeviceId
                );

                // 15) Return new access token + new refresh token
                return new TokenResponseDTO
                {
                    AccessToken = jwt,
                    AccessTokenExpiresUtc = jwtExp,
                    RefreshToken = newRawRefreshToken,
                    RefreshTokenExpiresUtc = newEntity.ExpiresUtc,
                    SessionId = existing.SessionId
                };
            }
            catch (AppException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Refresh failed. ClientId={ClientId}, DeviceId={DeviceId}", dto.ClientId, dto.DeviceId);
                throw new AppException("Unable to refresh token. Please try again.", 500);
            }
        }

        public async Task LogoutAsync(LogoutRequestDTO dto)
        {
            try
            {
                // 1) Validate client app first
                var client = await ValidateClientCredentialsAsync(dto.ClientId, dto.ClientSecret);

                // 2) Hash refresh token and load token + session
                var tokenHash = _refreshTokenService.HashToken(dto.RefreshToken);

                var existing = await _dbContext.RefreshTokens
                    .Include(rt => rt.Session)
                        .ThenInclude(s => s.ClientApp)
                    .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash);

                // If token not found, logout is considered successful (idempotent)
                if (existing == null) return;

                if (existing.Session == null)
                    throw new AppException("Invalid logout session.", 401);

                // 3) Ensure this refresh token belongs to the same client and device
                if (existing.Session.ClientAppId != client.Id)
                    throw new AppException("Logout token does not belong to this client.", 401);

                if (!string.Equals(existing.Session.DeviceId, dto.DeviceId, StringComparison.Ordinal))
                    throw new AppException("Logout token does not match this device.", 401);

                // If already revoked, nothing to do (idempotent)
                if (existing.Session.RevokedUtc != null)
                    return;

                // Defensive check: client should still be active
                if (existing.Session.ClientApp == null || !existing.Session.ClientApp.IsActive)
                    throw new AppException("Invalid client.", 401);

                // 4) Revoke the session (logout only this device)
                existing.Session.RevokedUtc = DateTime.UtcNow;

                // 5) Revoke all refresh tokens under this session
                var tokens = await _dbContext.RefreshTokens
                    .Where(rt => rt.SessionId == existing.SessionId && rt.RevokedUtc == null)
                    .ToListAsync();

                foreach (var t in tokens)
                {
                    t.RevokedUtc = DateTime.UtcNow;
                    t.RevokedReason = "Logout";
                }

                await _dbContext.SaveChangesAsync();

                _logger.LogInformation(
                    "Logout successful. ClientId={ClientId}, SessionId={SessionId}, DeviceId={DeviceId}",
                    dto.ClientId, existing.SessionId, dto.DeviceId
                );
            }
            catch (AppException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Logout failed. ClientId={ClientId}, DeviceId={DeviceId}", dto.ClientId, dto.DeviceId);
                throw new AppException("Unable to logout. Please try again.", 500);
            }
        }

        public async Task LogoutAllAsync(Guid userId)
        {
            try
            {
                // 1) Collect all session ids belonging to this user
                var sessionIds = await _dbContext.UserSessions
                    .Where(s => s.UserId == userId)
                    .Select(s => s.Id)
                    .ToListAsync();

                if (sessionIds.Count == 0)
                {
                    _logger.LogInformation("LogoutAllDevices requested but no sessions found. UserId={UserId}", userId);
                    return;
                }

                var now = DateTime.UtcNow;

                // 2) Revoke all active sessions (logs out from all devices)
                var activeSessions = await _dbContext.UserSessions
                    .Where(s => s.UserId == userId && s.RevokedUtc == null)
                    .ToListAsync();

                foreach (var s in activeSessions)
                    s.RevokedUtc = now;

                // 3) Revoke all active refresh tokens for those sessions
                var activeTokens = await _dbContext.RefreshTokens
                    .Where(rt => sessionIds.Contains(rt.SessionId) && rt.RevokedUtc == null)
                    .ToListAsync();

                foreach (var t in activeTokens)
                {
                    t.RevokedUtc = now;
                    t.RevokedReason = "LogoutAllDevices";
                }

                await _dbContext.SaveChangesAsync();

                _logger.LogInformation(
                    "Logout all devices successful. UserId={UserId}, SessionsRevoked={SessionsCount}, TokensRevoked={TokensCount}",
                    userId, activeSessions.Count, activeTokens.Count
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Logout all devices failed. UserId={UserId}", userId);
                throw new AppException("Unable to logout from all devices.", 500);
            }
        }

        public async Task RevokeSessionAsync(Guid userId, Guid sessionId, string reason)
        {
            // 1) Only allow revoking sessions owned by this user
            var session = await _dbContext.UserSessions
                .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId);

            if (session == null)
                throw new AppException("Session not found.", 404);

            // 2) Mark session as revoked
            session.RevokedUtc = DateTime.UtcNow;

            // 3) Revoke all refresh tokens under this session
            var tokens = await _dbContext.RefreshTokens
                .Where(rt => rt.SessionId == sessionId && rt.RevokedUtc == null)
                .ToListAsync();

            foreach (var t in tokens)
            {
                t.RevokedUtc = DateTime.UtcNow;
                t.RevokedReason = reason;
            }

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Session revoked. SessionId={SessionId}, Reason={Reason}", sessionId, reason);
        }

        public async Task<List<SessionResponseDTO>> GetMySessionsAsync(Guid userId)
        {
            // Fetch all active sessions for the user, most recent first
            var sessions = await _dbContext.UserSessions
                .AsNoTracking()
                .Where(us => us.UserId == userId && us.RevokedUtc == null)
                .OrderByDescending(s => s.LastSeenUtc)
                .ToListAsync();

            // Convert entity list -> response DTO list
            return sessions.Select(s => new SessionResponseDTO
            {
                SessionId = s.Id,
                DeviceId = s.DeviceId,
                IpAddress = s.IpAddress,
                LoginLocation = s.LoginLocation,
                UserAgent = s.UserAgent,
                LastSeenUtc = s.LastSeenUtc,
                IsActive = s.IsActive
            }).ToList();
        }

        private async Task<ClientApp> ValidateClientCredentialsAsync(string clientId, string clientSecret)
        {
            // Basic input validation
            if (string.IsNullOrWhiteSpace(clientId))
                throw new AppException("ClientId is required.");

            if (string.IsNullOrWhiteSpace(clientSecret))
                throw new AppException("ClientSecret is required.");

            // Load client app by ClientId and ensure it is active
            var client = await _dbContext.ClientApps
                .FirstOrDefaultAsync(c => c.ClientId == clientId && c.IsActive);

            if (client == null)
                throw new AppException("Invalid client.");

            // Verify raw secret against stored BCrypt hash
            if (!_clientSecretHasher.Verify(clientSecret, client.ClientSecretHash))
                throw new AppException("Invalid client credentials.", 401);

            return client;
        }

        private async Task<(string?, string)> GetLocationAsync(string? ipAddress)
        {
            try
            {
                // If running locally, the request IP might be localhost (::1 or 127.0.0.1)
                // In that case we first fetch the real public IP using ipify.
                if (string.IsNullOrEmpty(ipAddress) || ipAddress == "::1" || ipAddress == "127.0.0.1")
                {
                    ipAddress = await _httpClient.GetStringAsync("https://api.ipify.org");
                }

                // http://ip-api.com/json/49.37.112.48
                // Call IP-based geolocation service to get city/state/country
                var response = await _httpClient.GetStringAsync($"http://ip-api.com/json/{ipAddress}");

                // Deserialize JSON into a dictionary for simple key lookups
                var locationData = JsonSerializer.Deserialize<Dictionary<string, object>>(response);

                // Build a readable location string if keys exist
                if (locationData != null &&
                    locationData.TryGetValue("city", out var city) &&
                    locationData.TryGetValue("regionName", out var state) &&
                    locationData.TryGetValue("country", out var country) &&
                    locationData.TryGetValue("zip", out var zip))
                {
                    return (ipAddress, $"{country}-{state}-{city}-{zip}");
                }

                return (ipAddress, "Unknown Location");
            }
            catch
            {
                // We never want geo-location failure to block login
                return (ipAddress, "Unknown Location");
            }
        }
    }

}
