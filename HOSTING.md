# REIGN AI hosting — PostgreSQL on Render

Production uses PostgreSQL via Npgsql. Set **only**:

```
ConnectionStrings__Reign=<Postgres connection string>
```

Do not put the connection string in source files.

## Render

1. Dashboard → **New** → **PostgreSQL**. Use the free instance.
2. Open the database → copy **Internal Database URL** (same region as the web service).
3. On the **same** REIGN API web service (not the Postgres addon, not a different service) → **Environment** → **Add Environment Variable**:

| Key | Value |
| --- | --- |
| `ConnectionStrings__Reign` | Supabase or Render Postgres connection string (one line) |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE` | `false` |

Use two underscores: `ConnectionStrings__Reign`. `DATABASE_URL` is also accepted.

Save, then **Manual Deploy**. The Docker image does not contain the database password. If this variable is missing, the container exits immediately.

### Supabase (free Postgres)

Direct `db.<project-ref>.supabase.co:5432` is **IPv6-only** on many projects. Render cannot open that socket (`Network is unreachable` to an address like `2600:1f14:…`).

Set `ConnectionStrings__Reign` to either form. The API rewrites a direct `db.*` host to the **Session pooler** (IPv4, port **5432**) and changes username `postgres` to `postgres.<project-ref>`.

```
Host=db.YOUR_PROJECT.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=YOUR_PASSWORD;SSL Mode=Require
```

Or paste the Session pooler string from Supabase → Project Settings → Database:

```
Host=aws-0-us-west-2.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.YOUR_PROJECT;Password=YOUR_PASSWORD;SSL Mode=Require
```

| Optional override | When to set it |
| --- | --- |
| `SUPABASE_REGION` | Automatic rewrite guessed the wrong region (default `us-west-2`) |
| `SUPABASE_POOLER_HOST` | Use the exact Session pooler hostname from the dashboard |
| `SUPABASE_PROJECT_REF` | Connection string is already the pooler host but username is still `postgres` |

You can omit `ConnectionStrings__Reign` if both of these are set:

| Key | Value |
| --- | --- |
| `SUPABASE_PROJECT_REF` | `ifjgbajbasuoiuozkjox` (from `db.<ref>.supabase.co`) |
| `SUPABASE_DB_PASSWORD` | the database password from Supabase (no quotes) |
| `SUPABASE_POOLER_HOST` | exact Session pooler host from Connect (optional) |

An empty `ConnectionStrings__Reign` is treated as missing. Do not delete the variable unless those two `SUPABASE_*` keys are set.

Do **not** use the Transaction pooler on port **6543** with Entity Framework. The API rewrites 6543 to 5432 (session mode).

If the IPv6 you see in logs starts with `2600:1f14:`, the project is in `us-west-2`.

### Render Postgres

Use the **Internal Database URL** (`postgresql://USER:PASSWORD@dpg-xxxx-a/reign`) if you stay on Render Postgres.
That hostname only works from a Render service in the **same region**.

A `SocketException` / `AwaitableSocketAsyncEventArgs` at startup means the API cannot open a TCP connection to Postgres. Typical causes:

- `ConnectionStrings__Reign` is localhost, a laptop IP, or empty-and-wrong
- Supabase **direct** `db.*:5432` was used and the IPv6 rewrite/region is wrong — set `SUPABASE_REGION`
- The **External** Render URL was used without SSL, or the **Internal** URL was used from a different region
- The Postgres instance is not in the same Render account/region as the API

Fix: save `ConnectionStrings__Reign`, then redeploy. External `*.render.com` URLs are supported with SSL.

4. Redeploy the API. Startup creates the schema from the current EF model, then seeds QV / HH / HR.
5. Confirm `GET /api/health` returns `"status":"ok"` and `"databaseStatus":"configured"`.
   `GET /health` stays HTTP 200 even if the database password is rejected, so Render does not crash-loop. Fix `SUPABASE_DB_PASSWORD` and redeploy once.

You do not need a `/data` disk for PostgreSQL.

## Twilio (live inbound)

Sending a message from the Twilio Console uses Twilio's own API. It does **not** call REIGN. Live customer texts only work when the **phone number** webhook is HTTP POST:

`https://reign-ai-2.onrender.com/api/sms/webhooks/twilio`

Set `TWILIO_ACCOUNT_SID`, `TWILIO_AUTH_TOKEN` (live token, not test), `TWILIO_FROM_NUMBER` (the dedicated Twilio number), and `TWILIO_WEBHOOK_URL` to that same URL. `/api/sms/incoming` is the Development simulator and is disabled in production.

## Docker

```bash
docker build -t reign-api -f Dockerfile .
```

Pass the connection string at runtime:

```bash
docker run -e ASPNETCORE_ENVIRONMENT=Production \
  -e ConnectionStrings__Reign='Host=...;Database=reign;Username=...;Password=...' \
  -p 8080:8080 reign-api
```

## Local development

Leave `ConnectionStrings__Reign` empty. The API uses SQLite `REIGN.db` under the API content root. Existing SQLite migrations still apply locally and in tests.

To point local at Postgres:

```
ConnectionStrings__Reign=Host=localhost;Port=5432;Database=reign;Username=postgres;Password=postgres
```

## Other hosts

Azure and Railway notes (ports, health path) are in `DEPLOYMENT.md`. Production database on those hosts should still be PostgreSQL via `ConnectionStrings__Reign`, not SQLite `/data/REIGN.db`.
