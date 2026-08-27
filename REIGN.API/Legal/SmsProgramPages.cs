namespace REIGN.API.Legal;

/// <summary>
/// Public HTML for A2P 10DLC campaign review. Twilio requires reachable
/// privacy, terms, and business/opt-in pages — not login-gated docs.
/// </summary>
public static class SmsProgramPages
{
    public const string PrivacyPath = "/privacy";
    public const string TermsPath = "/terms";
    public const string ProgramPath = "/sms";

    public static string PrivacyHtml() => Page(
        "Privacy Policy — Miss Reign",
        """
        <h1>Miss Reign SMS Privacy Policy</h1>
        <p>Effective date: August 27, 2026</p>
        <p>This policy describes how Miss Reign (operated with REIGN AI) collects and uses information when you text our dedicated scheduling number.</p>

        <h2>Who we are</h2>
        <p>Miss Reign is an appointment scheduling and customer-care service for Pierce County and King County, Washington, including Tacoma and Federal Way. Visits offered: Quick Visit (QV) $150 / under 30 minutes, Half Hour (HH) $300 / 30 minutes, and Hour (HR) $500 / 60 minutes.</p>

        <h2>Information we collect</h2>
        <p>When you opt in to SMS, we collect your mobile phone number, the content of messages you send and receive, and appointment details you provide (name, requested service, date, and time).</p>

        <h2>How we use it</h2>
        <p>We use this information only to reply to your questions, book or change appointments, send confirmations, and provide customer care. Message frequency varies. Typical booking conversations are 2–8 messages. Additional texts may be sent when a visit is confirmed, rescheduled, or cancelled. Message and data rates may apply.</p>

        <h2>No sharing of mobile numbers</h2>
        <p>Mobile opt-in data and mobile phone numbers are not shared, sold, rented, or distributed to third parties, affiliates, or lead generators for marketing or promotional purposes.</p>

        <h2>Opt out and help</h2>
        <p>Reply <strong>STOP</strong> to opt out of SMS. Reply <strong>HELP</strong> for help. You may also text START to the same dedicated Miss Reign number to opt in again.</p>

        <h2>Contact</h2>
        <p>For privacy questions, text HELP to the dedicated Miss Reign scheduling number, or ask for a human owner during an SMS conversation.</p>

        <p><a href="/sms">SMS program</a> · <a href="/terms">Terms and Conditions</a></p>
        """);

    public static string TermsHtml() => Page(
        "SMS Terms — Miss Reign",
        """
        <h1>Miss Reign SMS Terms and Conditions</h1>
        <p>Effective date: August 27, 2026</p>
        <p>These terms cover the Miss Reign / REIGN AI text-messaging program on our dedicated Twilio 10DLC number.</p>

        <h2>Program</h2>
        <p>Miss Reign sends customer-care SMS about appointment scheduling, availability, confirmations, reschedules, and cancellations for Quick Visit ($150), Half Hour ($300), and Hour ($500) visits in Pierce County and King County, Washington.</p>

        <h2>Consent</h2>
        <p>You opt in by texting the dedicated Miss Reign scheduling number, or by texting START to that number. SMS is optional. You do not have to join the text program to request in-person service by other means.</p>

        <h2>Frequency and rates</h2>
        <p>Message frequency varies. Message and data rates may apply.</p>

        <h2>Help and opt out</h2>
        <p><strong>Reply HELP for help. Reply STOP to opt out.</strong> After STOP you will receive a final confirmation and no further program messages unless you opt in again by texting START.</p>

        <h2>Carriers</h2>
        <p>Carriers are not liable for any delayed or undelivered messages.</p>

        <h2>Support</h2>
        <p>Text HELP to the dedicated Miss Reign scheduling number for customer support.</p>

        <p>Privacy Policy: <a href="/privacy">https://reign-ai-3.onrender.com/privacy</a></p>
        <p><a href="/sms">SMS program</a></p>
        """);

    public static string ProgramHtml() => Page(
        "Miss Reign SMS Program",
        """
        <h1>Miss Reign</h1>
        <p>Customer-care and appointment scheduling assistant for Pierce County and King County, Washington (Tacoma and Federal Way areas). Scheduling uses Google Calendar in America/Los_Angeles.</p>

        <h2>Services</h2>
        <ul>
          <li>Quick Visit (QV) — $150, under 30 minutes</li>
          <li>Half Hour (HH) — $300, 30 minutes</li>
          <li>Hour (HR) — $500, 60 minutes</li>
        </ul>

        <h2>How to opt in</h2>
        <p>Text the dedicated Miss Reign scheduling number, or text START to that number. By doing so you agree to receive customer-care texts from Miss Reign about scheduling. Message frequency varies. Message and data rates may apply. Reply STOP to opt out. Reply HELP for help.</p>
        <p>No website checkbox is used. Consent is not pre-checked and is not required to receive in-person service.</p>

        <p><a href="/privacy">Privacy Policy</a> · <a href="/terms">Terms and Conditions</a></p>
        """);

    private static string Page(string title, string body) =>
        $$"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <title>{{title}}</title>
          <style>
            body { font-family: system-ui, sans-serif; max-width: 42rem; margin: 2rem auto; padding: 0 1rem; line-height: 1.5; color: #111; }
            a { color: #0f766e; }
          </style>
        </head>
        <body>
        {{body}}
        </body>
        </html>
        """;
}
