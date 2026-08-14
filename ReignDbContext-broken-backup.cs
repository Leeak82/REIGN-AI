using Microsoft.EntityFrameworkCore;
using REIGN.Data.Models;

namespace REIGN.Data;

public class ReignDbContext : DbContext
{
    public ReignDbContext(DbContextOptions<ReignDbContext> options)
        : base(options)
    {
    }

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Appointment> Appointments => Set<Appointment>();

    public DbSet<Service> Services => Set<Service>();

    public DbSet<ServiceRecommendation> ServiceRecommendations => Set<ServiceRecommendation>();

    public DbSet<ConversationMessage> ConversationMessages => Set<ConversationMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);


        modelBuilder.Entity<ServiceRecommendation>()
            .HasOne(x => x.Service)
            .WithMany()
            .HasForeignKey(x => x.ServiceId);

        modelBuilder.Entity<ConversationMessage>()
            .HasOne(x => x.Customer)
            .WithMany(x => x.Messages)
            .HasForeignKey(x => x.CustomerId);

        modelBuilder.Entity<Service>().HasData(
            new Service
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "Oil Change",
                Price = 89.99m,
                DurationMinutes = 30,
                Active = true
            },
            new Service
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Name = "Brake Service",
                Price = 249.99m,
                DurationMinutes = 60,
                Active = true
            },
            new Service
            {
                Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                Name = "Diagnostic Inspection",
                Price = 129.99m,
                DurationMinutes = 60,
                Active = true
            },
                        new Service
            {
                Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                Name = "Vehicle Inspection",
                Price = 79.99m,
                DurationMinutes = 30,
                Active = true
            }
        );


        modelBuilder.Entity<ServiceRecommendation>().HasData(
            new ServiceRecommendation
            {
                Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                Trigger = "oil",
                Recommendation = "Customer likely needs routine oil maintenance.",
                ServiceId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Active = true
            },
            new ServiceRecommendation
            {
                Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                Trigger = "brake",
                Recommendation = "Customer may need brake service.",
                ServiceId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Active = true
            },
            new ServiceRecommendation
            {
                Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                Trigger = "diagnostic",
                Recommendation = "Customer requires diagnostic inspection.",
                ServiceId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                Active = true
            }
        );
    }
}


