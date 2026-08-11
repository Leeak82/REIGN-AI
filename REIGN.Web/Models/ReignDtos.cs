namespace REIGN.Web.Models;

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

    public string Customer { get; set; } = "";

    public string Direction { get; set; } = "";

    public string Body { get; set; } = "";

    public DateTime CreatedAt { get; set; }
}


public class AppointmentDto
{
    public Guid Id { get; set; }

    public string Service { get; set; } = "";

    public DateTime AppointmentTime { get; set; }

    public string Status { get; set; } = "";

    public decimal Price { get; set; }
}


public class SendMessageRequest
{
    public string PhoneNumber { get; set; } = "";

    public string Body { get; set; } = "";
}