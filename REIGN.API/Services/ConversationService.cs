using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using REIGN.API.Messaging;
using REIGN.Data;
using REIGN.Data.Models;

namespace REIGN.API.Services;

public class ConversationService
{
    private readonly ReignDbContext _db;

    public ConversationService(ReignDbContext db)
    {
        _db = db;
    }


    public async Task<Customer> GetOrCreateCustomer(string phone, string? message = null)
    {
        var normalized = PhoneNumbers.Normalize(phone);
        var customer = await _db.Customers
            .Include(x => x.ConversationState)
            .Include(x => x.IntentMemory)
            .FirstOrDefaultAsync(x => x.PhoneNumber == normalized || x.PhoneNumber == phone);

        if (customer == null)
        {
            customer = new Customer
            {
                Id = Guid.NewGuid(),
                PhoneNumber = normalized,
                CreatedAt = DateTime.UtcNow,
                BusinessId = await _db.Businesses
                    .Where(x => x.Active)
                    .Select(x => (Guid?)x.Id)
                    .FirstOrDefaultAsync()
            };

            _db.Customers.Add(customer);
        }

        if (string.IsNullOrWhiteSpace(customer.Name) && !string.IsNullOrWhiteSpace(message))
        {
            var name = TryExtractName(message);

            if (!string.IsNullOrWhiteSpace(name))
            {
                customer.Name = name;
            }
        }

        await _db.SaveChangesAsync();

        return customer;
    }


    public static string? TryExtractName(string message)
    {
        var patterns = new[]
        {
            @"my name is ([A-Za-z]+)",
            @"this is ([A-Za-z]+)",
            @"i am ([A-Za-z]+)",
            @"i'm ([A-Za-z]+)"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(
                message,
                pattern,
                RegexOptions.IgnoreCase);

            if (match.Success)
            {
                return match.Groups[1].Value;
            }
        }

        return null;
    }


    public async Task SaveMessage(
        Guid customerId,
        string direction,
        string body,
        string source = "",
        bool isOwnerOverride = false)
    {
        var message = new ConversationMessage
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            Direction = direction,
            Body = body,
            Source = source,
            IsOwnerOverride = isOwnerOverride,
            CreatedAt = DateTime.UtcNow
        };

        _db.ConversationMessages.Add(message);

        await _db.SaveChangesAsync();
    }
}
