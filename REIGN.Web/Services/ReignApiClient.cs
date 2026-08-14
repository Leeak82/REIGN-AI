using System.Net.Http.Json;

namespace REIGN.Web.Services;

public class ReignApiClient
{
    private readonly HttpClient _http;

    public ReignApiClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<CustomerDto>> GetCustomers() =>
        await _http.GetFromJsonAsync<List<CustomerDto>>("api/customers") ?? new();

    public async Task<List<ServiceDto>> GetServices() =>
        await _http.GetFromJsonAsync<List<ServiceDto>>("api/services") ?? new();

    public async Task<List<AppointmentDto>> GetAppointments() =>
        await _http.GetFromJsonAsync<List<AppointmentDto>>("api/appointments") ?? new();

    public async Task<List<AppointmentDto>> GetCustomerAppointments(string phone) =>
        await _http.GetFromJsonAsync<List<AppointmentDto>>($"api/customer-appointments/{phone}") ?? new();

    public async Task<List<AppointmentDto>> GetCustomerAppointments(Guid customerId) =>
        await _http.GetFromJsonAsync<List<AppointmentDto>>($"api/inbox/appointments/{customerId}") ?? new();

    public async Task<REIGN.Web.Components.Pages.AI.AIResult?> Recommend(string message)
    {
        var response = await _http.PostAsJsonAsync(
            "api/ai/recommend",
            new
            {
                Message = message
            });

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<REIGN.Web.Components.Pages.AI.AIResult>();
    }

    public async Task<List<MessageDto>> GetMessages(string phone) =>
        await _http.GetFromJsonAsync<List<MessageDto>>($"api/messages/{phone}") ?? new();

    public async Task<string> SendMessage(string phone, string message)
    {
        var response = await _http.PostAsJsonAsync(
            "api/sms/incoming",
            new
            {
                Phone = phone,
                Message = message
            });

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<SMSResponse>();

        return result?.Reply ?? "";
    }

    public async Task<List<BusinessDto>> GetBusinesses()
    {
        return await _http.GetFromJsonAsync<List<BusinessDto>>(
            "api/businesses")
            ?? new();
    }

    public async Task<List<ActivityDto>> GetActivity() =>
        await _http.GetFromJsonAsync<List<ActivityDto>>("api/activity") ?? new();

    public async Task<AppointmentDto?> CreateAppointment(CreateAppointmentRequest request)
    {
        var response = await _http.PostAsJsonAsync(
            "api/appointments",
            request);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<AppointmentDto>();
    }

    public async Task ConfirmAppointment(Guid id)
    {
        var response = await _http.PostAsync(
            $"api/appointments/{id}/confirm",
            null);

        response.EnsureSuccessStatusCode();
    }

    public async Task CancelAppointment(Guid id)
    {
        var response = await _http.PostAsync(
            $"api/appointments/{id}/cancel",
            null);

        response.EnsureSuccessStatusCode();
    }

    public class SMSResponse
    {
        public string Reply { get; set; } = "";
    }

    public class CustomerDto
    {
        public Guid Id { get; set; }
        public string PhoneNumber { get; set; } = "";
        public string? Name { get; set; }
        public int Messages { get; set; }
        public int Appointments { get; set; }
    }

    public class MessageDto
    {
        public Guid Id { get; set; }
        public string Direction { get; set; } = "";
        public string Body { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }

    public class ActivityDto
    {
        public DateTime Time { get; set; }
        public string Customer { get; set; } = "";
        public string Direction { get; set; } = "";
        public string Message { get; set; } = "";
    }

    public class AppointmentDto
    {
        public Guid Id { get; set; }
        public string Customer { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Service { get; set; } = "";
        public DateTime AppointmentTime { get; set; }
        public string Status { get; set; } = "";
        public decimal Price { get; set; }
        public int DurationMinutes { get; set; }
        public string? Notes { get; set; }
    }

    public class ServiceDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public int DurationMinutes { get; set; }
        public bool Active { get; set; }
    }

    public class BusinessDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public string Phone { get; set; } = "";
        public string? Address { get; set; }
        public bool Active { get; set; }
    }

    public class CreateAppointmentRequest
    {
        public string Phone { get; set; } = "";
        public string? Name { get; set; }
        public Guid ServiceId { get; set; }
        public DateTime AppointmentTime { get; set; }
        public string? Notes { get; set; }
    }
}
