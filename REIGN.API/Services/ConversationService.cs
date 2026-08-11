using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
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
        var customer = await _db.Customers
            .FirstOrDefaultAsync(x => x.PhoneNumber == phone);

        if (customer == null)
        {
            customer = new Customer
            {
                Id = Guid.NewGuid(),
                PhoneNumber = phone,
                CreatedAt = DateTime.UtcNow
            };

            _db.Customers.Add(customer);
        }

        if (string.IsNullOrWhiteSpace(customer.Name) && !string.IsNullOrWhiteSpace(message))
        {
            var name = ExtractName(message);

            if (!string.IsNullOrWhiteSpace(name))
            {
                customer.Name = name;
            }
        }

        await _db.SaveChangesAsync();

        return customer;
    }


    private string? ExtractName(string message)
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
        string body)
    {
        var message = new ConversationMessage
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            Direction = direction,
            Body = body,
            CreatedAt = DateTime.UtcNow
        };

        _db.ConversationMessages.Add(message);

        await _db.SaveChangesAsync();
    }
}
