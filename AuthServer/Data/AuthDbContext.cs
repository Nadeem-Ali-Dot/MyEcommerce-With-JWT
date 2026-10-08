using AuthServer.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuthServer.Data
{
    public class AuthDbContext : DbContext
    {
        public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options) { }

        public DbSet<ClientApp> ClientApps { get; set; }
        public DbSet<AppUser> Users { get; set; }
        public DbSet<AppRole> Roles { get; set; }
        public DbSet<AppUserRole> UserRoles { get; set; }
        public DbSet<UserSession> UserSessions { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ClientApp
            modelBuilder.Entity<ClientApp>().HasIndex(x => x.ClientId).IsUnique();
            modelBuilder.Entity<ClientApp>().Property(x => x.CreatedUtc).HasDefaultValueSql("GETUTCDATE()");

            // AppUser
            modelBuilder.Entity<AppUser>().HasIndex(x => x.Email).IsUnique();
            modelBuilder.Entity<AppUser>().Property(x => x.CreatedUtc).HasDefaultValueSql("GETUTCDATE()");

            // AppRole
            modelBuilder.Entity<AppRole>().HasIndex(x => x.Name).IsUnique();
            modelBuilder.Entity<AppRole>().Property(x => x.IsActive).HasDefaultValue(true);

            // AppUserRole
            modelBuilder.Entity<AppUserRole>().HasIndex(x => new { x.UserId, x.RoleId }).IsUnique();

            modelBuilder.Entity<AppUserRole>()
                .HasOne(x => x.User)
                .WithMany(u => u.UserRoles)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AppUserRole>()
                .HasOne(x => x.Role)
                .WithMany(r => r.UserRoles)
                .HasForeignKey(x => x.RoleId)
                .OnDelete(DeleteBehavior.Restrict);

            // "AssignedByUserId" points back to AppUser.
            // Restrict delete to preserve audit trail.
            modelBuilder.Entity<AppUserRole>()
                .HasOne<AppUser>()
                .WithMany()
                .HasForeignKey(x => x.AssignedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<AppUserRole>().Property(x => x.AssignedUtc).HasDefaultValueSql("GETUTCDATE()");

            // UserSession
            modelBuilder.Entity<UserSession>().HasIndex(x => new { x.UserId, x.ClientAppId, x.DeviceId });

            modelBuilder.Entity<UserSession>()
                .HasOne(x => x.User)
                .WithMany(u => u.Sessions)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserSession>()
                .HasOne(x => x.ClientApp)
                .WithMany()
                .HasForeignKey(x => x.ClientAppId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<UserSession>().Property(x => x.CreatedUtc).HasDefaultValueSql("GETUTCDATE()");
            modelBuilder.Entity<UserSession>().Property(x => x.LastSeenUtc).HasDefaultValueSql("GETUTCDATE()");

            // RefreshToken
            modelBuilder.Entity<RefreshToken>().HasIndex(x => x.TokenHash).IsUnique();

            modelBuilder.Entity<RefreshToken>()
                .HasOne(x => x.Session)
                .WithMany(s => s.RefreshTokens)
                .HasForeignKey(x => x.SessionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<RefreshToken>()
                .HasOne(x => x.ParentToken)
                .WithMany()
                .HasForeignKey(x => x.ParentTokenId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<RefreshToken>()
                .HasOne(x => x.ReplacedByToken)
                .WithMany()
                .HasForeignKey(x => x.ReplacedByTokenId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<RefreshToken>().Property(x => x.CreatedUtc).HasDefaultValueSql("GETUTCDATE()");

            // Seed Data
            Seed(modelBuilder);
        }

        private static void Seed(ModelBuilder modelBuilder)
        {
            // Hardcoded Date
            var seedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            // Client Apps (Web / Android / iOS)
            var webClientAppId = Guid.Parse("11111111-1111-1111-1111-111111111111");
            var androidClientAppId = Guid.Parse("11111111-1111-1111-1111-111111111112");
            var iosClientAppId = Guid.Parse("11111111-1111-1111-1111-111111111113");

            var webClientSecretPlain = "jrvV7CkYcnRYvS_Eey1wU4_0FVFA0NdCUXD2EaiJlvw";
            var androidClientSecretPlain = "SXn5KS1JV1ZGmevESdydacfc5lIHsBfgGrAD3zHaFSg";
            var iosClientSecretPlain = "pp5JSaUrx6Q1-EKk7_WkSa_MfSw_CuTO8h7V0vJQZ0I";

            modelBuilder.Entity<ClientApp>().HasData(
                new ClientApp { Id = webClientAppId, ClientId = "QaFK1LnRkjiht0cnkmVYbw", Name = "E-Commerce Web Client", ClientSecretHash = BCrypt.Net.BCrypt.HashPassword(webClientSecretPlain), IsActive = true, CreatedUtc = seedUtc },
                new ClientApp { Id = androidClientAppId, ClientId = "u5JH6gdhmAB6VBqBnrfDHQ", Name = "E-Commerce Android Client", ClientSecretHash = BCrypt.Net.BCrypt.HashPassword(androidClientSecretPlain), IsActive = true, CreatedUtc = seedUtc },
                new ClientApp { Id = iosClientAppId, ClientId = "8dj5ti3zXBUjpgQ_mH-bGA", Name = "E-Commerce iOS Client", ClientSecretHash = BCrypt.Net.BCrypt.HashPassword(iosClientSecretPlain), IsActive = true, CreatedUtc = seedUtc }
            );

            // Roles
            var adminRoleId = Guid.Parse("22222222-2222-2222-2222-222222222221");
            var customerRoleId = Guid.Parse("22222222-2222-2222-2222-222222222222");
            var sellerRoleId = Guid.Parse("22222222-2222-2222-2222-222222222223");
            var deliveryPartnerRoleId = Guid.Parse("22222222-2222-2222-2222-222222222224");

            modelBuilder.Entity<AppRole>().HasData(
                new AppRole { Id = adminRoleId, Name = "Admin", Description = "Full system access. Manages users, roles, catalog, orders, and platform settings.", IsActive = true },
                new AppRole { Id = customerRoleId, Name = "Customer", Description = "End user who browses products, places orders, makes payments, and manages their profile.", IsActive = true },
                new AppRole { Id = sellerRoleId, Name = "Seller", Description = "Merchant who lists products, manages inventory/pricing, and fulfills customer orders.", IsActive = true },
                new AppRole { Id = deliveryPartnerRoleId, Name = "DeliveryPartner", Description = "Delivery agent responsible for pickup, shipment tracking, and last-mile delivery updates.", IsActive = true }
            );

            // Users
            var adminUserId = Guid.Parse("11111111-aaaa-aaaa-aaaa-111111111111");
            var customerUserId = Guid.Parse("22222222-bbbb-bbbb-bbbb-222222222222");
            var sellerUserId = Guid.Parse("33333333-cccc-cccc-cccc-333333333333");
            var deliveryUserId = Guid.Parse("44444444-dddd-dddd-dddd-444444444444");

            // Use fixed passwords for demo; change later.
            var adminHash = BCrypt.Net.BCrypt.HashPassword("Admin@123");
            var customerHash = BCrypt.Net.BCrypt.HashPassword("Customer@123");
            var sellerHash = BCrypt.Net.BCrypt.HashPassword("Seller@123");
            var deliveryHash = BCrypt.Net.BCrypt.HashPassword("Delivery@123");

            modelBuilder.Entity<AppUser>().HasData(
                new AppUser { Id = adminUserId, Email = "admin@ecommerce.com", FirstName = "System", LastName = "Admin", PhoneNumber = "9999999999", PasswordHash = adminHash, IsActive = true, CreatedUtc = seedUtc },
                new AppUser { Id = customerUserId, Email = "customer@ecommerce.com", FirstName = "Demo", LastName = "Customer", PhoneNumber = "8888888888", PasswordHash = customerHash, IsActive = true, CreatedUtc = seedUtc },
                new AppUser { Id = sellerUserId, Email = "seller@ecommerce.com", FirstName = "Demo", LastName = "Seller", PhoneNumber = "7777777777", PasswordHash = sellerHash, IsActive = true, CreatedUtc = seedUtc },
                new AppUser { Id = deliveryUserId, Email = "delivery@ecommerce.com", FirstName = "Demo", LastName = "Rider", PhoneNumber = "6666666666", PasswordHash = deliveryHash, IsActive = true, CreatedUtc = seedUtc }
            );

            // User Role Assignments
            modelBuilder.Entity<AppUserRole>().HasData(
               new AppUserRole { Id = Guid.Parse("10000000-0000-0000-0000-000000000001"), UserId = adminUserId, RoleId = adminRoleId, AssignedByUserId = adminUserId, AssignedUtc = seedUtc, Notes = "Seeded admin" },
               new AppUserRole { Id = Guid.Parse("10000000-0000-0000-0000-000000000002"), UserId = customerUserId, RoleId = customerRoleId, AssignedByUserId = adminUserId, AssignedUtc = seedUtc, Notes = "Seeded customer" },
               new AppUserRole { Id = Guid.Parse("10000000-0000-0000-0000-000000000003"), UserId = sellerUserId, RoleId = sellerRoleId, AssignedByUserId = adminUserId, AssignedUtc = seedUtc, Notes = "Seeded seller" },
               new AppUserRole { Id = Guid.Parse("10000000-0000-0000-0000-000000000004"), UserId = deliveryUserId, RoleId = deliveryPartnerRoleId, AssignedByUserId = adminUserId, AssignedUtc = seedUtc, Notes = "Seeded delivery partner" }
           );
        }
    }

}
