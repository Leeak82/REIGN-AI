using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using REIGN.API.Options;
using REIGN.Core.AI;
using REIGN.Core.Catalog;
using REIGN.Data;

namespace REIGN.API.Services;

public class OwnerAssistantService
{
    private readonly ReignDbContext _db;
    private readonly IAiProvider _ai;
    private readonly BusinessProfileOptions _business;
    private readonly ILogger<OwnerAssistantService> _logger;

    public OwnerAssistantService(
        ReignDbContext db,
        IAiProvider ai,
        IOptions<BusinessProfileOptions> business,
        ILogger<OwnerAssistantService> logger)
    {
        _db = db;
        _ai = ai;
        _business = business.Value;
        _logger = logger;
    }

    public async Task<string> AnswerAsync(string question, CancellationToken cancellationToken = default)
    {
        var snapshot = await BuildSnapshotAsync(cancellationToken);
        try
        {
            var ai = await _ai.CompleteAsync(new AiCompletionRequest
            {
                UserMessage = question,
                Intent = "owner_activity",
                BusinessProfile = $"{_business.Name}. {_business.Offering}",
                MemoryContext = snapshot,
                ConversationState = "owner_console"
            }, cancellationToken);

            if (!string.IsNullOrWhiteSpace(ai.Text) && ai.UsedLiveModel)
            {
                return ai.Text;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Owner Groq path failed; using snapshot.");
        }

        return snapshot;
    }

    public async Task<string> BuildSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var customers = await _db.Customers.CountAsync(cancellationToken);
        var messages = await _db.ConversationMessages.CountAsync(cancellationToken);
        var today = DateTime.Today;
        var appointments = await _db.Appointments
            .Include(x => x.Customer)
            .Include(x => x.Service)
            .Where(x => x.AppointmentTime >= today && x.AppointmentTime < today.AddDays(1) && x.Status != "Cancelled")
            .OrderBy(x => x.AppointmentTime)
            .ToListAsync(cancellationToken);

        var pending = await _db.Appointments.CountAsync(x => x.Status == "Pending", cancellationToken);
        var lines = appointments.Count == 0
            ? "No appointments today."
            : string.Join("; ", appointments.Select(a =>
                $"{a.AppointmentTime:t} {a.Service?.Name} for {a.Customer?.Name ?? a.Customer?.PhoneNumber} ({a.Status})"));

        return
            $"{_business.AssistantName} activity: {customers} customers, {messages} messages, {pending} pending bookings. " +
            $"Catalog: {ServiceCatalog.CatalogSummary}. Today: {lines}";
    }
}
