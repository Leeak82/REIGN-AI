using System.Net.Http.Json;

namespace REIGN.Web.Services;

public class ReignApiClient
{
    private readonly HttpClient _http;


    public ReignApiClient(HttpClient http)
    {
        _http = http;
    }



    public async Task<string> SendMessage(
        string phone,
        string message)
    {
        var response =
            await _http.PostAsJsonAsync(
                "api/sms/incoming",
                new
                {
                    Phone = phone,
                    Message = message
                });


        var result =
            await response.Content
                .ReadFromJsonAsync<SMSResponse>();


        return result?.Reply ?? "No response.";
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





    public class AppointmentDto
    {
        public Guid Id { get; set; }

        public string Customer { get; set; } = "";

        public string Service { get; set; } = "";

        public DateTime AppointmentTime { get; set; }

        public string Status { get; set; } = "";

        public decimal Price { get; set; }
    }
}