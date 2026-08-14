using System.Net.Http.Json;

namespace REIGN.Web.Services;

public class ReignApiClient
{
    private readonly HttpClient _http;


    public ReignApiClient(HttpClient http)
    {
        _http = http;
    }



    public Uri? BaseAddress => _http.BaseAddress;

    public async Task<ChatResult> ChatAsync(string phone, string message)
    {
        try
        {
            var response = await _http.PostAsJsonAsync(
                "api/ai/chat",
                new { Phone = phone, Message = message });

            if (!response.IsSuccessStatusCode)
            {
                return new ChatResult { Error = $"REIGN API returned {(int)response.StatusCode}." };
            }

            var result = await response.Content.ReadFromJsonAsync<ChatResult>();
            return result ?? new ChatResult { Error = "Empty API response." };
        }
        catch (Exception ex)
        {
            return new ChatResult { Error = "Could not reach the REIGN API. " + ex.Message };
        }
    }

    public async Task<string> SimulateIncomingSms(
        string phone,
        string message)
    {
        var chat = await ChatAsync(phone, message);
        if (!string.IsNullOrWhiteSpace(chat.Error))
        {
            return chat.Error;
        }

        return chat.Reply ?? "No response.";
    }

    public async Task<string> SendMessage(
        string phone,
        string message)
    {
        return await SimulateIncomingSms(phone, message);
    }

    public async Task<string> SendOwnerSms(
        string phone,
        string message)
    {
        var response =
            await _http.PostAsJsonAsync(
                "api/messages/send",
                new
                {
                    PhoneNumber = phone,
                    Body = message
                });

        if (!response.IsSuccessStatusCode)
        {
            return "Unable to send owner SMS.";
        }

        var result = await response.Content.ReadFromJsonAsync<OwnerSendResponse>();
        if (!string.IsNullOrWhiteSpace(result?.Error) && result.Sent != true)
        {
            return result.Error;
        }

        return result?.Simulated == true
            ? "Owner message saved (simulated SMS)."
            : "Owner message sent.";
    }

    public async Task ResumeAssistant(string phone)
    {
        await _http.PostAsJsonAsync(
            "api/messages/resume",
            new
            {
                PhoneNumber = phone,
                Body = ""
            });
    }

    public async Task<IntegrationStatusDto?> GetIntegrationStatus()
    {
        return await _http.GetFromJsonAsync<IntegrationStatusDto>("api/integrations/status");
    }





    public async Task<List<CustomerDto>> GetCustomers()
    {
        return await _http.GetFromJsonAsync<List<CustomerDto>>(
            "api/customers")
            ?? new();
    }





    public async Task<List<MessageDto>> GetMessages(
        string phone)
    {
        return await _http.GetFromJsonAsync<List<MessageDto>>(
            $"api/messages/{phone}")
            ?? new();
    }





    public async Task<List<AppointmentDto>> GetCustomerAppointments(
        string phone)
    {
        return await _http.GetFromJsonAsync<List<AppointmentDto>>(
            $"api/customer-appointments/{phone}")
            ?? new();
    }





    public async Task<List<AppointmentDto>> GetAppointments()
{
    return await _http.GetFromJsonAsync<List<AppointmentDto>>(
        "api/appointments")
        ?? new();
}


public async Task<List<AppointmentDto>> GetCustomerAppointments(Guid customerId)
{
    return await _http.GetFromJsonAsync<List<AppointmentDto>>(
        $"api/inbox/appointments/{customerId}")
        ?? new();
}


public async Task ConfirmAppointment(Guid id)
{
    await _http.PostAsync(
        $"api/appointments/{id}/confirm",
        null);
}


public async Task CancelAppointment(Guid id)
{
    await _http.PostAsync(
        $"api/appointments/{id}/cancel",
        null);
}





    public async Task<CustomerProfileDto?> GetCustomerProfile(string phone)
    {
        try
        {
            return await _http.GetFromJsonAsync<CustomerProfileDto>($"api/customers/{Uri.EscapeDataString(phone)}");
        }
        catch
        {
            return null;
        }
    }

    public async Task<string?> GetActivitySnapshot()
    {
        try
        {
            var result = await _http.GetFromJsonAsync<ActivityDto>("api/activity");
            return result?.Snapshot;
        }
        catch
        {
            return null;
        }
    }

    public async Task<CatalogDto?> GetCatalog()
    {
        try
        {
            return await _http.GetFromJsonAsync<CatalogDto>("api/services");
        }
        catch
        {
            return null;
        }
    }

    public class ActivityDto
    {
        public string Snapshot { get; set; } = "";
    }

    public class CatalogDto
    {
        public string Summary { get; set; } = "";

        public List<CatalogServiceDto> Services { get; set; } = [];
    }

    public class CatalogServiceDto
    {
        public string Name { get; set; } = "";

        public string Code { get; set; } = "";

        public decimal Price { get; set; }

        public int DurationMinutes { get; set; }
    }

    public class CustomerProfileDto
    {
        public Guid Id { get; set; }

        public string PhoneNumber { get; set; } = "";

        public string? Name { get; set; }

        public string? Notes { get; set; }

        public string? CurrentIntent { get; set; }

        public string? PendingServiceName { get; set; }

        public string? ConversationStatus { get; set; }

        public string? MemorySummary { get; set; }

        public int TurnCount { get; set; }
    }

    public async Task<ChatResult> AskOwnerAsync(string message)
    {
        try
        {
            var response = await _http.PostAsJsonAsync(
                "api/ai/owner",
                new { Message = message });

            if (!response.IsSuccessStatusCode)
            {
                return new ChatResult { Error = $"REIGN API returned {(int)response.StatusCode}." };
            }

            var result = await response.Content.ReadFromJsonAsync<ChatResult>();
            return result ?? new ChatResult { Error = "Empty API response." };
        }
        catch (Exception ex)
        {
            return new ChatResult { Error = "Could not reach the REIGN API. " + ex.Message };
        }
    }

    public class ChatResult
    {
        public string? Customer { get; set; }

        public string? Received { get; set; }

        public string? Reply { get; set; }

        public string? Intent { get; set; }

        public bool AutoReplied { get; set; }

        public bool Persisted { get; set; }

        public bool FellBack { get; set; }

        public string? Error { get; set; }
    }





    public class CustomerDto
    {
        public Guid Id { get; set; }

        public string PhoneNumber { get; set; } = "";

        public string? Name { get; set; }

        public int Messages { get; set; }

        public int Appointments { get; set; }

        public bool HumanOverrideActive { get; set; }

        public string? CurrentIntent { get; set; }

        public string? PendingServiceName { get; set; }

        public string? ConversationStatus { get; set; }

        public string? MemorySummary { get; set; }

        public int TurnCount { get; set; }
    }





    public class MessageDto
    {
        public Guid Id { get; set; }

        public string Direction { get; set; } = "";

        public string Body { get; set; } = "";

        public DateTime CreatedAt { get; set; }
    }





    public class AppointmentDto
    {
        public Guid Id { get; set; }

        public string Customer { get; set; } = "";

        public string Service { get; set; } = "";

        public DateTime AppointmentTime { get; set; }

        public string Status { get; set; } = "";

        public decimal Price { get; set; }
    }

    public class OwnerSendResponse
    {
        public bool Sent { get; set; }

        public bool HumanOverride { get; set; }

        public bool Simulated { get; set; }

        public string? Provider { get; set; }

        public string? Error { get; set; }
    }

    public class IntegrationStatusDto
    {
        public SmsStatusDto Sms { get; set; } = new();

        public GoogleStatusDto GoogleCalendar { get; set; } = new();
    }

    public class SmsStatusDto
    {
        public string ConfiguredProvider { get; set; } = "";

        public string ActiveProvider { get; set; } = "";

        public bool Simulated { get; set; }

        public bool CredentialsPresent { get; set; }
    }

    public class GoogleStatusDto
    {
        public string ConfiguredProvider { get; set; } = "";

        public string ActiveProvider { get; set; } = "";

        public bool Simulated { get; set; }

        public bool OauthClientConfigured { get; set; }

        public bool HasStoredGrant { get; set; }
    }
}