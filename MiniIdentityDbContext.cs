using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
/*
  OWNER: JOEVER MONCEDA
  CREATED: 2026-02-16
*/

namespace IdentiPoint
{

    public class MiniIdentityDbContext : DbContext
    {
        public MiniIdentityDbContext(DbContextOptions<MiniIdentityDbContext> opts) : base(opts) { }
        public DbSet<MiniUser> Users { get; set; }
        public DbSet<MiniRefreshToken> RefreshTokens { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MiniUser>().HasIndex(u => u.UserName).IsUnique();
            modelBuilder.Entity<MiniUser>().HasIndex(u => u.Email).IsUnique();
            modelBuilder.Entity<MiniRefreshToken>()
                .HasIndex(t => t.Token).IsUnique();
            modelBuilder.Entity<MiniRefreshToken>()
                .HasOne<MiniUser>()
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            base.OnModelCreating(modelBuilder);
        }
    }
}
