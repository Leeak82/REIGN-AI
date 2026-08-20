using REIGN.API.Calendar;
using REIGN.Data.Models;

namespace REIGN.API.Services;

public sealed class AppointmentWriteResult
{
    public required Appointment Appointment { get; init; }

    public bool Created { get; init; }

    public bool Rescheduled { get; init; }

    public bool Duplicate { get; init; }

    public CalendarSyncResult? CalendarSync { get; init; }
}
