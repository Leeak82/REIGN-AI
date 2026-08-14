# REIGN AI Host Deployment

Use this after `DEPLOYMENT.md`. Secrets stay in the host environment, never in git.

Container image (all three hosts):

```bash
docker build -t reign-api -f REIGN.API/Dockerfile .
```

Native publish:

```bash
dotnet publish REIGN.API/REIGN.API.csproj -c Release -o ./publish
```

| Item | Value |
| --- | --- |
| Build (Docker) | `docker build -t reign-api -f REIGN.API/Dockerfile .` (repo root) |
| Start (Docker) | `docker run --env-file .env -p 8080:8080 reign-api` |
| Start (published) | `dotnet ./publish/REIGN.API.dll` |
| Default port | `8080` (`PORT` or `WEBSITES_PORT` overrides) |
| Health check | `GET /health` |

`GET /health` returns API status, database connectivity, and whether Groq / SMS / calendar credentials are present. It never returns secret values.

## Environment variables

Required for live traffic (empty values keep Simulated SMS/calendar and Groq fallback):

```
GROQ_API_KEY
GOOGLE_CLIENT_ID
GOOGLE_CLIENT_SECRET
GOOGLE_REDIRECT_URI=https://YOUR_API_HOST/api/integrations/google/callback
TWILIO_ACCOUNT_SID
TWILIO_AUTH_TOKEN
TWILIO_PHONE_NUMBER
ConnectionStrings__Reign=Data Source=/data/REIGN.db
SMS_PROVIDER=Twilio
Sms__Provider=Twilio
GoogleCalendar__Provider=Google
CORS_ALLOWED_ORIGINS=https://YOUR_WEB_HOST
REIGN_API_BASE_URL=https://YOUR_API_HOST/
```

Do not set `CORS_ALLOWED_ORIGINS=*`. Production CORS allows only explicit https origins. Local development still allows localhost automatically.

Persist SQLite on a volume. Without a durable `ConnectionStrings__Reign` path the database resets when the container is replaced.

---

## Azure App Service

Recommended: Linux Web App for Containers, image built from `REIGN.API/Dockerfile`.

**Build command**

```bash
docker build -t reign-api -f REIGN.API/Dockerfile .
```

Push the image to Azure Container Registry, then assign it to the Web App.

**Start command**

Leave empty. The image entrypoint is `dotnet REIGN.API.dll`.

If you publish without Docker:

```
dotnet REIGN.API.dll --urls http://0.0.0.0:8080
```

**Port**

Set `WEBSITES_PORT=8080` (or `PORT=8080`). The process listens on that port.

**Health check URL**

`/health`

In Azure: App settings → Health check path = `/health`.

**App settings**

Set the environment variables listed above in Configuration → Application settings. Mark secrets as slot-sticky as needed. Do not put them in `appsettings.json`.

**Notes**

- Mount persistent storage for SQLite (`/data`) or the database will be lost on restart.
- Add the production Google redirect URI: `https://YOUR_API_HOST/api/integrations/google/callback`.
- Allow outbound HTTPS to `api.groq.com`, `oauth2.googleapis.com`, `www.googleapis.com`, and Twilio.

---

## Render

Recommended: Docker web service. Dockerfile path `REIGN.API/Dockerfile`, build context repository root.

**Build command**

Docker: leave the native build command empty. Render builds the Dockerfile.

Native (if you do not use Docker):

```bash
dotnet publish REIGN.API/REIGN.API.csproj -c Release -o ./publish
```

**Start command**

Docker: leave empty (image entrypoint).

Native:

```bash
dotnet ./publish/REIGN.API.dll --urls http://0.0.0.0:$PORT
```

**Port**

Docker: set the service port to `8080`.

Native: Render injects `PORT`; the API honors it.

**Health check URL**

`/health`

**Environment**

Add the variables from the table above in the Render dashboard. Attach a persistent disk at `/data` and set:

```
ConnectionStrings__Reign=Data Source=/data/REIGN.db
```

---

## Railway

Recommended: Dockerfile builder, `REIGN.API/Dockerfile`.

**Build command**

Railway builds the Dockerfile. No extra build command.

Native:

```bash
dotnet publish REIGN.API/REIGN.API.csproj -c Release -o ./publish
```

**Start command**

Docker: image entrypoint `dotnet REIGN.API.dll`.

Native:

```bash
dotnet ./publish/REIGN.API.dll --urls http://0.0.0.0:$PORT
```

**Port**

Railway injects `PORT`. The API listens on it. Default image port is `8080` when `PORT` is unset.

**Health check URL**

`/health`

Set Railway healthcheck path to `/health`.

**Variables**

Add the environment variables in the Railway service variables UI. Use a volume for `/data` and point `ConnectionStrings__Reign` at it.

---

## After the host is up

1. `curl https://YOUR_API_HOST/health`
2. Confirm startup logs: `REIGN startup status: database=... groq=... sms=... calendar=...` — never secret values
3. Follow `PRODUCTION-LAUNCH-CHECKLIST.md`
