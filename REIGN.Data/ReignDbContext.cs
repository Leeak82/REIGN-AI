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

    public DbSet<Business> Businesses => Set<Business>();

    public DbSet<BusinessAIProfile> BusinessAIProfiles => Set<BusinessAIProfile>();

    public DbSet<ConversationState> ConversationStates => Set<ConversationState>();

    public DbSet<CustomerIntentMemory> CustomerIntentMemories => Set<CustomerIntentMemory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<BusinessAIProfile>()
            .HasOne(x => x.Business)
            .WithMany()
            .HasForeignKey(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ServiceRecommendation>()
            .HasOne(x => x.Service)
            .WithMany()
            .HasForeignKey(x => x.ServiceId);

        modelBuilder.Entity<ConversationMessage>()
            .HasOne(x => x.Customer)
            .WithMany(x => x.Messages)
            .HasForeignKey(x => x.CustomerId);
    }
}


