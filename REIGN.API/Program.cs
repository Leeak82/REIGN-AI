using Microsoft.EntityFrameworkCore;
using REIGN.API.Services;
using REIGN.Data;
using REIGN.Core.AI;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://localhost:5012");

// Database path: configurable via ConnectionStrings:DefaultConnection,
// defaults to a workspace-relative location so the API works regardless
// of which absolute path the repo is checked out into.
var dbPath =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? Path.Combine(AppContext.BaseDirectory, "REIGN.db");

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler =
            System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

builder.Services.AddDbContext<ReignDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<REIGN.Core.Services.ConversationAIService>();
builder.Services.AddSingleton<IReignAssistant, ReignAssistant>();

builder.Services.AddScoped<ConversationEngine>();
builder.Services.AddScoped<ConversationStateService>();
builder.Services.AddScoped<BookingService>();
builder.Services.AddScoped<ConversationService>();
builder.Services.AddScoped<CustomerMemoryService>();
builder.Services.AddScoped<IntentDetectionService>();
builder.Services.AddScoped<IntentMemoryService>();
builder.Services.AddScoped<AppointmentService>();
builder.Services.AddScoped<SchedulingService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.Run();
