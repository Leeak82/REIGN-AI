using Microsoft.EntityFrameworkCore;
using REIGN.API.Messaging;
using REIGN.Data;
using REIGN.Data.Models;

namespace REIGN.API.Services;

public class OwnerSendResult
{
    public bool Succeeded { get; set; }

    public string? Error { get; set; }

    public bool HumanOverrideActive { get; set; }

    public SmsSendResult? Outbound { get; set; }
}

public class OwnerMessagingService
{
    private readonly ReignDbContext _db;
    private readonly ConversationService _conversationService;
    private readonly ISmsSender _smsSender;

    public OwnerMessagingService(
        ReignDbContext db,
        ConversationService conversationService,
        ISmsSender smsSender)
    {
        _db = db;
        _conversationService = conversationService;
        _smsSender = smsSender;
    }

    public async Task<OwnerSendResult> SendOverrideAsync(string phoneNumber, string body, CancellationToken cancellationToken = default)
    {
        var phone = PhoneNumbers.Normalize(phoneNumber);
        var customer = await _db.Customers.FirstOrDefaultAsync(x => x.PhoneNumber == phone, cancellationToken)
            ?? await _db.Customers.FirstOrDefaultAsync(x => x.PhoneNumber == phoneNumber, cancellationToken);

        if (customer == null)
        {
            return new OwnerSendResult { Succeeded = false, Error = "Customer not found." };
        }

        customer.HumanOverrideActive = true;
        customer.HumanOverrideAt = DateTime.UtcNow;

        await _conversationService.SaveMessage(
            customer.Id,
            "Outbound",
            body,
            source: "Owner",
            isOwnerOverride: true);

        var outbound = await _smsSender.SendAsync(new SmsSendRequest
        {
            To = customer.PhoneNumber,
            Body = body
        }, cancellationToken);

        return new OwnerSendResult
        {
            Succeeded = outbound.Succeeded,
            Error = outbound.Error,
            HumanOverrideActive = true,
            Outbound = outbound
        };
    }

    public async Task<bool> ResumeAssistantAsync(string phoneNumber, CancellationToken cancellationToken = default)
    {
        var phone = PhoneNumbers.Normalize(phoneNumber);
        var customer = await _db.Customers.FirstOrDefaultAsync(x => x.PhoneNumber == phone, cancellationToken)
            ?? await _db.Customers.FirstOrDefaultAsync(x => x.PhoneNumber == phoneNumber, cancellationToken);

        if (customer == null)
        {
            return false;
        }

        customer.HumanOverrideActive = false;
        customer.HumanOverrideAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
