# Miss Reign Operator Handoff

## Current production status

The production REIGN API is live at:

- https://reign-ai-3.onrender.com
- Health: https://reign-ai-3.onrender.com/health
- Swagger/API reference: https://reign-ai-3.onrender.com/swagger/index.html

Current verified integrations:

- SMS provider: SkipCalls
- Customer SMS number: +1 (813) 638-0375
- Google Calendar: connected
- Calendar account: j.collins2491@gmail.com
- Database: connected

## Important: operator dashboard is not ready for handoff yet

Do not use `reign-web.onrender.com` as the production dashboard. That Render service is currently misconfigured to build the API container and its latest deploys fail because the API database configuration is not present on that service.

The REIGN.Web project contains the intended operator dashboard (Inbox, Calendar, Customers, Integrations, Services, AI, Business, Activity), but it must be deployed separately and pointed at the production API.

## Important: there is no admin login yet

REIGN.Web currently has no authentication/authorization middleware or login page. There is no valid admin username/password to share yet.

Before giving the dashboard to an operator, add authentication and role-based access, then deploy the web app behind it.

## Intended Miss Reign responsibilities

Once dashboard auth/deployment is complete, Miss Reign should be able to:

- Watch incoming customer conversations and REIGN replies
- Take over a conversation when human help is needed
- Resume AI handling after owner intervention
- Review customers and conversation history
- Review, confirm, reschedule, and cancel appointments
- Verify Google Calendar synchronization
- Review service catalog and business profile information
- Monitor integration health and provider failures

## Security rules

- Never put API keys, SMS tokens, database passwords, or Google client secrets in GitHub.
- Do not share Render environment values in screenshots or messages.
- Do not give operators direct Render access unless they are expected to manage deployments/secrets.
- The public API URL is not a substitute for an authenticated operator dashboard.

## Developer references

- README.md — system overview
- DEPLOYMENT.md — production configuration
- HOSTING.md — hosting notes
- PRODUCTION-LAUNCH-CHECKLIST.md — production checklist
- PRODUCTION-POLISH-CHECKPOINT.md — latest polish/verification notes
