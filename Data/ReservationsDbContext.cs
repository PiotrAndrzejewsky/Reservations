using Microsoft.EntityFrameworkCore;
using Reservations.Models;
using System.Security.Cryptography;
using System.Text;

namespace Reservations.Data
{
    public class ReservationsDbContext : DbContext
    {
        public ReservationsDbContext(DbContextOptions<ReservationsDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Role> Roles { get; set; } = null!;
        public DbSet<Reservation> Reservations { get; set; } = null!;
        public DbSet<Session> Sessions { get; set; } = null!;
        public DbSet<Lane> Lanes { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Role>().HasData(
                new Role { Id = 1, Name = "Administrator" },
                new Role { Id = 2, Name = "Trainer" },
                new Role { Id = 3, Name = "User" }
            );

            modelBuilder.Entity<Lane>().HasData(
                new Lane { Id = 1, Name = "Tor 1", Capacity = 1 },
                new Lane { Id = 2, Name = "Tor 2", Capacity = 1 },
                new Lane { Id = 3, Name = "Tor 3", Capacity = 1 },
                new Lane { Id = 4, Name = "Tor 4", Capacity = 1 },
                new Lane { Id = 5, Name = "Tor 5", Capacity = 1 },
                new Lane { Id = 6, Name = "Tor 6", Capacity = 1 }
            );

            // Seed initial admin user: username=admin, password=admin
            string HashPassword(string password)
            {
                using var sha = SHA256.Create();
                var bytes = Encoding.UTF8.GetBytes(password);
                var hash = sha.ComputeHash(bytes);
                return BitConverter.ToString(hash).Replace("-", string.Empty);
            }

            modelBuilder.Entity<User>().HasData(
                new User
                {
                    Id = 1,
                    UserName = "admin",
                    Email = "admin@example.com",
                    RoleId = 1,
                    PasswordHash = HashPassword("admin")
                }
            );
        }
    }
}
