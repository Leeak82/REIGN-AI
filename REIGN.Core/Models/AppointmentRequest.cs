namespace REIGN.Core.Models;

public class AppointmentRequest
{
    public string CustomerPhone { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public string ServiceName { get; set; } = "";
    public DateTime RequestedDate { get; set; }
    public string LocationType { get; set; } = "";
    public bool HasTime { get; set; }
}