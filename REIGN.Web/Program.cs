using REIGN.Web;
using REIGN.Web.Components;
using REIGN.Web.Services;

HostingFileWatch.DisableForProductionHosts();
var builder = WebApplication.CreateBuilder(args);
HostingFileWatch.DisableReloadOnChange(builder.Configuration);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var apiBase = builder.Configuration["ReignApi:BaseUrl"]
    ?? builder.Configuration["ApiBaseUrl"]
    ?? Environment.GetEnvironmentVariable("REIGN_API_BASE_URL")
    ?? "http://localhost:5012/";
if (!apiBase.EndsWith('/'))
{
    apiBase += "/";
}

builder.Services.AddHttpClient<ReignApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBase);
});

var app = builder.Build();

app.Logger.LogInformation("REIGN Web API base URL: {BaseUrl}", apiBase);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found");

app.UseAntiforgery();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
