# REIGN AI Deployment

REIGN is an AI appointment assistant for QV ($150), HH ($300), and HR ($500) visits.
Secrets belong in the host environment, never in git.

Runtime configuration is bound from ASP.NET Core keys, with aliases applied at startup
(`GROQ_API_KEY`, `GOOGLE_CLIENT_*`, `SKIPCALLS_*`, `TWILIO_*`, `ConnectionStrings__Reign`).

## Required variables

| Variable | Purpose |
| --- | --- |
| `GROQ_API_KEY` | Live Groq assistant. If unset, REIGN uses the built-in fallback. |
| `GOOGLE_CLIENT_ID` | Google Calendar OAuth client id |
| `GOOGLE_CLIENT_SECRET` | Google Calendar OAuth client secret |
| `SKIPCALLS_TOKEN` | SkipCalls API bearer token |
| `SKIPCALLS_FROM_NUMBER` | Live REIGN SMS number (`+18136380375`) |
| `ConnectionStrings__Reign` | Production PostgreSQL connection string; see `HOSTING.md`. Local development defaults to SQLite. |

Also set for live SMS/calendar (not secrets, but required):

```
SMS_PROVIDER=SkipCalls
Sms__Provider=SkipCalls
SKIPCALLS_FROM_NUMBER=+18136380375
Sms__BusinessPhoneNumber=+18136380375
GoogleCalendar__Provider=Google
GOOGLE_REDIRECT_URI=https://YOUR_DOMAIN/api/integrations/google/callback
REIGN_API_BASE_URL=https://YOUR_API_ORIGIN/
CORS_ALLOWED_ORIGINS=https://YOUR_WEB_ORIGIN
```

Do not commit real values. Use `.env` locally (gitignored) or the host secret store in production.

## Local development setup

1. Install .NET SDK 10.
2. Copy `.env.example` to `.env` and fill only what you need. Leave keys empty to use Groq fallback, Simulated SMS, and Simulated Calendar.
3. Run the API:

```bash
dotnet run --project REIGN.API
```

URLs:

- `https://localhost:5001`
- `http://localhost:5012`

Google callback (local, do not change):

`https://localhost:5001/api/integrations/google/callback`

4. Run the web UI:

```bash
dotnet run --project REIGN.Web
```

The web app calls `http://localhost:5012/` by default.

5. Health:

```bash
curl http://localhost:5012/health
curl http://localhost:5012/api/health
```

## Production setup

1. Choose a host (Render, Fly, Azure, Docker VM, etc.).
2. Build the API image from the repository root:

```bash
docker build -t reign-api -f REIGN.API/Dockerfile .
```

3. Set the required environment variables on the host. Keep `appsettings.json` empty of secrets.
4. Set `ConnectionStrings__Reign` to the production PostgreSQL connection string described in `HOSTING.md`. Startup requires a database connection outside Development.

### Database initialization

Production uses PostgreSQL. Startup initializes its schema from the EF model and applies the existing PostgreSQL upgrade guards. No SQLite volume is needed for PostgreSQL.

Local Development defaults to SQLite under the API content root. SQLite startup applies EF migrations and additive schema guards. An explicitly configured SQLite deployment still needs persistent storage outside `/app`; never use an ephemeral container file for customer data.

5. Set `REIGN_API_BASE_URL` on REIGN.Web to the public API origin. This alias overrides the checked-in localhost defaults; `ReignApi__BaseUrl` is also supported. Use an absolute HTTP(S) URL without credentials, query, or fragment.
6. Confirm `GET /health` returns `"status":"healthy"` and `"database":"connected"`. HTTP 200 alone is insufficient: a disconnected database returns 200 with `"status":"degraded"`. `/api/health` reports configuration presence, not database connectivity.
7. Confirm startup logs say Groq / SMS / Google credentials are present — never the secret values.

Host-specific build, start, port, and health settings: **`HOSTING.md`** (Azure App Service, Render, Railway).

Launch steps: **`PRODUCTION-LAUNCH-CHECKLIST.md`**.

`REIGN.API/appsettings.Production.example.json` is a placeholder-only reminder of the secret fields. The running app reads the nested keys and env aliases listed above.

Production CORS: set `CORS_ALLOWED_ORIGINS` to the public web origin (comma-separated https URLs). Wildcard `*` is rejected. Local development still allows localhost automatically.

## Google OAuth setup

1. In Google Cloud Console, create an OAuth 2.0 Web client.
2. Add the authorized redirect URI that matches how you run the API (they are different):

   - Local `dotnet run`: `https://localhost:5001/api/integrations/google/callback`
   - Local Docker: `http://localhost:8080/api/integrations/google/callback`
   - Production: `https://YOUR_DOMAIN/api/integrations/google/callback`

   Google allows `http://localhost` for development. Do not mix the Docker port with the Kestrel HTTPS port.

3. Put credentials in a gitignored `.env` or the host environment. Never commit real values.

```
GOOGLE_CLIENT_ID=
GOOGLE_CLIENT_SECRET=
GOOGLE_CALENDAR_ID=j.collins2491@gmail.com
GOOGLE_CALENDAR_TIMEZONE=America/Los_Angeles
GoogleCalendar__Provider=Google
```

`docker-compose.yml` pins `GoogleCalendar__RedirectUri` and `GOOGLE_REDIRECT_URI` to `http://localhost:8080/api/integrations/google/callback`. It loads `docker-oauth.env` (literal 8080 values, no host substitution) and sets `REIGN_DOCKER=1` so authorize and token exchange still use that 8080 callback if `appsettings.json` or a host `.env` still contains `https://localhost:5001/...`. It does **not** interpolate host `GOOGLE_REDIRECT_URI` or `GoogleCalendar__RedirectUri`. A `.env` used for `dotnet run` often contains `https://localhost:5001/...`; that value must not enter the container. Do not set `REIGN_DOCKER` on Render or other public hosts.

For `dotnet run` without Docker, set `GOOGLE_REDIRECT_URI=https://localhost:5001/api/integrations/google/callback`.

4. Set `GoogleCalendar__Provider=Google`.
5. Open `/api/integrations/google/authorize` once while signed in as **j.collins2491@gmail.com**.
   Production shortcut: `https://reign-ai-3.onrender.com/api/integrations/google/authorize`.
   The dashboard **Connect Google Calendar** / **Reconnect Google Calendar** buttons on Calendar and Integrations hit the same URL.
6. Confirm `/api/integrations/status` shows `hasStoredGrant: true`, `activeProvider: Google`, `calendarId: j.collins2491@gmail.com`, and a **https** `redirectUri` on `reign-ai-3.onrender.com` (never `localhost`).
7. Book a QV/HH/HR, reply `YES`, and verify one calendar event is created on j.collins2491@gmail.com. Confirmed appointments reuse `ExternalCalendarEventId` so Google does not get a duplicate event.

On Render, production prefers `RENDER_EXTERNAL_URL` / `RENDER_EXTERNAL_HOSTNAME` (and the public `X-Forwarded-Host`) so authorize and token exchange use the public service origin, currently:

`https://reign-ai-3.onrender.com/api/integrations/google/callback`

Still set these on the API service (not secrets, but required for Google Calendar):

| Key | Value |
| --- | --- |
| `GoogleCalendar__Provider` | `Google` |
| `GOOGLE_CALENDAR_ID` | `j.collins2491@gmail.com` |
| `GoogleCalendar__CalendarId` | `j.collins2491@gmail.com` |
| `GOOGLE_CALENDAR_TIMEZONE` | `America/Los_Angeles` |
| `GOOGLE_REDIRECT_URI` | `https://reign-ai-3.onrender.com/api/integrations/google/callback` |
| `GoogleCalendar__RedirectUri` | same public callback |
| `REIGN_DOCKER` | unset (never `1` on Render) |

In Google Cloud Console, the OAuth **Web** client's authorized redirect URIs must include that same production callback. The live client id is configured on Render (`GOOGLE_CLIENT_ID`).

`GET /api/integrations/status` includes `oauthClientId` (public) and `oauthClientSecretLooksLikeWeb` (true when the secret starts with `GOCSPX-` after trimming quotes). `oauthClientConfigured: true` only means a secret is **present**. If consent succeeds and the callback still returns `invalid_client`, paste the **Client secret** from that same Web client into Render as `GOOGLE_CLIENT_SECRET` (or `GoogleCalendar__ClientSecret`), then redeploy. Do not put the secret in git.

Authorize and the token POST use the same canonical callback. If exchange fails, the JSON includes Google's `error` / `error_description` (never the secret) plus the `redirectUri` that was sent.

## SkipCalls webhook setup (current live SMS)

Customers text **+18136380375**. Configure SkipCalls to POST inbound SMS to:

`https://reign-ai-3.onrender.com/api/sms/webhooks/skipcalls`

Set on the API service:

| Key | Value |
| --- | --- |
| `Sms__Provider` | `SkipCalls` |
| `SKIPCALLS_TOKEN` | live bearer token |
| `SKIPCALLS_FROM_NUMBER` | `+18136380375` |
| `Sms__BusinessPhoneNumber` | `+18136380375` |
| `SKIPCALLS_WEBHOOK_SECRET` | webhook secret when configured |

Keep SkipCalls `autoRespondToSms` disabled so Miss Reign is the only assistant replying.

## Twilio webhook setup (optional)

If Twilio is used later, set its live credentials and point **A Message Comes In** to:

`https://reign-ai-3.onrender.com/api/sms/incoming`

Method must be HTTP POST using Twilio's standard form fields (`From`, `To`, `Body`, `MessageSid`). JSON POST `/api/sms/incoming` is the Development simulator and stays disabled in production.

## SmsGate (Android, optional fallback)

Optional fallback: a dedicated Android phone + Straight Talk SIM **+19073001244** running [SMS Gateway for Android](https://sms-gate.app/). The current customer SMS inbox is SkipCalls **+18136380375**. Register the SmsGate `sms:received` webhook at `https://reign-ai-3.onrender.com/api/sms/webhooks/smsgate` only when SmsGate is the active provider. Details are in `HOSTING.md`.

## Health

`GET /health` reports API status, database connectivity, and whether Groq / SMS / calendar credentials are present. It never returns secret values.
