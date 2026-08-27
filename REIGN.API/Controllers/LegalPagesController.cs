using Microsoft.AspNetCore.Mvc;
using REIGN.API.Legal;

namespace REIGN.API.Controllers;

[ApiController]
public class LegalPagesController : ControllerBase
{
    [HttpGet(SmsProgramPages.ProgramPath)]
    [HttpGet("/sms-program")]
    public IActionResult Program() => Html(SmsProgramPages.ProgramHtml());

    [HttpGet(SmsProgramPages.PrivacyPath)]
    public IActionResult Privacy() => Html(SmsProgramPages.PrivacyHtml());

    [HttpGet(SmsProgramPages.TermsPath)]
    public IActionResult Terms() => Html(SmsProgramPages.TermsHtml());

    private static ContentResult Html(string html) => new()
    {
        Content = html,
        ContentType = "text/html; charset=utf-8",
        StatusCode = StatusCodes.Status200OK
    };
}
