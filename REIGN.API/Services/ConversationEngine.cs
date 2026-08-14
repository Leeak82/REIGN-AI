using System.Globalization;
using Microsoft.EntityFrameworkCore;
using REIGN.Data;
using REIGN.Data.Models;

namespace REIGN.API.Services;

public class ConversationEngine
{
    private readonly ReignDbContext _db;
    private readonly CustomerMemoryService _memory;
    private readonly IntentDetectionService _intent;
    private readonly ConversationStateService _state;
    private readonly AppointmentService _appointments;


    public ConversationEngine(
        ReignDbContext db,
        CustomerMemoryService memory,
        IntentDetectionService intent,
        ConversationStateService state,
        AppointmentService appointments)
    {
        _db = db;
        _memory = memory;
        _intent = intent;
        _state = state;
        _appointments = appointments;
    }


    public async Task<string> Process(
        Customer customer,
        string message)
    {
        var intent = _intent.Detect(message);

        var business =
            await _db.Businesses
            .FirstOrDefaultAsync(x => x.Active);


        var state =
            await _state.GetOrCreate(customer.Id);


        message = message.ToLower().Trim();



        if (intent == CustomerIntent.Confirmation &&
            state.RequestedTime != null &&
            !string.IsNullOrWhiteSpace(state.SelectedService))
        {
            var appointment =
                await _appointments.CreateAppointment(
                    customer.Id,
                    state.SelectedService,
                    state.RequestedTime.Value);


            if (appointment != null)
            {
                state.CurrentStep = "Completed";

                await _db.SaveChangesAsync();


                return
                    $"Confirmed. Your {state.SelectedService} appointment has been created for {appointment.AppointmentTime:g}.";
            }


            return "I could not create the appointment. Please try again.";
        }



        if (message.Contains("quick") ||
            message.Contains("qv"))
        {
            await _state.UpdateService(customer.Id, "QV");

            return
                "Great. I can schedule a QV (Quick Visit - less than 30 minutes) for $150. What day and time works best?";
        }



        if (message.Contains("half") ||
            message.Contains("hh") ||
            message.Contains("30"))
        {
            await _state.UpdateService(customer.Id, "HH");

            return
                "Great. I can schedule an HH (Half Hour) visit for $300. What day and time works best?";
        }



        if (message.Contains("hour") ||
            message.Contains("hr") ||
            message.Contains("60"))
        {
            await _state.UpdateService(customer.Id, "HR");

            return
                "Great. I can schedule an HR (One Hour) visit for $500. What day and time works best?";
        }



        if (message.Contains("tomorrow") ||
            message.Contains("today") ||
            message.Contains("am") ||
            message.Contains("pm"))
        {
            var time =
                DateTime.Today
                .AddDays(message.Contains("tomorrow") ? 1 : 0)
                .AddHours(10);


            await _state.UpdateRequestedTime(customer.Id, time);


            return
                $"I have {time:g} available. Your {state.SelectedService ?? "visit"} appointment is ready. Confirm?";
        }



        if (intent == CustomerIntent.PricingQuestion)
        {
            return
                "Our visits are: QV $150, HH $300, HR $500. Which visit would you like?";
        }



        if (string.IsNullOrWhiteSpace(customer.Name))
        {
            if (LooksLikeName(message))
            {
                customer.Name =
                    CultureInfo.CurrentCulture.TextInfo
                    .ToTitleCase(message);


                await _db.SaveChangesAsync();


                return
                    $"Thanks {customer.Name}. What type of visit would you like to schedule?";
            }

            return
                "Thanks. What name should I put on the appointment?";
        }



        return
            $"Hi {customer.Name}. I can help schedule a QV ($150), HH ($300), or HR ($500) visit.";
    }



    private static bool LooksLikeName(string value)
    {
        var blocked = new[]
        {
            "hello",
            "hi",
            "hey",
            "yes",
            "no",
            "whats up",
            "what's up",
            "price",
            "cost",
            "quick visit",
            "qv",
            "hh",
            "hr",
            "tomorrow"
        };


        if (blocked.Contains(value))
            return false;


        var words = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);


        if (words.Length > 3)
            return false;


        return words.All(x =>
            x.Length >= 2 &&
            char.IsLetter(x[0]));
    }
}

