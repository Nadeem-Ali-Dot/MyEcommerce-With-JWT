using AuthServer.Data;
using AuthServer.Middleware;
using AuthServer.Security;
using AuthServer.Services.Implementations;
using AuthServer.Services.Interfaces;
using JwtAuth.Shared.Extensions;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Controllers + JSON Formatting
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Keep JSON property names exactly as defined in DTOs (PascalCase),
        // instead of converting them to camelCase.
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
    });

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
// Register IHttpClientFactory so services can safely make outbound HTTP calls
// (example: IP lookup and geo-location calls during login).
builder.Services.AddHttpClient();

// Register Auth database context.
// Stores users, roles, sessions, refresh tokens, and client applications.
builder.Services.AddDbContext<AuthDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("AuthDb"));
});

// Authentication / Token Issuing Services

// Password hashing service (BCrypt):
// - Hashes user passwords during registration
// - Verifies passwords during login
// Never store or log raw passwords.
builder.Services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();

// Client secret hashing/verifying (BCrypt):
// Validates trusted client applications (ClientId + ClientSecret),
// preventing unknown apps from requesting tokens.
builder.Services.AddScoped<IClientSecretHasher, ClientSecretHasher>();

// JWT token creation service:
// - Builds the JWT claims (user id, roles, session id, client id)
// - Signs the JWT using the configured signing key (HS256)
// - Returns access token + expiry
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

// Refresh Token Service:
// - Generates cryptographically strong random refresh tokens
// - Hashes them before storing in DB (raw token is returned only to client)
// Singleton is fine because it should be stateless and thread-safe.
builder.Services.AddSingleton<IRefreshTokenService, RefreshTokenService>();

// Business Service (Auth Workflows)
// AuthService implements the main flows:
// - Register (create user + assign default role)
// - Login (validate client + validate user + create session + issue tokens)
// - Refresh (validate client + rotate refresh token + issue new access token)
// - Logout/LogoutAll (revoke session and refresh tokens)
// - Session listing and session revocation
builder.Services.AddScoped<IAuthService, AuthService>();

// JWT Validation (Authentication) + Role/Policy Checks (Authorization)
// AddJwtAuth configures:
// - Authentication: Validate incoming access tokens (issuer, audience, signature, expiry)
// - Authorization: Enables [Authorize] and role/policy-based access control
// - Custom JSON for 401 (Unauthorized) and 403 (Forbidden)
builder.Services.AddJwtAuth(builder.Configuration);

var app = builder.Build();

// Global exception middleware:
// Converts thrown AppException/Exception into consistent JSON responses.
app.UseMiddleware<ExceptionHandlingMiddleware>();


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Authentication / Authorization Middleware (Order matters!)
// Auth middleware reads the Authorization: Bearer <token> header,
// validates the JWT, and builds HttpContext.User (claims principal).
app.UseAuthentication();

// Authorization middleware enforces [Authorize] rules:
// - checks authenticated user
// - checks roles/policies
// If user is authenticated but lacks permission => 403 Forbidden.
app.UseAuthorization();


app.MapControllers();

app.Run();
