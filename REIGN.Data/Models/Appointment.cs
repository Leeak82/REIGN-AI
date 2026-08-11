namespace REIGN.Data.Models;

public class Appointment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CustomerId { get; set; }

    public Customer Customer { get; set; } = null!;


    public Guid ServiceId { get; set; }

    public Service Service { get; set; } = null!;


    public decimal Price { get; set; }

    public int DurationMinutes { get; set; }

    public DateTime AppointmentTime { get; set; }

    public string Status { get; set; } = "Pending";

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
