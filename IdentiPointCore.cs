using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Reflection.Emit;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using static IdentiPoint.IdentiPointCore;
/*
  OWNER: JOEVER MONCEDA
  CREATED: 2026-02-16
*/
namespace IdentiPoint
{
    public class IdentiPointCore
    {
        public static class PasswordHasher
        {
            public static string HashPassword(string password, int iterations = 100_000)
            {
                using var rng = RandomNumberGenerator.Create();
                byte[] salt = new byte[16];
                rng.GetBytes(salt);
                var hash = PBKDF2(password, salt, iterations, 32);
                return $"{iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}".Trim();
            }

            private static byte[] PBKDF2(string password, byte[] salt, int iterations, int outputBytes)
            {
                using var derive = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
                return derive.GetBytes(outputBytes);
            }

            public static bool Verify(string hashed, string password)
            {
                var parts = hashed.Split('.', 3);
                if (parts.Length != 3) return false;
                if (!int.TryParse(parts[0], out var iterations)) return false;
                var salt = Convert.FromBase64String(parts[1]);
                var expected = Convert.FromBase64String(parts[2]);
                var actual = PBKDF2(password, salt, iterations, expected.Length);
                return CryptographicOperations.FixedTimeEquals(expected, actual);
            }
        }

        public interface IIdentityUserStore
        {
            Task<MiniUser> FindByUsernameAsync(string userName);
            Task<MiniUser> FindByEmailAsync(string email);
            Task CreateAsync(MiniUser user, string password);
            Task<bool> ValidateCredentialsAsync(string userNameOrEmail, string password);
        }

        public class EfIdentityUserStore : IIdentityUserStore
        {
            private readonly MiniIdentityDbContext _db;
            public EfIdentityUserStore(MiniIdentityDbContext db) => _db = db;

            public async Task CreateAsync(MiniUser user, string password)
            {
                user.PasswordHash = PasswordHasher.HashPassword(password);
                _db.Users.Add(user);
                await _db.SaveChangesAsync();
            }

            public Task<MiniUser> FindByEmailAsync(string email)
                => _db.Users.FirstOrDefaultAsync(u => u.Email == email)!;

            public Task<MiniUser> FindByUsernameAsync(string userName)
                => _db.Users.FirstOrDefaultAsync(u => u.UserName == userName)!;

            public async Task<bool> ValidateCredentialsAsync(string userNameOrEmail, string password)
            {
                var user = await _db.Users.FirstOrDefaultAsync(u => u.UserName == userNameOrEmail || u.Email == userNameOrEmail);
                return user != null && PasswordHasher.Verify(user.PasswordHash, password);
            }
        }

        public interface ITokenService
        {
            string GenerateToken(MiniUser user, IDictionary<string, string> extraClaims = null);
        }

        public class JwtTokenService : ITokenService
        {
            private readonly MiniIdentityOptions _opts;
            private readonly SigningCredentials _creds;

            public JwtTokenService(IOptions<MiniIdentityOptions> opts)
            {
                _opts = opts.Value;
                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opts.JwtSigningKey));
                _creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            }

            public string GenerateToken(MiniUser user, IDictionary<string, string> extraClaims = null)
            {
                var now = DateTime.UtcNow;
                var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.UserName ?? user.Email),
                new Claim(JwtRegisteredClaimNames.Email, user.Email)
            };
                if (extraClaims != null)
                    foreach (var kv in extraClaims)
                        claims.Add(new Claim(kv.Key, kv.Value));

                var jwt = new JwtSecurityToken(
                    issuer: _opts.JwtIssuer,
                    audience: _opts.JwtAudience,
                    claims: claims,
                    notBefore: now,
                    expires: now.Add(_opts.TokenLifetime),
                    signingCredentials: _creds
                );
                return new JwtSecurityTokenHandler().WriteToken(jwt);
            }
        }

        public interface IRefreshTokenService
        {
            Task<string> CreateRefreshTokenAsync(Guid userId);
            Task<(bool valid, MiniUser user)> ValidateRefreshTokenAsync(string refreshToken);
            Task RevokeAsync(string refreshToken);
        }

        public class EfRefreshTokenService : IRefreshTokenService
        {
            private readonly MiniIdentityDbContext _db;
            private readonly MiniIdentityOptions _opts;
            public EfRefreshTokenService(MiniIdentityDbContext db, IOptions<MiniIdentityOptions> opts)
            {
                _db = db; _opts = opts.Value;
            }

            public async Task<string> CreateRefreshTokenAsync(Guid userId)
            {
                var now = DateTime.UtcNow;
                var expired = _db.RefreshTokens.Where(t => t.ExpiresAt < now || t.Revoked);
                _db.RefreshTokens.RemoveRange(expired);

                var tokenBytes = new byte[32];
                RandomNumberGenerator.Fill(tokenBytes);
                var token = Convert.ToBase64String(tokenBytes);

                var entity = new MiniRefreshToken
                {
                    UserId = userId,
                    Token = token,
                    ExpiresAt = now.Add(_opts.RefreshTokenLifetime)
                };
                _db.RefreshTokens.Add(entity);
                await _db.SaveChangesAsync();
                return token;
            }

            public async Task<(bool valid, MiniUser user)> ValidateRefreshTokenAsync(string refreshToken)
            {
                var now = DateTime.UtcNow;
                var token = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.Token == refreshToken && !t.Revoked && t.ExpiresAt > now);
                if (token == null) return (false, null)!;
                var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == token.UserId);
                return user != null ? (true, user) : (false, null);
            }

            public async Task RevokeAsync(string refreshToken)
            {
                var token = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.Token == refreshToken);
                if (token != null)
                {
                    token.Revoked = true;
                    await _db.SaveChangesAsync();
                }
            }
        }
    }
}
