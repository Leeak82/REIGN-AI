namespace REIGN.Web.Models;

public class ActivityItem
{
    public DateTime Timestamp { get; set; }

    public string Type { get; set; } = "";

    public string Description { get; set; } = "";

    public string User { get; set; } = "";
}
