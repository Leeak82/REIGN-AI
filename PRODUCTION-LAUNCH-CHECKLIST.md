# REIGN AI Production Launch Checklist

Catalog is unchanged: QV $150 / under 30 minutes, HH $300 / 30 minutes, HR $500 / 60 minutes.
Do not put secrets in git.

## Before launch

- [ ] Deploy the application (Azure App Service, Render, or Railway — see `HOSTING.md`)
- [ ] Set environment variables (`GROQ_API_KEY`, `GOOGLE_CLIENT_ID`, `GOOGLE_CLIENT_SECRET`, `TWILIO_ACCOUNT_SID`, `TWILIO_AUTH_TOKEN`, `TWILIO_PHONE_NUMBER`, `ConnectionStrings__Reign`, `Sms__Provider=Twilio`, `GoogleCalendar__Provider=Google`, `CORS_ALLOWED_ORIGINS`, `REIGN_API_BASE_URL`)
- [ ] Run migrations (API startup applies EF migrations; confirm a durable SQLite volume so they persist)
- [ ] Verify `GET /health` returns `"status":"healthy"` and `"database":"connected"`
- [ ] Authorize Google Calendar (`/api/integrations/google/authorize`, then confirm `/api/integrations/status`)
- [ ] Configure the Twilio inbound webhook: `https://YOUR_API_HOST/api/sms/webhooks/twilio`
- [ ] Send a test SMS (lookup, memory, reply, outbound send)
- [ ] Verify the scheduling flow: book QV/HH/HR → `YES` confirm → reschedule → cancel; calendar updates only after confirm

## Also confirm

- [ ] Startup logs show `REIGN startup status` with database / Groq / SMS / calendar flags and no secret values
- [ ] `CORS_ALLOWED_ORIGINS` is the production web origin, not `*`
- [ ] Google OAuth redirect URI matches `https://YOUR_API_HOST/api/integrations/google/callback`
- [ ] Twilio From-number is a dedicated REIGN business number, not the owner cell
- [ ] SQLite path is on persistent storage
- [ ] Production domain is live and `REIGN_API_BASE_URL` points at the API origin
