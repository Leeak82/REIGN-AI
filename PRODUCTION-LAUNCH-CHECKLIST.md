# REIGN AI Production Launch Checklist

Catalog is unchanged: QV $150 / under 30 minutes, HH $300 / 30 minutes, HR $500 / 60 minutes.
Do not put secrets in git.

## Before launch

- [ ] Deploy the application (Azure App Service, Render, or Railway — see `HOSTING.md`)
- [ ] Set environment variables (`GROQ_API_KEY`, `GOOGLE_CLIENT_ID`, `GOOGLE_CLIENT_SECRET`, `SKIPCALLS_TOKEN`, `SKIPCALLS_FROM_NUMBER=+18136380375`, `ConnectionStrings__Reign`, `Sms__Provider=SkipCalls`, `Sms__BusinessPhoneNumber=+18136380375`, `GoogleCalendar__Provider=Google`, `CORS_ALLOWED_ORIGINS`, `REIGN_API_BASE_URL`)
- [ ] Verify database initialization (production PostgreSQL schema and upgrade guards run at startup; EF migrations apply to SQLite only)
- [ ] Verify `GET /health` returns `"status":"healthy"` and `"database":"connected"`; HTTP 200 alone also permits a degraded database
- [ ] Authorize Google Calendar (`/api/integrations/google/authorize`, then confirm `/api/integrations/status`)
- [ ] Configure the SkipCalls inbound webhook: `https://YOUR_API_HOST/api/sms/webhooks/skipcalls` (HTTP POST)
- [ ] Send a real test SMS to `+18136380375` and verify lookup, memory, REIGN reply, and outbound send
- [ ] Verify the scheduling flow: book QV/HH/HR → `YES` confirm → reschedule → cancel; calendar updates only after confirm

## Also confirm

- [ ] Startup logs show `REIGN startup status` with database / Groq / SMS / calendar flags and no secret values
- [ ] `CORS_ALLOWED_ORIGINS` is the production web origin, not `*`
- [ ] Google OAuth redirect URI matches `https://YOUR_API_HOST/api/integrations/google/callback`
- [ ] SkipCalls business number is `+18136380375`; `+19073001244` is voice forwarding / optional SmsGate fallback only
- [ ] Production PostgreSQL connection is configured; if explicitly using SQLite, its path is on persistent storage
- [ ] Production domain is live and `REIGN_API_BASE_URL` points at the API origin
