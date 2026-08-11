using REIGN.API.Services;
using Microsoft.EntityFrameworkCore;
using REIGN.Data;
using REIGN.Core.AI;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(options => { options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles; });

builder.Services.AddDbContext<ReignDbContext>(options =>
    options.UseSqlite("Data Source=REIGN.db"));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddScoped<ConversationEngine>();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<REIGN.Core.Services.ConversationAIService>();
builder.Services.AddScoped<REIGN.API.Services.BookingService>();

builder.Services.AddSingleton<IReignAssistant, ReignAssistant>();

builder.Services.AddScoped<REIGN.API.Services.ConversationService>();
builder.Services.AddScoped<REIGN.API.Services.AppointmentService>();
builder.Services.AddScoped<SchedulingService>();
builder.Services.AddScoped<ConversationEngine>();


var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ReignDbContext>();

    db.Database.EnsureCreated();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.Run();
