namespace REIGN.Core.Models;

public class BookingRequest
{
    public string CustomerPhone { get; set; } = "";
    public string Message { get; set; } = "";
    public string? RequestedService { get; set; }
    public DateTime? RequestedTime { get; set; }
}