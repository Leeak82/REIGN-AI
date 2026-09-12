using System.Net.Http.Json;

namespace REIGN.Web.Services;

public sealed class CustomerAdminClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public CustomerAdminClient(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public async Task<CustomerAdminResult> CreateAsync(CustomerAdminDto customer)
    {
        using var client = CreateClient();
        var response = await client.PostAsJsonAsync("api/customers", new
        {
            customer.PhoneNumber,
            customer.Name,
            customer.Notes
        });
        return await ReadCustomer(response);
    }

    public async Task<CustomerAdminResult> UpdateAsync(CustomerAdminDto customer)
    {
        using var client = CreateClient();
        var response = await client.PutAsJsonAsync($"api/customers/{customer.Id}", new
        {
            customer.PhoneNumber,
            customer.Name,
            customer.Notes
        });
        return await ReadCustomer(response);
    }

    public async Task<CustomerDeleteResult> DeleteAsync(
        Guid id,
        string confirmPhone,
        bool deleteAppointments)
    {
        using var client = CreateClient();
        var response = await client.PostAsJsonAsync($"api/customers/{id}/delete", new
        {
            ConfirmPhone = confirmPhone,
            DeleteAppointments = deleteAppointments
        });

        if (!response.IsSuccessStatusCode)
        {
            return new CustomerDeleteResult { Error = await ReadError(response) };
        }

        return new CustomerDeleteResult { Deleted = true };
    }

    public async Task<SimulationPurgeResult> PurgeSimulationsAsync()
    {
        using var client = CreateClient();
        var response = await client.PostAsync("api/customers/purge-simulations", null);
        if (!response.IsSuccessStatusCode)
        {
            return new SimulationPurgeResult { Error = await ReadError(response) };
        }

        try
        {
            return await response.Content.ReadFromJsonAsync<SimulationPurgeResult>()
                ?? new SimulationPurgeResult { Error = "REIGN returned an empty cleanup response." };
        }
        catch
        {
            return new SimulationPurgeResult { Error = "REIGN returned an invalid cleanup response." };
        }
    }

    private HttpClient CreateClient()
    {
        var key = _configuration["REIGN_ADMIN_API_KEY"];
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException("Dashboard customer management is not configured.");
        }

        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(ApiEndpoint.Resolve(_configuration));
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-REIGN-ADMIN-KEY", key);
        return client;
    }

    private static async Task<CustomerAdminResult> ReadCustomer(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            return new CustomerAdminResult { Error = await ReadError(response) };
        }

        try
        {
            var customer = await response.Content.ReadFromJsonAsync<CustomerAdminDto>();
            return customer == null
                ? new CustomerAdminResult { Error = "REIGN returned an empty customer response." }
                : new CustomerAdminResult { Customer = customer };
        }
        catch
        {
            return new CustomerAdminResult { Error = "REIGN returned an invalid customer response." };
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

public sealed class CustomerAdminDto
{
    public Guid Id { get; set; }

    public string PhoneNumber { get; set; } = "";

    public string? Name { get; set; }

    public string? Notes { get; set; }

    public string? Warning { get; set; }
}

public sealed class CustomerAdminResult
{
    public CustomerAdminDto? Customer { get; set; }

    public string? Error { get; set; }
}

public sealed class CustomerDeleteResult
{
    public bool Deleted { get; set; }

    public string? Error { get; set; }
}

public sealed class SimulationPurgeResult
{
    public int MessagesDeleted { get; set; }

    public int CustomersDeleted { get; set; }

    public string? Error { get; set; }
}
