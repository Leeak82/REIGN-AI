# Miss Reign Operator Handoff

## Production operator dashboard

Dashboard:

- https://reign-web.onrender.com
- Login: https://reign-web.onrender.com/login

Admin username:

- `missreign`

The admin password is intentionally **not stored in GitHub**. It is configured as a PBKDF2 hash in Render and should be handed to the operator privately.

The dashboard is configured to use the live production API:

- https://reign-ai-3.onrender.com

## Current production status

Verified production services:

- REIGN API: live
- REIGN operator dashboard: live
- Database: connected
- AI provider: configured
- SMS provider: SkipCalls
- Customer SMS number: +1 (813) 638-0375
- Google Calendar: connected
- Calendar account: j.collins2491@gmail.com

The dashboard requires authentication. Unauthenticated operators should use `/login`; authenticated sessions use a secure HTTP-only cookie and expire after 12 hours.

## Miss Reign daily workflow

Start at the dashboard home page, then use the operating sections as needed:

1. **Inbox** — watch incoming customer conversations, review REIGN replies, intervene when human help is needed, and resume AI handling afterward.
2. **Calendar** — review upcoming appointments and pending scheduling work; confirm, reschedule, or cancel when appropriate and verify calendar sync.
3. **Customers** — review customer profiles and conversation history before making customer-service decisions.
4. **Services** — verify the current service catalog and pricing/business information used by REIGN.
5. **Integrations** — check SMS and Google Calendar integration status when messages or scheduling are not behaving normally.
6. **AI / Reign** — review REIGN behavior and operational AI information.
7. **Business / Activity** — review business profile and operational activity.

## What Miss Reign owns operationally

- Monitor the customer inbox and make sure conversations do not die in an unattended queue.
- Take over conversations that need a human decision or customer-service judgment.
- Resume AI handling after the human issue is resolved.
- Review pending appointments and scheduling conflicts.
- Keep customer-facing service information accurate.
- Check integration status if SMS or calendar behavior looks wrong.
- Report reproducible bugs with the customer/conversation/appointment involved and what action failed.

## Important phone/SMS note

The current customer-facing SMS provider is **SkipCalls** and the current live customer SMS number is:

- +1 (813) 638-0375

The Alaska number +1 (907) 300-1244 is not the current customer SkipCalls inbox. It remains associated with the voice/SmsGate fallback path.

## Security rules

- Do not share the admin password publicly or put it in GitHub, screenshots, issue comments, or support tickets.
- Do not put API keys, SMS tokens, database passwords, Google client secrets, or Render environment values in messages or GitHub.
- Do not give ordinary operators direct Render access unless they are expected to manage deployments and secrets.
- Log out when using a shared device: https://reign-web.onrender.com/logout

## If something appears down

Check these first:

- API health: https://reign-ai-3.onrender.com/health
- Dashboard login: https://reign-web.onrender.com/login
- Integrations page inside the authenticated dashboard

If the API health endpoint is healthy but the dashboard cannot load data, treat it as a dashboard/API connectivity issue rather than an SMS-provider outage.

## Developer references

- `README.md` — system overview
- `DEPLOYMENT.md` — production configuration
- `HOSTING.md` — hosting notes
- `PRODUCTION-LAUNCH-CHECKLIST.md` — production checklist
- `PRODUCTION-POLISH-CHECKPOINT.md` — latest polish/verification notes
