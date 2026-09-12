using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using REIGN.Web;
using REIGN.Web.Components;
using REIGN.Web.Services;

HostingFileWatch.DisableForProductionHosts();
var builder = WebApplication.CreateBuilder(args);
HostingFileWatch.DisableReloadOnChange(builder.Configuration);
ContainerListen.Apply(builder);

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
        | ForwardedHeaders.XForwardedProto
        | ForwardedHeaders.XForwardedHost;
    options.KnownProxies.Clear();
    options.KnownIPNetworks.Clear();
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.Cookie.Name = "reign.admin";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var apiBase = ApiEndpoint.Resolve(builder.Configuration);
var adminUsername = builder.Configuration["REIGN_ADMIN_USERNAME"];
var adminPasswordHash = builder.Configuration["REIGN_ADMIN_PASSWORD_HASH"];

if (builder.Environment.IsDevelopment())
{
    adminUsername = string.IsNullOrWhiteSpace(adminUsername) ? "admin" : adminUsername;
    adminPasswordHash = string.IsNullOrWhiteSpace(adminPasswordHash)
        ? "pbkdf2-sha256$100000$cmVpZ24tbG9jYWwtZGV2IQ==$nFm3hBc4P7E2we3ZecEbjGhac7r1PoOV259VCwGETvk="
        : adminPasswordHash;
}

if (string.IsNullOrWhiteSpace(adminUsername) || string.IsNullOrWhiteSpace(adminPasswordHash))
{
    throw new InvalidOperationException(
        "Dashboard admin credentials are not configured. Set REIGN_ADMIN_USERNAME and REIGN_ADMIN_PASSWORD_HASH.");
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

app.UseForwardedHeaders();
app.UseStatusCodePagesWithReExecute("/not-found");
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapGet("/login", (HttpContext context) =>
{
    if (context.User.Identity?.IsAuthenticated == true)
    {
        return Results.Redirect("/");
    }

    return Results.Content(LoginHtml(), "text/html", Encoding.UTF8);
}).AllowAnonymous();

app.MapPost("/auth/login", async (HttpContext context) =>
{
    var form = await context.Request.ReadFormAsync();
    var suppliedUsername = form["username"].ToString();
    var suppliedPassword = form["password"].ToString();

    if (!AdminLogin.IsValid(suppliedUsername, suppliedPassword, adminUsername!, adminPasswordHash!))
    {
        return Results.Content(LoginHtml("Invalid username or password."), "text/html", Encoding.UTF8, StatusCodes.Status401Unauthorized);
    }

    var identity = new ClaimsIdentity(
        new[]
        {
            new Claim(ClaimTypes.Name, adminUsername!),
            new Claim(ClaimTypes.Role, "Admin")
        },
        CookieAuthenticationDefaults.AuthenticationScheme);

    await context.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        new ClaimsPrincipal(identity),
        new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(12),
            AllowRefresh = true
        });

    return Results.Redirect("/");
}).AllowAnonymous();

app.MapGet("/logout", async (HttpContext context) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/login");
}).RequireAuthorization();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .RequireAuthorization();

app.Run();

static string LoginHtml(string? error = null)
{
    var errorMarkup = string.IsNullOrWhiteSpace(error)
        ? string.Empty
        : $"<div class=\"error\">{System.Net.WebUtility.HtmlEncode(error)}</div>";

    return $$"""
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>REIGN Admin</title>
  <style>
    :root { color-scheme: dark; font-family: Inter, system-ui, sans-serif; }
    body { margin: 0; min-height: 100vh; display: grid; place-items: center; background: #090b10; color: #f5f7fb; }
    .card { width: min(92vw, 420px); box-sizing: border-box; padding: 28px; border: 1px solid #272b35; border-radius: 18px; background: #11141b; box-shadow: 0 24px 70px rgba(0,0,0,.45); }
    h1 { margin: 0 0 6px; font-size: 28px; letter-spacing: .04em; }
    p { margin: 0 0 22px; color: #9ca5b5; }
    label { display: block; margin: 14px 0 6px; font-size: 13px; color: #c8cfda; }
    input { width: 100%; box-sizing: border-box; padding: 13px 14px; border-radius: 10px; border: 1px solid #333846; background: #0b0e13; color: white; font-size: 16px; }
    button { width: 100%; margin-top: 20px; padding: 13px 14px; border: 0; border-radius: 10px; background: #f3f5f8; color: #090b10; font-weight: 800; font-size: 15px; cursor: pointer; }
    .error { margin: 0 0 12px; padding: 10px 12px; border-radius: 9px; background: #3a171b; color: #ffb7c0; }
    .small { margin-top: 18px; font-size: 12px; color: #717b8d; }
  </style>
</head>
<body>
  <main class="card">
    <h1>REIGN</h1>
    <p>Operator Command Center</p>
    {{errorMarkup}}
    <form method="post" action="/auth/login">
      <label for="username">Admin username</label>
      <input id="username" name="username" autocomplete="username" required autofocus />
      <label for="password">Password</label>
      <input id="password" name="password" type="password" autocomplete="current-password" required />
      <button type="submit">Sign in</button>
    </form>
    <div class="small">Authorized REIGN operators only.</div>
  </main>
</body>
</html>
""";
}
