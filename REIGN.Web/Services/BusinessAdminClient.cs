using System.Net.Http.Json;

namespace REIGN.Web.Services;

public sealed class BusinessAdminClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public BusinessAdminClient(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public async Task<BusinessAdminDto> GetAsync()
    {
        using var client = CreateClient();
        var response = await client.GetAsync("api/businesses/manage");
        if (!response.IsSuccessStatusCode)
        {
            return new BusinessAdminDto { Error = await ReadError(response) };
        }

        try
        {
            return await response.Content.ReadFromJsonAsync<BusinessAdminDto>()
                ?? new BusinessAdminDto { Error = "REIGN returned an empty business profile." };
        }
        catch
        {
            return new BusinessAdminDto { Error = "REIGN returned an invalid business profile." };
        }
    }

    public async Task<BusinessSaveResult> UpdateAsync(BusinessAdminDto business)
    {
        using var client = CreateClient();
        var response = await client.PutAsJsonAsync($"api/businesses/{business.Id}", new
        {
            business.Name,
            business.OwnerName,
            business.Phone,
            business.Email,
            business.Address,
            business.Hours,
            business.TimeZone
        });

        if (!response.IsSuccessStatusCode)
        {
            return new BusinessSaveResult { Error = await ReadError(response) };
        }

        try
        {
            var saved = await response.Content.ReadFromJsonAsync<BusinessAdminDto>();
            return saved == null
                ? new BusinessSaveResult { Error = "REIGN returned an empty business response." }
                : new BusinessSaveResult { Business = saved };
        }
        catch
        {
            return new BusinessSaveResult { Error = "REIGN returned an invalid business response." };
        }
    }

    private HttpClient CreateClient()
    {
        var key = _configuration["REIGN_ADMIN_API_KEY"];
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException("Dashboard business editing is not configured.");
        }

        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(ApiEndpoint.Resolve(_configuration));
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-REIGN-ADMIN-KEY", key);
        return client;
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

public sealed class BusinessAdminDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = "";

    public string? OwnerName { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? Address { get; set; }

    public string? Hours { get; set; }

    public string? TimeZone { get; set; }

    public bool Active { get; set; }

    public string? Error { get; set; }
}

public sealed class BusinessSaveResult
{
    public BusinessAdminDto? Business { get; set; }

    public string? Error { get; set; }
}
