# REIGN AI — Deployment Status

Checkpoint: production cleanup after architecture consolidation.
Catalog is unchanged: Quick Visit $150 / under 30 minutes, Half Hour $300 / 30 minutes, Hour $500 / 60 minutes.

**Completion: ~94%**

The product code, AI pipeline, memory, scheduling, calendar, SMS abstraction, and dashboard are in place. Remaining work is host configuration, secrets, OAuth consent, and provider webhooks — not application redesign.

## Working features

| Area | Status |
| --- | --- |
| AI conversation | Working. Groq is the live provider; missing/failed Groq calls fall back to the built-in assistant. `ConversationEngine` + `IntentDetectionService` are intact. |
| Memory | Working. `ConversationState` and `CustomerIntentMemory` persist step, intent, selected service, history, and preferences. |
| Scheduling | Working. QV / HH / HR catalog, overlap checks, pending → confirm (`YES`), reschedule, cancel. |
| Calendar | Working as an integration. Simulated by default. Google Calendar create/update/cancel is implemented; live use needs OAuth env + one-time authorize. |
| SMS architecture | Working as an abstraction. Simulated by default. Twilio and Vonage senders + signed webhooks exist; live use needs provider credentials and webhook URLs. |
| Dashboard / UI | Working. Blazor Web: Home, Dashboard, AI, Assistant, Inbox, Integrations. Local Web → API default is `http://localhost:5012/`. |

## Security / git cleanup in this phase

Removed from git tracking:

- `Business-broken-backup.cs`
- `ReignDbContext-broken-backup.cs`
- `CheckServices.cs`
- `REIGN.Web/Components/Pages/Reign.razor.backup`
- `reign_files.txt` / `reign_tree.txt`
- OpenClaw `memory/` runtime notes

`.gitignore` now includes:

```
appsettings.Development.json
appsettings.*.local.json
*.db
*.sqlite
.openclaw/
.env
memory/
```

Committed configuration files contain **empty placeholders only**. Secrets must come from environment variables.

## Required environment variables

Set these in the host secret store. Do not put values in source files.

| Purpose | Variable |
| --- | --- |
| Groq | `GROQ_API_KEY` |
| Google OAuth | `GOOGLE_CLIENT_ID` |
| Google OAuth | `GOOGLE_CLIENT_SECRET` |
| Twilio | `TWILIO_ACCOUNT_SID` |
| Twilio | `TWILIO_AUTH_TOKEN` |
| Twilio From / business number | `TWILIO_PHONE_NUMBER` (alias: `TWILIO_FROM_NUMBER`) |

Also typically required for live traffic:

| Purpose | Variable |
| --- | --- |
| Database | `ConnectionStrings__Reign` or `REIGN_CONNECTION_STRING` |
| SMS provider | `Sms__Provider=Twilio` (or `Vonage`) |
| Google provider | `GoogleCalendar__Provider=Google` |
| Google redirect | `GOOGLE_REDIRECT_URI` |
| Web → API | `REIGN_API_BASE_URL` |
| Dedicated business SMS number | `Sms__BusinessPhoneNumber` / `REIGN_BUSINESS_PHONE` |
| Owner personal number | `Sms__OwnerPhoneNumber` / `REIGN_OWNER_PHONE` |

Local Google callback (do not change):

`https://localhost:5001/api/integrations/google/callback`

Local API URLs:

- `https://localhost:5001`
- `http://localhost:5012`

## Remaining manual steps

1. **Environment variables** — set Groq, Twilio, and Google secrets on the host. Keep `appsettings.json` empty.
2. **OAuth authorization** — create a Google Cloud OAuth client whose redirect URI is `/api/integrations/google/callback`, then open `/api/integrations/google/authorize` once and confirm a stored grant.
3. **SMS provider webhook** — provision a dedicated REIGN business number (not the owner cell). Point Twilio/Vonage inbound HTTPS at `/api/sms/webhooks/twilio` or `/api/sms/webhooks/vonage`.
4. **Hosting** — pick Azure App Service, Render, or Railway (`HOSTING.md`), deploy API + Web, set `REIGN_API_BASE_URL` to the live API origin, set `CORS_ALLOWED_ORIGINS` to the production web origin (never `*`), use durable SQLite, and allow egress to `api.groq.com`, `oauth2.googleapis.com`, `www.googleapis.com`, and the SMS provider.

## Verification (this commit)

- `dotnet clean` succeeded
- `dotnet build` — 0 errors, 0 warnings
- `dotnet test` — 31 passed
- `dotnet ef migrations list` — 21 migrations present; model has no pending changes
- Fresh `dotnet ef database update` creates the full schema without touching production data
- `GET /health` reports API, database, Groq, SMS, and calendar status without secrets
- `REIGN.API/Dockerfile` publishes the API image (`dotnet publish` verified; host Docker to run the image)

Launch checklist: `PRODUCTION-LAUNCH-CHECKLIST.md`

## Production launch checklist

- [ ] No secrets in git
- [ ] `GROQ_API_KEY` set (or fallback accepted)
- [ ] `GOOGLE_CLIENT_ID` / `GOOGLE_CLIENT_SECRET` set if calendar is live
- [ ] `TWILIO_ACCOUNT_SID` / `TWILIO_AUTH_TOKEN` / `TWILIO_PHONE_NUMBER` set if SMS is live
- [ ] Google consent completed
- [ ] Provider webhook signature validation succeeds
- [ ] One live QV/HH/HR booking confirms and appears on the calendar only after `YES`
