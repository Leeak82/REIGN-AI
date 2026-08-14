using REIGN.API.Calendar;
using REIGN.API.Messaging;
using REIGN.API.Options;
using REIGN.API.Services;
using Microsoft.EntityFrameworkCore;
using REIGN.Data;
using REIGN.Core.AI;
using REIGN.Data.Schema;
using REIGN.Data.Seed;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
});

var dbPath = builder.Configuration.GetConnectionString("Reign");
if (string.IsNullOrWhiteSpace(dbPath))
{
    dbPath = $"Data Source={Path.Combine(builder.Environment.ContentRootPath, "REIGN.db")}";
}

builder.Services.AddDbContext<ReignDbContext>(options =>
    options.UseSqlite(dbPath));

builder.Services.Configure<SmsOptions>(builder.Configuration.GetSection(SmsOptions.SectionName));
builder.Services.Configure<GoogleCalendarOptions>(builder.Configuration.GetSection(GoogleCalendarOptions.SectionName));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<REIGN.Core.Services.ConversationAIService>();
builder.Services.AddSingleton<IReignAssistant, ReignAssistant>();

builder.Services.AddScoped<BookingService>();
builder.Services.AddScoped<ConversationService>();
builder.Services.AddScoped<AppointmentService>();
builder.Services.AddScoped<SchedulingService>();
builder.Services.AddScoped<ConversationEngine>();
builder.Services.AddScoped<IncomingSmsProcessor>();
builder.Services.AddScoped<OwnerMessagingService>();
builder.Services.AddScoped<AppointmentCalendarSync>();

builder.Services.AddSingleton<SimulatedSmsSender>();
builder.Services.AddSingleton<TextNowUnsupportedSmsSender>();
builder.Services.AddHttpClient<TwilioSmsSender>();
builder.Services.AddHttpClient<VonageSmsSender>();
builder.Services.AddScoped<ConfigurableSmsSender>();
builder.Services.AddScoped<ISmsSender>(sp => sp.GetRequiredService<ConfigurableSmsSender>());

builder.Services.AddSingleton<SimulatedCalendarService>();
builder.Services.AddHttpClient<GoogleCalendarService>();
builder.Services.AddScoped<ConfigurableCalendarService>();
builder.Services.AddScoped<ICalendarService>(sp => sp.GetRequiredService<ConfigurableCalendarService>());

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ReignDbContext>();
    await SqliteSchemaUpgrades.ApplyAsync(db);
    await ServiceCatalogBootstrapper.EnsureAsync(db);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.Run();

public partial class Program { }
