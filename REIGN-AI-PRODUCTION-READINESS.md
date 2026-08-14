# REIGN AI Production Readiness Report

Checkpoint: live-deployment readiness on the existing architecture.
Catalog is unchanged: Quick Visit $150 / 20 minutes, Half Hour $300 / 30 minutes, Hour $500 / 60 minutes.

## Completed in this phase

### Configuration hardening
- Source `appsettings.json` and `appsettings.Example.json` contain empty secret fields only.
- SQLite database files (`*.db`) are gitignored and untracked.
- Common environment aliases are applied at API startup (`GROQ_API_KEY`, `TWILIO_*`, `VONAGE_*`, `GOOGLE_CLIENT_*`, `REIGN_CONNECTION_STRING`).
- Startup logs whether Groq, SMS, Google Calendar, and database settings are present. Secret values are never printed.
- Production warns when Simulated SMS/Calendar are still selected, and disables the internal SMS simulator unless `Sms__InternalApiKey` is set.

### Google Calendar
- Booking flow is: request → duration overlap availability → pending confirmation → calendar event create.
- Confirm (`YES` / `/api/appointments/{id}/confirm`) creates or updates the calendar event.
- Reschedule (`/api/appointments/{id}/reschedule` or a new time from SMS) updates the existing appointment. Confirmed appointments upsert the same event id.
- Cancel removes the local booking and deletes/cancels the calendar event.
- Google OAuth remains at `/api/integrations/google/authorize` and `/callback`, with tokens in `IntegrationTokens`.
- Access tokens refresh five minutes before expiry; rotated refresh tokens are stored.
- Event times are sent as business-timezone wall clock (`America/New_York` by default) plus `timeZone`, not UTC `Z` with a conflicting zone.
- Duplicate Google events are avoided by reusing `ExternalCalendarEventId` and looking up `extendedProperties.private.reignAppointmentId`.

### SMS production
- Provider webhooks remain `POST /api/sms/webhooks/twilio` and `POST /api/sms/webhooks/vonage`.
- Valid Twilio/Vonage signatures still process inbound SMS: customer lookup, conversation memory, reply, outbound send.
- After a valid signature, processing errors return success to the provider (empty TwiML / `200`) so retry storms are avoided.
- Outbound replies are attempted for customer, owner, and processor-failure fallback messages.
- Missing Twilio AuthToken or Vonage SignatureSecret returns `503` with a configuration hint, not a secret value.
- Internal simulator `POST /api/sms/incoming` stays out of the provider path and is locked down in Production without `Sms__InternalApiKey`.

### Production cleanup
- Blazor template `Counter` and `Weather` pages are removed.
- Navigation is Home, Dashboard, AI, Assistant, Inbox, Integrations.
- Web API base URL comes from `ReignApi__BaseUrl` / `REIGN_API_BASE_URL` (local default `http://localhost:5204/`).

### Validation
- `dotnet build`: 0 errors
- `dotnet test`: 21 passed (availability, confirm-then-create calendar, reschedule/cancel, config aliases, timezone wall-clock, existing production scenarios)


## Required environment variables

Set these in the host secret store. Do not put values in source files.

| Purpose | Primary key | Alias |
| --- | --- | --- |
| Database | `ConnectionStrings__Reign` | `REIGN_CONNECTION_STRING` |
| Groq | `Ai__ApiKey` | `GROQ_API_KEY` |
| SMS provider | `Sms__Provider` | `Twilio` or `Vonage` for live traffic |
| Business number | `Sms__BusinessPhoneNumber` | `REIGN_BUSINESS_PHONE` |
| Owner number | `Sms__OwnerPhoneNumber` | `REIGN_OWNER_PHONE` |
| Public API origin | `Sms__PublicBaseUrl` | `REIGN_PUBLIC_BASE_URL` |
| Internal simulator key | `Sms__InternalApiKey` | `REIGN_INTERNAL_API_KEY` |
| Twilio | `Sms__Twilio__AccountSid` `Sms__Twilio__AuthToken` `Sms__Twilio__FromNumber` | `TWILIO_ACCOUNT_SID` `TWILIO_AUTH_TOKEN` `TWILIO_FROM_NUMBER` |
| Twilio webhook URL | `Sms__Twilio__WebhookPublicUrl` | `TWILIO_WEBHOOK_URL` |
| Vonage | `Sms__Vonage__ApiKey` `Sms__Vonage__ApiSecret` `Sms__Vonage__SignatureSecret` | `VONAGE_API_KEY` `VONAGE_API_SECRET` `VONAGE_SIGNATURE_SECRET` |
| Google provider | `GoogleCalendar__Provider=Google` | |
| Google OAuth | `GoogleCalendar__ClientId` `GoogleCalendar__ClientSecret` `GoogleCalendar__RedirectUri` | `GOOGLE_CLIENT_ID` `GOOGLE_CLIENT_SECRET` `GOOGLE_REDIRECT_URI` |
| Calendar id / TZ | `GoogleCalendar__CalendarId` `GoogleCalendar__TimeZone` | `GOOGLE_CALENDAR_ID` `GOOGLE_CALENDAR_TIMEZONE` |
| Web → API | `ReignApi__BaseUrl` | `REIGN_API_BASE_URL` |

## Remaining deployment steps

1. Provision a dedicated REIGN business SMS number (not the owner personal cell) on Twilio or Vonage.
2. Point the provider inbound webhook at `/api/sms/webhooks/{provider}` over HTTPS.
3. Create a Google Cloud OAuth client with redirect URI `/api/integrations/google/callback`, then open `/api/integrations/google/authorize` once and confirm `hasStoredGrant`.
4. Set `Sms__Provider` and `GoogleCalendar__Provider` away from Simulated.
5. Set `GROQ_API_KEY` (or accept the built-in fallback assistant).
6. Set `ConnectionStrings__Reign` to durable storage. SQLite is fine for a single instance; use a hosted file or Postgres later if the process is ephemeral.
7. Set `ReignApi__BaseUrl` on the Web app to the live API origin.
8. Disable `Sms__AllowInternalSimulator` or set `Sms__InternalApiKey`.
9. Confirm host egress to `api.groq.com`, `oauth2.googleapis.com`, `www.googleapis.com`, and the SMS provider API.
10. Send one live QV/HH/HR booking through SMS and confirm the Google Calendar event appears only after `YES`.

## Production launch checklist

- [ ] No secrets in git (`appsettings.json` empty, `.env` ignored)
- [ ] Environment variables set on the host
- [ ] Startup logs show Groq key present (or fallback accepted)
- [ ] Startup logs show Twilio/Vonage credentials present
- [ ] Startup logs show Google OAuth client present
- [ ] Google consent completed; Integrations page shows stored grant
- [ ] Webhook signature validation succeeds (invalid signatures 401/403)
- [ ] Customer SMS: lookup, memory, reply, outbound send
- [ ] Availability rejects overlapping QV/HH/HR times
- [ ] Confirm creates one calendar event; reschedule reuses it; cancel removes it
- [ ] Owner personal number is not a customer thread
- [ ] Internal `/api/sms/incoming` is not publicly usable
- [ ] Navigation shows only REIGN features
- [ ] `dotnet build` and `dotnet test` pass on the release commit
