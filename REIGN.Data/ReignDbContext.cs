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

    public DbSet<Business> Businesses => Set<Business>();

    public DbSet<BusinessAIProfile> BusinessAIProfiles => Set<BusinessAIProfile>();

    public DbSet<ConversationState> ConversationStates => Set<ConversationState>();

    public DbSet<CustomerIntentMemory> CustomerIntentMemories => Set<CustomerIntentMemory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        if (Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true)
        {
            modelBuilder.Entity<Appointment>()
                .Property(x => x.AppointmentTime)
                .HasColumnType(PostgresTimestamps.WallClockColumnType);
        }

        modelBuilder.Entity<BusinessAIProfile>()
            .HasOne(x => x.Business)
            .WithMany(x => x.AIProfiles)
            .HasForeignKey(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Service>()
            .HasOne(x => x.Business)
            .WithMany(x => x.Services)
            .HasForeignKey(x => x.BusinessId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Customer>()
            .HasOne(x => x.Business)
            .WithMany(x => x.Customers)
            .HasForeignKey(x => x.BusinessId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Customer>()
            .HasOne(x => x.ConversationState)
            .WithOne(x => x.Customer)
            .HasForeignKey<ConversationState>(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Customer>()
            .HasOne(x => x.IntentMemory)
            .WithOne(x => x.Customer)
            .HasForeignKey<CustomerIntentMemory>(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ConversationState>()
            .HasIndex(x => x.CustomerId)
            .IsUnique();

        modelBuilder.Entity<CustomerIntentMemory>()
            .HasIndex(x => x.CustomerId)
            .IsUnique();

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
