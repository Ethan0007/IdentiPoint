using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
/*
  OWNER: JOEVER MONCEDA
  CREATED: 2026-02-16
*/

namespace IdentiPoint
{
    public class MiniIdentityOptions
    {
        public string JwtIssuer { get; set; } = "mini-issuer";
        public string JwtAudience { get; set; } = "mini-audience";
        public string JwtSigningKey { get; set; }
        public TimeSpan TokenLifetime { get; set; } = TimeSpan.FromHours(1);
        public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(7);
    }

    public class MiniUser
    {
        [Key] public Guid Id { get; set; } = Guid.NewGuid();
        [Required, MaxLength(200)] public string UserName { get; set; }
        [Required, MaxLength(200)] public string Email { get; set; }
        [Required] public string PasswordHash { get; set; }
        public string DisplayName { get; set; }
        public bool EmailConfirmed { get; set; }
        public ICollection<MiniRefreshToken> RefreshTokens { get; set; }
    }

    public class MiniRefreshToken
    {
        [Key] public Guid Id { get; set; } = Guid.NewGuid();
        [Required] public Guid UserId { get; set; }
        [Required] public string Token { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool Revoked { get; set; }
    }
}
