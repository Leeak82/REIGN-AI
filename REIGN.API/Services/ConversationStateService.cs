using Microsoft.EntityFrameworkCore;
using REIGN.Data;
using REIGN.Data.Models;

namespace REIGN.API.Services;

public class ConversationStateService
{
    private readonly ReignDbContext _db;

    public ConversationStateService(ReignDbContext db)
    {
        _db = db;
    }


    public async Task<ConversationState> GetOrCreate(Guid customerId)
    {
        var state = await _db.ConversationStates
            .FirstOrDefaultAsync(x => x.CustomerId == customerId);

        if (state != null)
            return state;


        state = new ConversationState
        {
            CustomerId = customerId,
            CurrentStep = "New"
        };


        _db.ConversationStates.Add(state);

        await _db.SaveChangesAsync();

        return state;
    }



    public async Task<ConversationState?> GetActiveBookingState(
        Guid customerId)
    {
        return await _db.ConversationStates
            .FirstOrDefaultAsync(x =>
                x.CustomerId == customerId &&
                (
                    x.CurrentStep == "WaitingForTime" ||
                    x.CurrentStep == "ReadyForBooking"
                ));
    }



    public async Task UpdateService(
        Guid customerId,
        string service)
    {
        var state = await GetOrCreate(customerId);

        state.SelectedService = service;
        state.CurrentStep = "WaitingForTime";
        state.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }



    public async Task UpdateLocation(
        Guid customerId,
        string location)
    {
        var state = await GetOrCreate(customerId);

        state.Location = location;
        state.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }



    public async Task UpdateRequestedTime(
        Guid customerId,
        DateTime time)
    {
        var state = await GetOrCreate(customerId);

        state.RequestedTime = time;
        state.CurrentStep = "ReadyForBooking";
        state.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }



    public async Task ClearRequestedTime(Guid customerId)
    {
        var state = await _db.ConversationStates
            .FirstOrDefaultAsync(x => x.CustomerId == customerId);

        if (state == null)
            return;

        state.RequestedTime = null;

        await _db.SaveChangesAsync();
    }

}
