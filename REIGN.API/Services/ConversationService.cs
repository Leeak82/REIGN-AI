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


    private static readonly HashSet<string> NotNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "the", "this", "that", "it", "its", "it's",
        "hi", "hey", "hello", "yes", "no", "ok", "okay", "k", "yo",
        "thanks", "thank", "please", "help", "stop", "start", "info",
        "book", "schedule", "appointment", "cancel", "confirm", "confirmed",
        "tomorrow", "today", "tonight", "morning", "afternoon", "evening",
        "ready", "available", "here", "interested", "looking", "trying",
        "customer", "user", "unknown", "test", "miss", "reign",
        "qv", "hh", "hr", "quick", "visit", "half", "hour",
        "price", "cost", "hours", "services", "menu", "when", "what",
        "who", "where", "why", "how", "just", "name", "not", "now",
        "new", "next", "still", "also", "really", "currently"
    };

    private static readonly Regex IntroducedName = new(
        @"\b(?:my name is|name is|this is|i am|i['’]m|im|it['’]?s|call me|i go by)\s+(" + PersonNamePattern + @")\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex StandaloneName = new(
        @"^(?:just\s+|it['’]?s\s+)?(" + PersonNamePattern + @")[!.?]*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private const string PersonNamePattern = @"[A-Za-z][A-Za-z'\-]{1,}(?:\s+[A-Za-z][A-Za-z'\-]{1,}){0,2}";

    public static string? TryExtractName(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        var text = message.Trim();
        var introduced = IntroducedName.Match(text);
        if (introduced.Success)
        {
            var fromPhrase = NormalizePersonName(introduced.Groups[1].Value);
            if (fromPhrase != null)
            {
                return fromPhrase;
            }
        }

        var standalone = StandaloneName.Match(text);
        if (standalone.Success)
        {
            return NormalizePersonName(standalone.Groups[1].Value);
        }

        return null;
    }

    private static string? NormalizePersonName(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var cleaned = Regex.Replace(raw.Trim().TrimEnd('.', '!', '?'), @"\s+", " ");
        var tokens = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length is < 1 or > 3 || tokens.Any(token => !IsNameToken(token)))
        {
            return null;
        }

        return string.Join(" ", tokens.Select(TitleCaseToken));
    }

    private static bool IsNameToken(string token)
    {
        if (token.Length < 2 || NotNames.Contains(token.Trim('\'', '-')))
        {
            return false;
        }

        return token.All(ch => char.IsLetter(ch) || ch is '\'' or '-');
    }

    private static string TitleCaseToken(string token) =>
        string.Join("-", token.Split('-').Select(part =>
            string.Join("'", part.Split('\'').Select(static piece =>
                piece.Length == 0
                    ? piece
                    : char.ToUpperInvariant(piece[0]) + piece[1..].ToLowerInvariant()))));


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
