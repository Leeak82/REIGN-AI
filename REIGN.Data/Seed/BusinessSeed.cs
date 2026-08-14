using REIGN.Data.Models;

namespace REIGN.Data.Seed;

public static class BusinessSeed
{
    public static Business GetBusiness()
    {
        return new Business
        {
            Id = Guid.Parse("99999999-9999-9999-9999-999999999999"),

            Name = "REIGN AI",

            OwnerName = "Jessica",

            Phone = "(555) 555-REIGN",

            Email = "hello@reign.ai",

            Address = "100 Main Street",

            Industry = "Appointment Services",

            Active = true,


            Greeting =
                "Welcome to REIGN AI. How can we help you schedule a visit today?",


            Tone =
                "Professional, friendly, and concise",


            Personality =
                "Expert appointment coordinator",


            Instructions =
                "Help customers understand QV, HH, and HR visits, pricing, and scheduling."
        };
    }
}
