namespace REIGN.API.Services;

public sealed class InvalidBookingException : InvalidOperationException
{
    public InvalidBookingException(string message) : base(message)
    {
    }
}
