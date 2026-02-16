using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using static IdentiPoint.IdentiPointCore;
/*
  OWNER: JOEVER MONCEDA
  CREATED: 2026-02-16
*/

namespace IdentiPoint.Test
{
    public class MiniIdentityTests
    {
        private readonly ServiceProvider _provider;
        private readonly MiniIdentityDbContext _db;
        private readonly MiniIdentityManager _identity;

        public MiniIdentityTests()
        {
            var services = new ServiceCollection();

            services.AddDbContext<MiniIdentityDbContext>(opt =>
                opt.UseInMemoryDatabase(Guid.NewGuid().ToString()));

            services.AddMiniIdentity(opt =>
            {
                opt.JwtSigningKey = "supersecret_signing_key_1234567890123456";
                opt.JwtIssuer = "test-issuer";
                opt.JwtAudience = "test-audience";
                opt.TokenLifetime = TimeSpan.FromMinutes(5);
                opt.RefreshTokenLifetime = TimeSpan.FromDays(1);
            });

            _provider = services.BuildServiceProvider();
            _db = _provider.GetRequiredService<MiniIdentityDbContext>();
            _identity = _provider.GetRequiredService<MiniIdentityManager>();
        }

        [Fact(DisplayName = "Register User Successfully")]
        public async Task Register_Should_Create_User()
        {
            var (ok, error) = await _identity.RegisterAsync("jh_hunt_1", "jh_hunt@example.com", "P@ssword1");

            Assert.True(ok, error);
            Assert.Null(error);

            var user = await _db.Users.FirstOrDefaultAsync(u => u.UserName == "jh_hunt_1");
            Assert.NotNull(user);
            Assert.True(PasswordHasher.Verify(user.PasswordHash, "P@ssword1"));
        }

        [Fact(DisplayName = "Login Should Return Valid Tokens")]
        public async Task Login_Should_Return_JWT_And_RefreshToken()
        {
            await _identity.RegisterAsync("jh_hunt", "jane@example.com", "P@ssword2");

            var (ok, accessToken, refreshToken) = await _identity.LoginAsync("jh_hunt", "P@ssword2");

            Assert.True(ok);
            Assert.NotNull(accessToken);
            Assert.NotNull(refreshToken);
            Assert.Contains(".", accessToken);
        }

        [Fact(DisplayName = "Refresh Token Should Generate New Tokens")]
        public async Task Refresh_Should_Rotate_Tokens()
        {
            await _identity.RegisterAsync("jh", "jh@repoint.com", "P@ssword3");
            var (_, _, refreshToken) = await _identity.LoginAsync("jh", "P@ssword3");

            var (ok, newAccess, newRefresh) = await _identity.RefreshAsync(refreshToken);

            Assert.True(ok);
            Assert.NotNull(newAccess);
            Assert.NotNull(newRefresh);
            Assert.NotEqual(refreshToken, newRefresh);
        }

        [Fact(DisplayName = "Invalid Refresh Token Should Fail")]
        public async Task Invalid_Refresh_Token_Should_Fail()
        {
            var (ok, newAccess, newRefresh) = await _identity.RefreshAsync("invalid-token");

            Assert.False(ok);
            Assert.Null(newAccess);
            Assert.Null(newRefresh);
        }

        [Fact(DisplayName = "Duplicate Registration Should Fail")]
        public async Task Register_Same_Username_Should_Fail()
        {
            await _identity.RegisterAsync("jh_dup", "jh_dup@example.com", "P@ssword1");
            var (ok, error) = await _identity.RegisterAsync("jh_dup", "jh_dup2@example.com", "P@ssword2");

            Assert.False(ok);
            Assert.Contains("exists", error, StringComparison.OrdinalIgnoreCase);
        }

        [Fact(DisplayName = "Login With Wrong Password Should Fail")]
        public async Task Login_Wrong_Password_Should_Fail()
        {
            await _identity.RegisterAsync("jh_wrong", "jh_wrong@example.com", "P@ssword1");
            var (ok, accessToken, refreshToken) = await _identity.LoginAsync("jh_wrong", "WrongPass!");

            Assert.False(ok);
            Assert.Null(accessToken);
            Assert.Null(refreshToken);
        }

        [Fact(DisplayName = "Login With Nonexistent User Should Fail")]
        public async Task Login_Nonexistent_User_Should_Fail()
        {
            var (ok, accessToken, refreshToken) = await _identity.LoginAsync("ghost_user", "NoPass!");

            Assert.False(ok);
            Assert.Null(accessToken);
            Assert.Null(refreshToken);
        }

        [Fact(DisplayName = "Refresh Token Expired Should Fail")]
        public async Task Expired_Refresh_Token_Should_Fail()
        {
            await _identity.RegisterAsync("jh_expired", "jh_expired@example.com", "P@ssword1");
            var (_, _, refreshToken) = await _identity.LoginAsync("jh_expired", "P@ssword1");

            var token = await _db.RefreshTokens.FirstAsync(r => r.Token == refreshToken);
            token.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            await _db.SaveChangesAsync();

            var (ok, newAccess, newRefresh) = await _identity.RefreshAsync(refreshToken);

            Assert.False(ok);
            Assert.Null(newAccess);
            Assert.Null(newRefresh);
        }

        [Fact(DisplayName = "Delete User Should Remove Related Tokens")]
        public async Task Delete_User_Should_Remove_Tokens()
        {
            await _identity.RegisterAsync("jh_del", "jh_del@example.com", "P@ssword1");
            var (_, _, refreshToken) = await _identity.LoginAsync("jh_del", "P@ssword1");

            var user = await _db.Users.FirstAsync(u => u.UserName == "jh_del");
            _db.Users.Remove(user);
            await _db.SaveChangesAsync();

            var tokenExists = await _db.RefreshTokens.AnyAsync(r => r.Token == refreshToken);
            Assert.False(tokenExists);
        }
    }
}
