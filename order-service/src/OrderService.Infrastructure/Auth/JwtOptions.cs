using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OrderService.Infrastructure.Auth
{
    public class JwtOptions
    {
        public const string DevOnlySigningKey = "dev-only-insecure-signing-key-change-me-32chars-minimum";
        public string SigningKey { get; set; } = DevOnlySigningKey;
        public string Issuer { get; set; } = "order-system";
        public string Audience { get; set; } = "order-system";
        public int AccessTokenMinutes { get; set; } = 15;
    }
}