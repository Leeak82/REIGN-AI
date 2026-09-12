using REIGN.Web;
using REIGN.Web.Components;
using REIGN.Web.Services;

HostingFileWatch.DisableForProductionHosts();
var builder = WebApplication.CreateBuilder(args);
HostingFileWatch.DisableReloadOnChange(builder.Configuration);
ContainerListen.Apply(builder);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var apiBase = ApiEndpoint.Resolve(builder.Configuration);

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
