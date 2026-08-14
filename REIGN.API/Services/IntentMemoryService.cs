using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using REIGN.Core.AI;
using REIGN.Data;
using REIGN.Data.Models;

namespace REIGN.API.Services;

public class IntentMemoryService
{
    private readonly ReignDbContext _db;

    public IntentMemoryService(ReignDbContext db)
    {
        _db = db;
    }

    public async Task<CustomerIntentMemory> GetOrCreate(Guid customerId)
    {
        var memory = await _db.CustomerIntentMemories
            .FirstOrDefaultAsync(x => x.CustomerId == customerId);

        if (memory != null)
        {
            return memory;
        }

        memory = new CustomerIntentMemory
        {
            CustomerId = customerId,
            Intent = "Unknown",
            Stage = "New"
        };

        _db.CustomerIntentMemories.Add(memory);
        await _db.SaveChangesAsync();
        return memory;
    }

    public async Task RecordAsync(Customer customer, DetectedIntent intent, string message)
    {
        var memory = await GetOrCreate(customer.Id);
        var history = Deserialize(memory.HistoryJson);
        history.Add(new IntentMemoryEntry
        {
            At = DateTime.UtcNow,
            Intent = intent.Label,
            ServiceName = intent.ServiceName,
            Excerpt = Truncate(message)
        });

        if (history.Count > 12)
        {
            history = history.TakeLast(12).ToList();
        }

        memory.Intent = intent.Label;
        if (!string.IsNullOrWhiteSpace(intent.ServiceName))
        {
            memory.SelectedService = intent.ServiceName;
        }

        memory.Stage = intent.Kind switch
        {
            ReignIntentKind.Confirm => "Confirmed",
            ReignIntentKind.Cancel => "Cancelled",
            ReignIntentKind.Schedule => "Scheduling",
            _ => "Active"
        };
        memory.HistoryJson = JsonSerializer.Serialize(history);
        memory.Summary = BuildSummary(customer, memory, history);
        memory.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }

    public async Task<string> GetAsync(Guid customerId)
    {
        var memory = await _db.CustomerIntentMemories
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.CustomerId == customerId);

        if (memory == null)
        {
            return "";
        }

        if (!string.IsNullOrWhiteSpace(memory.Summary))
        {
            return memory.Summary;
        }

        var history = Deserialize(memory.HistoryJson);
        return history.Count == 0
            ? "No prior intents."
            : "Recent intents: " + string.Join(", ", history.TakeLast(5).Select(x => x.Intent));
    }

    private static List<IntentMemoryEntry> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<IntentMemoryEntry>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static string BuildSummary(Customer customer, CustomerIntentMemory memory, List<IntentMemoryEntry> history)
    {
        var recent = string.Join(", ", history.TakeLast(4).Select(x => x.Intent));
        var pending = string.IsNullOrWhiteSpace(memory.SelectedService)
            ? "none"
            : memory.SelectedService;
        return $"Intent memory for {customer.Name ?? customer.PhoneNumber}: recent={recent}; pending={pending}.";
    }

    private static string Truncate(string value) =>
        value.Length <= 80 ? value : value[..80];

    private sealed class IntentMemoryEntry
    {
        public DateTime At { get; set; }

        public string Intent { get; set; } = "";

        public string? ServiceName { get; set; }

        public string Excerpt { get; set; } = "";
    }
}
