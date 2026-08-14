using Microsoft.EntityFrameworkCore;
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



    public async Task<CustomerIntentMemory> GetOrCreate(
        Guid customerId)
    {
        var memory =
            await _db.CustomerIntentMemories
            .FirstOrDefaultAsync(x =>
                x.CustomerId == customerId);


        if(memory != null)
            return memory;



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



    public async Task Update(
        Guid customerId,
        string intent,
        string? service,
        string stage)
    {
        var memory =
            await GetOrCreate(customerId);



        memory.Intent = intent;


        if(!string.IsNullOrWhiteSpace(service))
        {
            memory.SelectedService = service;
        }


        memory.Stage = stage;


        memory.UpdatedAt =
            DateTime.UtcNow;


        await _db.SaveChangesAsync();
    }



    public async Task<CustomerIntentMemory?> Get(
        Guid customerId)
    {
        return await _db.CustomerIntentMemories
            .FirstOrDefaultAsync(x =>
                x.CustomerId == customerId);
    }

}
