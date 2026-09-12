using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using REIGN.API.Controllers;
using REIGN.API.Options;
using REIGN.API.Services;
using REIGN.Core.Catalog;
using Xunit;

namespace REIGN.Tests;

public class CustomerAdminTests
{
    [Fact]
    public async Task Customer_admin_requires_key_and_can_create_and_update_contact()
    {
        await using var harness = await IncomingSmsProcessorTests.Harness.CreateAsync();
        var controller = CreateController(harness, out _);

        var denied = await controller.Create(new CustomerEditRequest
        {
            PhoneNumber = "+15555550111",
            Name = "Jamie"
        });
        Assert.IsType<UnauthorizedObjectResult>(denied);

        Authorize(controller);
        var created = await controller.Create(new CustomerEditRequest
        {
            PhoneNumber = "+15555550111",
            Name = "Jamie",
            Notes = "Prefers afternoons"
        });
        Assert.IsType<OkObjectResult>(created);

        var customer = await harness.Db.Customers.SingleAsync(x => x.PhoneNumber == "+15555550111");
        Assert.Equal("Jamie", customer.Name);
        Assert.Equal("Prefers afternoons", customer.Notes);

        var updated = await controller.Update(customer.Id, new CustomerEditRequest
        {
            PhoneNumber = "+15555550112",
            Name = "Jamie R",
            Notes = "Call after 2"
        });
        Assert.IsType<OkObjectResult>(updated);

        customer = await harness.Db.Customers.SingleAsync(x => x.Id == customer.Id);
        Assert.Equal("+15555550112", customer.PhoneNumber);
        Assert.Equal("Jamie R", customer.Name);
        Assert.Equal("Call after 2", customer.Notes);
    }

    [Fact]
    public async Task Purge_simulations_deletes_only_tagged_simulation_data()
    {
        await using var harness = await IncomingSmsProcessorTests.Harness.CreateAsync();
        var conversations = harness.Conversations;

        var simulated = await conversations.GetOrCreateCustomer("+15555550201");
        await conversations.SaveMessage(simulated.Id, "Inbound", "test", "SimulationCustomer");
        await conversations.SaveMessage(simulated.Id, "Outbound", "reply", "SimulationAssistant");

        var real = await conversations.GetOrCreateCustomer("+15555550202");
        await conversations.SaveMessage(real.Id, "Inbound", "hello", "Customer");
        await conversations.SaveMessage(real.Id, "Outbound", "hi", "Assistant");

        var mixed = await conversations.GetOrCreateCustomer("+15555550203");
        await conversations.SaveMessage(mixed.Id, "Inbound", "test", "SimulationCustomer");
        await conversations.SaveMessage(mixed.Id, "Inbound", "real text", "Customer");

        var controller = CreateController(harness, out _);
        Authorize(controller);

        var result = await controller.PurgeSimulations();
        Assert.IsType<OkObjectResult>(result);

        Assert.False(await harness.Db.Customers.AnyAsync(x => x.Id == simulated.Id));
        Assert.True(await harness.Db.Customers.AnyAsync(x => x.Id == real.Id));
        Assert.True(await harness.Db.Customers.AnyAsync(x => x.Id == mixed.Id));
        Assert.Equal(2, await harness.Db.ConversationMessages.CountAsync(x => x.CustomerId == real.Id));
        Assert.Single(await harness.Db.ConversationMessages.Where(x => x.CustomerId == mixed.Id).ToListAsync());
        Assert.DoesNotContain(
            await harness.Db.ConversationMessages.ToListAsync(),
            x => x.Source == "SimulationCustomer" || x.Source == "SimulationAssistant");
    }

    [Fact]
    public async Task Delete_customer_with_appointment_requires_explicit_opt_in_and_cancels_calendar()
    {
        await using var harness = await IncomingSmsProcessorTests.Harness.CreateAsync();
        var controller = CreateController(harness, out var appointmentService);
        Authorize(controller);

        var customer = await harness.Conversations.GetOrCreateCustomer("+15555550301");
        var when = DateTime.SpecifyKind(DateTime.Now.Date.AddDays(2).AddHours(11), DateTimeKind.Unspecified);
        var write = await appointmentService.CreateAppointment(customer.Id, ServiceCatalog.QuickVisitName, when);
        Assert.NotNull(write);
        var confirmed = await appointmentService.ConfirmAppointment(write!.Appointment.Id);
        Assert.NotNull(confirmed?.Appointment);
        Assert.Single(harness.Calendar.Events);

        var blocked = await controller.Delete(customer.Id, new CustomerDeleteRequest
        {
            ConfirmPhone = customer.PhoneNumber,
            DeleteAppointments = false
        });
        Assert.IsType<ConflictObjectResult>(blocked);
        Assert.True(await harness.Db.Customers.AnyAsync(x => x.Id == customer.Id));

        var deleted = await controller.Delete(customer.Id, new CustomerDeleteRequest
        {
            ConfirmPhone = customer.PhoneNumber,
            DeleteAppointments = true
        });
        Assert.IsType<OkObjectResult>(deleted);
        Assert.False(await harness.Db.Customers.AnyAsync(x => x.Id == customer.Id));
        Assert.Empty(await harness.Db.Appointments.Where(x => x.CustomerId == customer.Id).ToListAsync());
        Assert.True(harness.Calendar.Events.Single().Cancelled);
    }

    private static CustomersController CreateController(
        IncomingSmsProcessorTests.Harness harness,
        out AppointmentService appointmentService)
    {
        var calendarSync = new AppointmentCalendarSync(
            harness.Db,
            harness.Calendar,
            Options.Create(new GoogleCalendarOptions { TimeZone = "America/Los_Angeles" }),
            NullLogger<AppointmentCalendarSync>.Instance);
        appointmentService = new AppointmentService(
            harness.Db,
            calendarSync,
            new SchedulingService(harness.Db));

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["REIGN_ADMIN_API_KEY"] = "customer-admin-test-key"
            })
            .Build();

        var controller = new CustomersController(
            harness.Db,
            appointmentService,
            config,
            NullLogger<CustomersController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        return controller;
    }

    private static void Authorize(CustomersController controller)
    {
        controller.Request.Headers["X-REIGN-ADMIN-KEY"] = "customer-admin-test-key";
    }
}
