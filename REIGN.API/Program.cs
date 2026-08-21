using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using REIGN.API.AI;
using REIGN.API.Calendar;
using REIGN.API.Configuration;
using REIGN.API.Messaging;
using REIGN.API.Options;
using REIGN.API.Services;
using Microsoft.EntityFrameworkCore;
using REIGN.Data;
using REIGN.Core.AI;
using REIGN.Data.Schema;
using REIGN.Data.Seed;

HostingFileWatch.DisableForProductionHosts();
var builder = WebApplication.CreateBuilder(args);
HostingFileWatch.DisableReloadOnChange(builder.Configuration);
ConfigEnvironmentAliases.Apply(builder.Configuration);
ConfigEnvironmentAliases.ApplyRuntimeSmsDefaults(builder.Configuration, builder.Environment);
GoogleRedirectUri.Apply(builder.Configuration, builder.Environment);
ContainerListen.Apply(builder);

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
        | ForwardedHeaders.XForwardedProto
        | ForwardedHeaders.XForwardedHost;
    options.KnownProxies.Clear();
    options.KnownIPNetworks.Clear();
});

builder.Services.AddProblemDetails();
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
});

var connection = builder.Configuration.GetConnectionString("Reign");
if (string.IsNullOrWhiteSpace(connection))
{
    connection = DatabaseConnection.ResolveFromEnvironment();
}
string? sqliteStorageWarning = null;
if (string.IsNullOrWhiteSpace(connection))
{
    if (builder.Environment.IsDevelopment())
    {
        connection = $"Data Source={Path.Combine(builder.Environment.ContentRootPath, "REIGN.db")}";
    }
    else
    {
        throw new InvalidOperationException(DatabaseConnection.MissingPostgresMessage());
    }
}

builder.Services.AddDbContext<ReignDbContext>(options =>
{
    if (DatabaseConnection.IsPostgreSql(connection))
    {
        options.UseNpgsql(DatabaseConnection.Normalize(connection));
        return;
    }

    var sqlite = SqliteStorage.EnsureWritableFile(
        connection,
        builder.Environment.ContentRootPath,
        out sqliteStorageWarning);
    options.UseSqlite(sqlite);
});

builder.Services.Configure<SmsOptions>(builder.Configuration.GetSection(SmsOptions.SectionName));
builder.Services.Configure<GoogleCalendarOptions>(builder.Configuration.GetSection(GoogleCalendarOptions.SectionName));
builder.Services.PostConfigure<GoogleCalendarOptions>(options =>
    GoogleRedirectUri.ApplyToOptions(options, builder.Environment));
builder.Services.Configure<AiOptions>(builder.Configuration.GetSection(AiOptions.SectionName));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var cors = CorsOriginPolicy.Resolve(builder.Configuration, builder.Environment.IsDevelopment());
if (cors.Origins.Count > 0)
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy(CorsOriginPolicy.PolicyName, policy =>
        {
            policy.WithOrigins(cors.Origins.ToArray())
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
    });
}

builder.Services.AddSingleton<REIGN.Core.Services.ConversationAIService>();
builder.Services.AddSingleton<IReignAssistant, ReignAssistant>();
builder.Services.AddSingleton<IntentDetectionService>();
builder.Services.AddScoped<IBusinessProfileAccessor, BusinessProfileService>();
builder.Services.AddScoped<ConversationStateService>();
builder.Services.AddScoped<IntentMemoryService>();
builder.Services.AddScoped<CustomerMemoryService>();
builder.Services.AddScoped<OwnerAssistantService>();

builder.Services.AddScoped<BookingService>();
builder.Services.AddScoped<ConversationService>();
builder.Services.AddScoped<AppointmentService>();
builder.Services.AddScoped<SchedulingService>();
builder.Services.AddScoped<CatalogIntelligence>();
builder.Services.AddScoped<ConversationEngine>();
builder.Services.AddScoped<IncomingSmsProcessor>();
builder.Services.AddScoped<OwnerMessagingService>();
builder.Services.AddScoped<AppointmentCalendarSync>();

builder.Services.AddSingleton<SimulatedSmsSender>();
builder.Services.AddSingleton<TextNowUnsupportedSmsSender>();
builder.Services.AddHttpClient<TwilioSmsSender>();
builder.Services.AddHttpClient<VonageSmsSender>();
builder.Services.AddHttpClient<SmsGateSmsSender>();
builder.Services.AddScoped<ConfigurableSmsSender>();
builder.Services.AddScoped<ISmsSender>(sp => sp.GetRequiredService<ConfigurableSmsSender>());

builder.Services.AddSingleton<BusinessClock>();
builder.Services.AddSingleton<SimulatedCalendarService>();
builder.Services.AddHttpClient<GoogleCalendarService>();
builder.Services.AddScoped<ConfigurableCalendarService>();
builder.Services.AddScoped<ICalendarService>(sp => sp.GetRequiredService<ConfigurableCalendarService>());

builder.Services.AddHttpClient<GroqAiProvider>((sp, client) =>
{
    var ai = sp.GetRequiredService<IOptions<AiOptions>>().Value;
    if (Uri.TryCreate(ai.BaseUrl, UriKind.Absolute, out var uri))
    {
        client.BaseAddress = uri;
    }

    client.Timeout = TimeSpan.FromSeconds(Math.Clamp(ai.TimeoutSeconds, 5, 60));
});
builder.Services.AddSingleton<FallbackAiProvider>();
builder.Services.AddScoped<ResilientAiProvider>();
builder.Services.AddScoped<IAiProvider>(sp => sp.GetRequiredService<ResilientAiProvider>());

var app = builder.Build();
if (!string.IsNullOrWhiteSpace(sqliteStorageWarning))
{
    app.Logger.LogWarning("{Message}", sqliteStorageWarning);
}

var postgresEndpoint = DatabaseConnection.IsPostgreSql(connection)
    ? DatabaseConnection.DescribeEndpoint(DatabaseConnection.Normalize(connection))
    : null;
if (postgresEndpoint != null)
{
    app.Logger.LogInformation(
        "PostgreSQL endpoint {Endpoint} is configured. {PasswordStatus}.",
        postgresEndpoint,
        DatabaseConnection.DescribeSecret(connection));
}
else
{
    app.Logger.LogInformation("SQLite local fallback is configured.");
}

ConfigStartupValidator.ValidateAndLog(app);

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ReignDbContext>();
    try
    {
        await SqliteSchemaUpgrades.ApplyAsync(db);
        await ServiceCatalogBootstrapper.EnsureAsync(db);
    }
    catch (Exception ex) when (IsSocketFailure(ex))
    {
        var endpoint = postgresEndpoint ?? "local SQLite";
        throw new InvalidOperationException(DatabaseConnection.UnreachableMessage(endpoint), ex);
    }
    catch (Exception ex) when (DatabaseConnection.IsPasswordAuthFailure(ex))
    {
        var endpoint = postgresEndpoint ?? "PostgreSQL";
        app.Logger.LogCritical(
            ex,
            "{Message} The API will stay up so Render does not crash-loop. Booking and SMS persistence will fail until SUPABASE_DB_PASSWORD (or Password= in ConnectionStrings__Reign) matches the reset Supabase database password. Do not keep redeploying with the rejected password.",
            DatabaseConnection.AuthFailedMessage(endpoint));
    }
    catch (Exception ex) when (PostgresModel.IsMissingRelation(ex))
    {
        app.Logger.LogCritical(
            ex,
            "PostgreSQL is missing the Businesses table after schema setup. The API will stay up. Redeploy once after this build so CREATE TABLE can run.");
    }
}

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler();
}

app.UseSwagger();
app.UseSwaggerUI();

if (cors.Origins.Count > 0)
{
    app.UseCors(CorsOriginPolicy.PolicyName);
}

app.MapControllers();

app.Run();

static bool IsSocketFailure(Exception ex)
{
    for (var current = ex; current != null; current = current.InnerException)
    {
        if (current is System.Net.Sockets.SocketException)
        {
            return true;
        }

        if (current.GetType().Name.Contains("Socket", StringComparison.OrdinalIgnoreCase)
            && current.Message.Contains("Socket", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
    }

    return false;
}

public partial class Program { }
