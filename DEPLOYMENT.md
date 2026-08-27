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
SMS_PROVIDER=Twilio
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

### Production database location

SQLite file must live **outside the container filesystem** that gets replaced on deploy.

| Environment | `ConnectionStrings__Reign` | Storage |
| --- | --- | --- |
| Local | unset or `Data Source=REIGN.db` | API content root (gitignored) |
| Docker / Render / Railway / Azure | `Data Source=/data/REIGN.db` | Mount a persistent volume at `/data` |

Do not point production at a path inside `/app`. Startup runs `Database.MigrateAsync()` then additive SQLite `CREATE TABLE IF NOT EXISTS` / `ADD COLUMN` guards. That is safe to re-run; it does not drop customer data.

5. Set `REIGN_API_BASE_URL` on REIGN.Web to the public API origin.
6. Confirm `GET /health` returns `"status":"healthy"` and `"database":"connected"`.
7. Confirm startup logs say Groq / Twilio / Google credentials are present — never the secret values.

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
   Production shortcut: `https://reign-ai-2.onrender.com/api/integrations/google/authorize`.
   The dashboard **Connect Google Calendar** / **Reconnect Google Calendar** buttons on Calendar and Integrations hit the same URL.
6. Confirm `/api/integrations/status` shows `hasStoredGrant: true`, `activeProvider: Google`, `calendarId: j.collins2491@gmail.com`, and a **https** `redirectUri` on `reign-ai-2.onrender.com` (never `localhost`).
7. Book a QV/HH/HR, reply `YES`, and verify one calendar event is created on j.collins2491@gmail.com. Confirmed appointments reuse `ExternalCalendarEventId` so Google does not get a duplicate event.

On Render, leftover `https://localhost:5001/...` from `appsettings.json` used to be rewritten to `http://localhost:8080/...` because the API image listens on 8080. Production now prefers `RENDER_EXTERNAL_URL` / `RENDER_EXTERNAL_HOSTNAME` (and the public `X-Forwarded-Host`) so authorize and token exchange use:

`https://reign-ai-2.onrender.com/api/integrations/google/callback`

Still set these on the API service (not secrets, but required for the Collins calendar):

| Key | Value |
| --- | --- |
| `GoogleCalendar__Provider` | `Google` |
| `GOOGLE_CALENDAR_ID` | `j.collins2491@gmail.com` |
| `GoogleCalendar__CalendarId` | `j.collins2491@gmail.com` |
| `GOOGLE_CALENDAR_TIMEZONE` | `America/Los_Angeles` |
| `GOOGLE_REDIRECT_URI` | `https://reign-ai-2.onrender.com/api/integrations/google/callback` |
| `GoogleCalendar__RedirectUri` | same public callback |
| `REIGN_DOCKER` | unset (never `1` on Render) |

In Google Cloud Console, the OAuth **Web** client's authorized redirect URIs must include that same production callback. The live client id is already configured on Render (`GOOGLE_CLIENT_ID`).

`GET /api/integrations/status` now includes `oauthClientId` (public) and `oauthClientSecretLooksLikeWeb` (true when the secret starts with `GOCSPX-` after trimming quotes). `oauthClientConfigured: true` only means a secret is **present**. If consent succeeds and the callback still returns `invalid_client`, paste the **Client secret** from that same Web client into Render as `GOOGLE_CLIENT_SECRET` (or `GoogleCalendar__ClientSecret`), then redeploy. Do not put the secret in git.

Authorize and the token POST use the same canonical callback. If exchange fails, the JSON includes Google's `error` / `error_description` (never the secret) plus the `redirectUri` that was sent.

Do not complete consent as `lee.anthony57@gmail.com`. Cursor Calendar MCP is that account and cannot write to Collins' calendar until Collins shares it. REIGN must receive Collins' own OAuth grant.

## Twilio webhook setup

1. Buy or assign a **dedicated business number**. Do not use the owner’s personal cell as the REIGN From-number.
2. Set `TWILIO_ACCOUNT_SID`, `TWILIO_AUTH_TOKEN`, `TWILIO_PHONE_NUMBER`.
3. Set `Sms__Provider=Twilio`.
4. Point the Twilio inbound webhook (HTTP POST) at:

`https://YOUR_DOMAIN/api/sms/incoming`

5. Twilio signs the **public URL it POSTed to**. Behind Render, the API reconstructs that URL from `TWILIO_WEBHOOK_URL`, `Sms__PublicBaseUrl` / `RENDER_EXTERNAL_URL`, and `X-Forwarded-*`. Invalid signatures return **403** (not 500). `TWILIO_WEBHOOK_URL` must be exactly `https://YOUR_HOST/api/sms/incoming`. `/api/sms/webhooks/twilio` remains a compatible alias.
6. Text the Twilio number from a real phone. Sending SMS from the Twilio Console **Send a message** box only uses Twilio's API — it never hits REIGN and is not a live webhook test.

The Twilio phone number (or Messaging Service) **A Message Comes In** webhook must be:

- Method: **HTTP POST**
- Content-Type: `application/x-www-form-urlencoded` (Twilio default: `From`, `To`, `Body`, `MessageSid`)
- URL: `https://reign-ai-2.onrender.com/api/sms/incoming`

JSON POST `/api/sms/incoming` is still the Development simulator and stays disabled in production.

Set on the API service:

| Key | Value |
| --- | --- |
| `Sms__Provider` | `Twilio` |
| `TWILIO_ACCOUNT_SID` | live Account SID (same project as the number) |
| `TWILIO_AUTH_TOKEN` | live Auth Token (must match the SID; test tokens fail live signatures) |
| `TWILIO_FROM_NUMBER` | the dedicated Twilio number in E.164 (`+1…`), not the owner cell |
| `TWILIO_WEBHOOK_URL` | `https://reign-ai-2.onrender.com/api/sms/incoming` |
| `Sms__BusinessPhoneNumber` | same dedicated Twilio number |
| `Sms__OwnerPhoneNumber` | owner personal cell (never used as From) |

If inbound shows 403 in Twilio Debugger, the signed URL did not match. Confirm the webhook URL above, then check API logs for `Tried N public URL candidates`. If inbound is 200 but there is no reply, the From number is not a Twilio number or Twilio rejected the outbound send — logs include `outbound send failed`.

## SmsGate (Android, open source)

Free path for unknown customers: a dedicated Android phone + SIM running [SMS Gateway for Android](https://sms-gate.app/). The dedicated REIGN business number is the Straight Talk SIM **+19073001244**. Set `Sms__Provider=SmsGate`, `SMSGATE_USERNAME`, `SMSGATE_PASSWORD`, `SMSGATE_SIGNING_KEY`, and `SMSGATE_FROM_NUMBER=+19073001244`. Register HTTP POST `https://YOUR_HOST/api/sms/webhooks/smsgate` as the `sms:received` webhook. Details are in `HOSTING.md`.

## Health

`GET /health` reports API status, database connectivity, and whether Groq / SMS / calendar credentials are present. It never returns secret values.
