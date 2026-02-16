using Microsoft.Extensions.DependencyInjection;
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
    public static class MiniIdentityExtensions
    {
        public static IServiceCollection AddMiniIdentity(this IServiceCollection services, Action<MiniIdentityOptions> configureOptions)
        {
            services.Configure(configureOptions);
            services.AddSingleton<ITokenService, JwtTokenService>();
            services.AddScoped<IIdentityUserStore, EfIdentityUserStore>();
            services.AddScoped<IRefreshTokenService, EfRefreshTokenService>();
            services.AddScoped<MiniIdentityManager>();
            return services;
        }
    }
}
