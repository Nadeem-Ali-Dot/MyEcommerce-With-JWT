using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JwtAuth.Shared.Options
{
    // Strongly typed mapping for:
    // "RefreshToken": {
    //   "DaysToExpire": 14
    // }
    public sealed class RefreshTokenSettings
    {
        // Used by AuthServer when generating refresh token expiry.
        public int DaysToExpire { get; set; } = 14;
    }

}
