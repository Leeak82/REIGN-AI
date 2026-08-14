using Microsoft.EntityFrameworkCore;
using REIGN.Core.Catalog;
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

    public DbSet<IntegrationToken> IntegrationTokens => Set<IntegrationToken>();

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

        modelBuilder.Entity<IntegrationToken>()
            .HasIndex(x => x.Provider)
            .IsUnique();

        modelBuilder.Entity<Service>().HasData(
            new Service
            {
                Id = ServiceCatalog.QuickVisitId,
                Name = ServiceCatalog.QuickVisitName,
                Price = ServiceCatalog.QuickVisitPrice,
                DurationMinutes = ServiceCatalog.QuickVisitMinutes,
                Active = true
            },
            new Service
            {
                Id = ServiceCatalog.HalfHourId,
                Name = ServiceCatalog.HalfHourName,
                Price = ServiceCatalog.HalfHourPrice,
                DurationMinutes = ServiceCatalog.HalfHourMinutes,
                Active = true
            },
            new Service
            {
                Id = ServiceCatalog.HourId,
                Name = ServiceCatalog.HourName,
                Price = ServiceCatalog.HourPrice,
                DurationMinutes = ServiceCatalog.HourMinutes,
                Active = true
            }
        );

        modelBuilder.Entity<ServiceRecommendation>().HasData(
            new ServiceRecommendation
            {
                Id = ServiceCatalog.QuickVisitRecommendationId,
                Trigger = "quick",
                Recommendation = "Customer is asking about a Quick Visit (QV): $150, less than 30 minutes.",
                ServiceId = ServiceCatalog.QuickVisitId,
                Active = true
            },
            new ServiceRecommendation
            {
                Id = ServiceCatalog.HalfHourRecommendationId,
                Trigger = "half",
                Recommendation = "Customer is asking about a Half Hour appointment (HH): $300, 30 minutes.",
                ServiceId = ServiceCatalog.HalfHourId,
                Active = true
            },
            new ServiceRecommendation
            {
                Id = ServiceCatalog.HourRecommendationId,
                Trigger = "hour",
                Recommendation = "Customer is asking about an Hour appointment (HR): $500, 60 minutes.",
                ServiceId = ServiceCatalog.HourId,
                Active = true
            }
        );
    }
}
