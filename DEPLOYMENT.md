# REIGN AI Deployment

REIGN is an AI appointment assistant for QV ($150), HH ($300), and HR ($500) visits.
Secrets belong in the host environment, never in git.

Runtime configuration is bound from ASP.NET Core keys, with aliases applied at startup
(`GROQ_API_KEY`, `GOOGLE_CLIENT_*`, `TWILIO_*`, `ConnectionStrings__Reign`).

## Required variables

| Variable | Purpose |
| --- | --- |
| `GROQ_API_KEY` | Live Groq assistant. If unset, REIGN uses the built-in fallback. |
| `GOOGLE_CLIENT_ID` | Google Calendar OAuth client id |
| `GOOGLE_CLIENT_SECRET` | Google Calendar OAuth client secret |
| `TWILIO_ACCOUNT_SID` | Twilio account |
| `TWILIO_AUTH_TOKEN` | Twilio auth token |
| `TWILIO_PHONE_NUMBER` | Dedicated REIGN business number (also `TWILIO_FROM_NUMBER`) |
| `ConnectionStrings__Reign` | SQLite path, e.g. `Data Source=/data/REIGN.db` |

Also set for live SMS/calendar (not secrets, but required):

```
Sms__Provider=Twilio
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
4. Persist SQLite (`ConnectionStrings__Reign`) on a volume, or the database will reset when the container is replaced.
5. Set `REIGN_API_BASE_URL` on REIGN.Web to the public API origin.
6. Confirm `GET /health` returns `"status":"healthy"` and `"database":"connected"`.
7. Confirm startup logs say Groq / Twilio / Google credentials are present — never the secret values.

Host-specific build, start, port, and health settings: **`HOSTING.md`** (Azure App Service, Render, Railway).

Launch steps: **`PRODUCTION-LAUNCH-CHECKLIST.md`**.

`REIGN.API/appsettings.Production.example.json` is a placeholder-only reminder of the secret fields. The running app reads the nested keys and env aliases listed above.

Production CORS: set `CORS_ALLOWED_ORIGINS` to the public web origin (comma-separated https URLs). Wildcard `*` is rejected. Local development still allows localhost automatically.

## Google OAuth setup

1. In Google Cloud Console, create an OAuth 2.0 Web client.
2. Add the authorized redirect URI:

   - Local: `https://localhost:5001/api/integrations/google/callback`
   - Production: `https://YOUR_DOMAIN/api/integrations/google/callback`

3. Set `GOOGLE_CLIENT_ID`, `GOOGLE_CLIENT_SECRET`, and `GOOGLE_REDIRECT_URI`.
4. Set `GoogleCalendar__Provider=Google`.
5. Open `/api/integrations/google/authorize` once while signed in as the business owner.
6. Confirm `/api/integrations/status` shows a stored grant.
7. Book a QV/HH/HR, reply `YES`, and verify one calendar event is created.

## Twilio webhook setup

1. Buy or assign a **dedicated business number**. Do not use the owner’s personal cell as the REIGN From-number.
2. Set `TWILIO_ACCOUNT_SID`, `TWILIO_AUTH_TOKEN`, `TWILIO_PHONE_NUMBER`.
3. Set `Sms__Provider=Twilio`.
4. Point the Twilio inbound webhook (HTTP POST) at:

`https://YOUR_DOMAIN/api/sms/webhooks/twilio`

5. Twilio must be able to validate `X-Twilio-Signature`. Invalid signatures return 401/403.
6. Send a test SMS: lookup, memory, reply, outbound send should all succeed.

## Health

`GET /health` reports API status, database connectivity, and whether Groq / SMS / calendar credentials are present. It never returns secret values.
