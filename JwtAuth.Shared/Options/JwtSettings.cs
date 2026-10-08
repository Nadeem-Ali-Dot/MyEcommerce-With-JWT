using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JwtAuth.Shared.Options
{

    // Strongly Typed Mapping for:
    // "JWT": {
    //   "Issuer": "...",
    //   "Audience": "...",
    //   "SigningKey": "...",
    //   "AccessTokenMinutes": 15
    // }

    public sealed class JwtSettings
    {
        public string Issuer { get; set; } = string.Empty;
        public string Audience { get; set; } = string.Empty;

        // NOTE: Resource Servers must have the same SigningKey that AuthServer used to sign tokens.
        public string SigningKey { get; set; } = string.Empty;
        public int AccessTokenMinutes { get; set; } = 15;
    }

}
