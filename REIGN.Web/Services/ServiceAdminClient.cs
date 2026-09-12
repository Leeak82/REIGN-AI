using System.Net.Http.Json;

namespace REIGN.Web.Services;

public sealed class ServiceAdminClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public ServiceAdminClient(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public async Task<ServiceAdminCatalog> GetAsync()
    {
        using var client = CreateClient();
        var response = await client.GetAsync("api/services/manage");
        return await ReadCatalog(response);
    }

    public async Task<ServiceSaveResult> CreateAsync(ServiceAdminDto service)
    {
        using var client = CreateClient();
        var response = await client.PostAsJsonAsync("api/services", new
        {
            service.Name,
            service.Price,
            service.DurationMinutes,
            service.Active
        });
        return await ReadSave(response);
    }

    public async Task<ServiceSaveResult> UpdateAsync(ServiceAdminDto service)
    {
        using var client = CreateClient();
        var response = await client.PutAsJsonAsync($"api/services/{service.Id}", new
        {
            service.Name,
            service.Price,
            service.DurationMinutes,
            service.Active
        });
        return await ReadSave(response);
    }

    private HttpClient CreateClient()
    {
        var key = _configuration["REIGN_ADMIN_API_KEY"];
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException("Dashboard service editing is not configured.");
        }

        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(ApiEndpoint.Resolve(_configuration));
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-REIGN-ADMIN-KEY", key);
        return client;
    }

    private static async Task<ServiceAdminCatalog> ReadCatalog(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            var error = await ReadError(response);
            return new ServiceAdminCatalog { Error = error };
        }

        try
        {
            return await response.Content.ReadFromJsonAsync<ServiceAdminCatalog>()
                ?? new ServiceAdminCatalog { Error = "REIGN returned an empty service catalog." };
        }
        catch
        {
            return new ServiceAdminCatalog { Error = "REIGN returned an invalid service catalog." };
        }
    }

    private static async Task<ServiceSaveResult> ReadSave(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            return new ServiceSaveResult { Error = await ReadError(response) };
        }

        try
        {
            var service = await response.Content.ReadFromJsonAsync<ServiceAdminDto>();
            return service == null
                ? new ServiceSaveResult { Error = "REIGN returned an empty service response." }
                : new ServiceSaveResult { Service = service };
        }
        catch
        {
            return new ServiceSaveResult { Error = "REIGN returned an invalid service response." };
        }
    }

    private static async Task<string> ReadError(HttpResponseMessage response)
    {
        try
        {
            var error = await response.Content.ReadFromJsonAsync<ErrorEnvelope>();
            if (!string.IsNullOrWhiteSpace(error?.Error))
            {
                return error.Error;
            }
        }
        catch
        {
        }

        return $"REIGN API returned {(int)response.StatusCode}.";
    }

    private sealed class ErrorEnvelope
    {
        public string Error { get; set; } = "";
    }
}

public sealed class ServiceAdminCatalog
{
    public string Summary { get; set; } = "";

    public List<ServiceAdminDto> Services { get; set; } = [];

    public string? Error { get; set; }
}

public sealed class ServiceAdminDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = "";

    public decimal Price { get; set; }

    public int DurationMinutes { get; set; } = 30;

    public bool Active { get; set; } = true;
}

public sealed class ServiceSaveResult
{
    public ServiceAdminDto? Service { get; set; }

    public string? Error { get; set; }
}
