using REIGN.Web.Components;
using REIGN.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var apiBase = builder.Configuration["ReignApi:BaseUrl"]
    ?? Environment.GetEnvironmentVariable("REIGN_API_BASE_URL")
    ?? "http://localhost:5204/";
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
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
