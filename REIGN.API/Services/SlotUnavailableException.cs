namespace REIGN.API.Services;

public sealed class SlotUnavailableException : InvalidOperationException
{
    public SlotUnavailableException()
        : base("That time is not available.")
    {
    }
}
