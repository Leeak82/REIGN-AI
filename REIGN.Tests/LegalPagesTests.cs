using REIGN.API.Controllers;
using REIGN.API.Legal;
using Xunit;

namespace REIGN.Tests;

public class LegalPagesTests
{
    [Fact]
    public void Privacy_policy_includes_required_a2p_disclosures()
    {
        var html = SmsProgramPages.PrivacyHtml();
        Assert.Contains("not shared", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("third parties", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Message frequency varies", html, StringComparison.Ordinal);
        Assert.Contains("Message and data rates may apply", html, StringComparison.Ordinal);
        Assert.Contains("STOP", html, StringComparison.Ordinal);
        Assert.Contains("HELP", html, StringComparison.Ordinal);
        Assert.Contains("Miss Reign", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Terms_include_help_stop_carriers_and_privacy_link()
    {
        var html = SmsProgramPages.TermsHtml();
        Assert.Contains("<strong>Reply HELP for help. Reply STOP to opt out.</strong>", html, StringComparison.Ordinal);
        Assert.Contains("Message and data rates may apply", html, StringComparison.Ordinal);
        Assert.Contains("Message frequency varies", html, StringComparison.Ordinal);
        Assert.Contains("Carriers are not liable", html, StringComparison.Ordinal);
        Assert.Contains("/privacy", html, StringComparison.Ordinal);
        Assert.Contains("Miss Reign", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_page_identifies_business_and_opt_in()
    {
        var html = SmsProgramPages.ProgramHtml();
        Assert.Contains("Quick Visit", html, StringComparison.Ordinal);
        Assert.Contains("text START", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Message and data rates may apply", html, StringComparison.Ordinal);
        Assert.Contains("/privacy", html, StringComparison.Ordinal);
        Assert.Contains("/terms", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Legal_controller_serves_html()
    {
        var controller = new LegalPagesController();
        var privacy = Assert.IsType<Microsoft.AspNetCore.Mvc.ContentResult>(controller.Privacy());
        var terms = Assert.IsType<Microsoft.AspNetCore.Mvc.ContentResult>(controller.Terms());
        var program = Assert.IsType<Microsoft.AspNetCore.Mvc.ContentResult>(controller.Program());
        Assert.Equal(200, privacy.StatusCode);
        Assert.StartsWith("text/html", privacy.ContentType, StringComparison.Ordinal);
        Assert.Contains("Privacy Policy", privacy.Content, StringComparison.Ordinal);
        Assert.Contains("Terms and Conditions", terms.Content, StringComparison.Ordinal);
        Assert.Contains("Miss Reign", program.Content, StringComparison.Ordinal);
    }
}
