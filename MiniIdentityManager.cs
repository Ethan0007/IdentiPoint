using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static IdentiPoint.IdentiPointCore;
/*
  OWNER: JOEVER MONCEDA
  CREATED: 2026-02-16
*/

namespace IdentiPoint
{
    public class MiniIdentityManager
    {
        private readonly IIdentityUserStore _store;
        private readonly ITokenService _tokenService;
        private readonly IRefreshTokenService _refreshService;

        public MiniIdentityManager(IIdentityUserStore store, ITokenService tokenService, IRefreshTokenService refreshService)
        {
            _store = store; _tokenService = tokenService; _refreshService = refreshService;
        }

        public async Task<(bool ok, string error)> RegisterAsync(string username, string email, string password)
        {
            var existing = await _store.FindByUsernameAsync(username) ?? await _store.FindByEmailAsync(email);
            if (existing != null) return (false, "User already exists");
            var user = new MiniUser { UserName = username, Email = email, DisplayName = username };
            await _store.CreateAsync(user, password);
            return (true, null)!;
        }

        public async Task<(bool ok, string accessToken, string refreshToken)> LoginAsync(string usernameOrEmail, string password)
        {
            var valid = await _store.ValidateCredentialsAsync(usernameOrEmail, password);
            if (!valid) return (false, null, null)!;
            var user = await _store.FindByUsernameAsync(usernameOrEmail) ?? await _store.FindByEmailAsync(usernameOrEmail);
            var access = _tokenService.GenerateToken(user);
            var refresh = await _refreshService.CreateRefreshTokenAsync(user.Id);
            return (true, access, refresh);
        }

        public async Task<(bool ok, string newAccess, string newRefresh)> RefreshAsync(string refreshToken)
        {
            var (valid, user) = await _refreshService.ValidateRefreshTokenAsync(refreshToken);
            if (!valid) return (false, null, null)!;
            await _refreshService.RevokeAsync(refreshToken);
            var newAccess = _tokenService.GenerateToken(user);
            var newRefresh = await _refreshService.CreateRefreshTokenAsync(user.Id);
            return (true, newAccess, newRefresh);
        }
    }
}
