using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace JwtAuth.Shared.Options
{
    // Controls how the shared JWT middleware behaves in each API (Resource Server or Auth Server).
    // This keeps the extension flexible while still reusable and consistent.
    public sealed class JwtAuthSharedOptions
    {
        // Production: true. Local dev: false (if you don't have HTTPS locally).
        public bool RequireHttpsMetadata { get; set; } = false;

        // Save the raw token into HttpContext after validation.
        // Only enable if you truly need it (token forwarding/debug).
        public bool SaveToken { get; set; } = true;

        // Clock skew tolerance (seconds).
        // Helps with minor server/client clock drift.
        public int ClockSkewSeconds { get; set; } = 30;

        // Add shared policies (AdminOnly, CustomerOnly, AdminOrSeller, etc).
        // If false, only JWT validation is added.
        // Policies can be defined in each API separately.
        public bool AddSharedPolicies { get; set; } = true;

        // Enable logging for token validation failures (signature/expiry/malformed).
        // Do NOT log raw tokens.
        // This option only logs safe request metadata + exception.
        public bool EnableAuthFailureLogging { get; set; } = true;

        // Category name used for logging authentication failures.
        public string LoggerCategoryName { get; set; } = "JwtBearerAuth";

        // This is not usually stored in appsettings.
        // Serializer options used to produce JSON (you can customize naming policy, etc).
        public JsonSerializerOptions JsonSerializerOptions { get; set; } = new JsonSerializerOptions
        {
            PropertyNamingPolicy = null // keeps "IsSuccess" not "isSuccess"
        };
    }

}
