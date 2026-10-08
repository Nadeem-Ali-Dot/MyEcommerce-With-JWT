using JwtAuth.Shared.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace JwtAuth.Shared.Extensions
{
    // Shared JWT authentication/authorization configuration used by:
    // - Multiple Resource Servers (APIs)
    // - Auth Server too (for protecting its own secured endpoints like /logout, /revoke, admin endpoints)
    //
    // Important:
    // - This class DOES NOT generate tokens.
    // - This class only VALIDATES incoming access tokens (Bearer).
    public static class JwtAuthServiceCollectionExtensions
    {
        // Adds JWT Bearer Authentication + Authorization to the DI container.
        // It also registered policies

        // Constants used for configuration sections.
        // Keeps spelling consistent and avoids magic strings everywhere.
        private const string JwtSectionName = "JWT";
        private const string RefreshTokenSectionName = "RefreshToken";
        private const string SharedSectionName = "JwtAuthShared";

        // Adds JWT validation using configuration from appsettings.json (Options Pattern),
        // appsettings.json:
        //   "JWT": { "Issuer": "...", "Audience": "...", "SigningKey": "...", "AccessTokenMinutes": 15 }
        //   "RefreshToken": { "DaysToExpire": 14 }
        //   "JwtAuthShared": { "RequireHttpsMetadata": true, SaveToken: true, "ClockSkewSeconds": 30}
        public static IServiceCollection AddJwtAuth(
            this IServiceCollection services,
            IConfiguration config)
        {
            // 1) Register strongly typed configuration (Options Pattern)
            // This allows us to inject IOptions<JwtSettings>, IOptions<RefreshTokenSettings>, and IOptions<JwtAuthSharedOptions>
            // anywhere in any project that references this shared library.
            services.Configure<JwtSettings>(config.GetSection(JwtSectionName));
            services.Configure<RefreshTokenSettings>(config.GetSection(RefreshTokenSectionName));
            services.Configure<JwtAuthSharedOptions>(config.GetSection(SharedSectionName));

            // Read settings immediately here to configure JWT bearer options.
            // We validate "required" keys and fail fast at startup if missing.

            var jwtSettings = config.GetSection(JwtSectionName).Get<JwtSettings>()
                ?? throw new InvalidOperationException("JWT configuration section is missing.");

            if (string.IsNullOrWhiteSpace(jwtSettings.Issuer))
                throw new InvalidOperationException("JWT:Issuer missing.");

            if (string.IsNullOrWhiteSpace(jwtSettings.Audience))
                throw new InvalidOperationException("JWT:Audience missing.");

            if (string.IsNullOrWhiteSpace(jwtSettings.SigningKey))
                throw new InvalidOperationException("JWT:SigningKey missing.");

            // Convert signing key into SymmetricSecurityKey for signature validation.
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SigningKey));

            // 2) Read shared behavior options from appsettings (with defaults)
            var sharedOptions = config.GetSection(SharedSectionName).Get<JwtAuthSharedOptions>()
                ?? new JwtAuthSharedOptions();

            // 3) Configure JWT Bearer authentication
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    // If true, metadata endpoints require HTTPS (recommended in production).
                    options.RequireHttpsMetadata = sharedOptions.RequireHttpsMetadata;

                    // If true, saves the raw token in HttpContext after validation.
                    // Only enable if you need to read/forward the token later.
                    options.SaveToken = sharedOptions.SaveToken;

                    // Token validation rules applied to every incoming Authorization: Bearer <token>.
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        // Validate token issuer ("iss")
                        ValidateIssuer = true,
                        ValidIssuer = jwtSettings.Issuer,

                        // Validate token audience ("aud")
                        ValidateAudience = false,
                        ValidAudience = jwtSettings.Audience,

                        // Validate signature using same signing key used by AuthServer
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = key,

                        // Validate expiry ("exp")
                        ValidateLifetime = true,

                        // Allows small time drift between servers/clients (clock differences).
                        // Example: if token expires at 10:00:00, it is still accepted until 10:00:30.
                        ClockSkew = TimeSpan.FromSeconds(sharedOptions.ClockSkewSeconds)
                    };

                    // 4) JwtBearerEvents allows us to customize response for 401/403.
                    // Here we return consistent JSON responses for:
                    // - 401 Unauthorized (missing/invalid token)
                    // - 403 Forbidden (valid token, but insufficient permission)
                    options.Events = new JwtBearerEvents
                    {
                        // 401: Token missing/invalid/expired => authentication did not succeed
                        OnChallenge = async context =>
                        {
                            // Stop default framework response so we can return our own JSON.
                            context.HandleResponse();

                            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                            context.Response.ContentType = "application/json";

                            // Returning a Custom Error Response for 401
                            var bodyObj = new
                            {
                                IsSuccess = false,
                                Status = StatusCodes.Status401Unauthorized,
                                Message = "Unauthorized. Please provide a valid access token."
                            };

                            // Serialize the .net object to JSON.
                            var json = JsonSerializer.Serialize(bodyObj, sharedOptions.JsonSerializerOptions);

                            // Write the JSON to the response body.
                            await context.Response.WriteAsync(json);
                        },

                        // Triggered when authentication succeeded (token valid),
                        // but authorization failed (missing required role/permission).
                        OnForbidden = async context =>
                        {
                            context.Response.StatusCode = StatusCodes.Status403Forbidden;
                            context.Response.ContentType = "application/json";

                            // Returning a Custom Error Response for 403
                            var bodyObj = new
                            {
                                IsSuccess = false,
                                Status = StatusCodes.Status403Forbidden,
                                Message = "Forbidden. You do not have permission to access this resource."
                            };

                            // Serialize the .net object to JSON.
                            var json = JsonSerializer.Serialize(bodyObj, sharedOptions.JsonSerializerOptions);

                            // Write the JSON to the response body.
                            await context.Response.WriteAsync(json);
                        },

                        // Triggered when token validation throws an exception due to
                        // signature issues, expiration, or malformed token
                        OnAuthenticationFailed = context =>
                        {
                            if (!sharedOptions.EnableAuthFailureLogging)
                                return Task.CompletedTask;

                            // Logging is useful for debugging issues like:
                            // - wrong issuer/audience
                            // - wrong signing key
                            // - expired token
                            // - malformed token
                            //
                            // IMPORTANT SECURITY NOTE:
                            // Never log the raw token value.
                            // Never log sensitive claims or PII.

                            // Resolve logger from DI at runtime
                            var loggerFactory = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>();
                            var logger = loggerFactory.CreateLogger(sharedOptions.LoggerCategoryName);

                            var http = context.HttpContext;

                            logger.LogWarning(context.Exception,
                                "JWT authentication failed. Path={Path}, Method={Method}, RemoteIP={RemoteIP}, Scheme={Scheme}, AuthHeaderPresent={AuthHeaderPresent}",
                                http.Request.Path.Value,
                                http.Request.Method,
                                http.Connection.RemoteIpAddress?.ToString(),
                                http.Request.Scheme,
                                http.Request.Headers.ContainsKey("Authorization"));

                            return Task.CompletedTask;
                        }
                    };
                });

            // 5) Authorization policies (shared across multiple APIs)
            // You can disable these if you want each API to define its own policies only.
            services.AddAuthorization(options =>
            {
                if (!sharedOptions.AddSharedPolicies)
                    return;

                // Role-Based Policies (common roles used across APIs)
                options.AddPolicy("AdminOnly", p => p.RequireRole("Admin"));
                options.AddPolicy("CustomerOnly", p => p.RequireRole("Customer"));

                // Claim-Based Policies (tokens include these claims)
                options.AddPolicy("HasClientId", p => p.RequireClaim("client_id"));
                options.AddPolicy("HasSessionId", p => p.RequireClaim("sid"));

                // OR roles: Admin OR Seller (single RequireRole call with multiple roles => OR)
                options.AddPolicy("AdminOrSeller", p => p.RequireRole("Admin", "Seller"));

                // AND roles: Admin AND Seller (two RequireRole calls => AND)
                options.AddPolicy("AdminAndSeller", p =>
                {
                    p.RequireRole("Admin");
                    p.RequireRole("Seller");
                });

                // Combined rule: Customer + client_id claim
                options.AddPolicy("CustomerWithClient", p =>
                {
                    p.RequireRole("Customer");
                    p.RequireClaim("client_id");
                });
            });

            return services;
        }
    }

}
