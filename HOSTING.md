# REIGN AI hosting — PostgreSQL on Render

Production uses PostgreSQL via Npgsql. Set **only**:

```
ConnectionStrings__Reign=<Render Internal Database URL>
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

On the API service, set `ConnectionStrings__Reign` to the Supabase URI or Npgsql form. Do not commit it.

```
Host=db.YOUR_PROJECT.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=YOUR_PASSWORD;SSL Mode=Require
```

If Render still throws a socket error, use Supabase **Session pooler** (port **6543**) from Project Settings → Database. Direct `db.*.supabase.co:5432` is IPv6-only on some projects and fails from Render.

Use the **Internal Database URL** (`postgresql://USER:PASSWORD@dpg-xxxx-a/reign`) if you stay on Render Postgres.
That hostname only works from a Render service in the **same region**.

A `SocketException` / `AwaitableSocketAsyncEventArgs` at startup means the API cannot open a TCP connection to Postgres. Typical causes:

- `ConnectionStrings__Reign` is localhost, a laptop IP, or empty-and-wrong
- The **External** URL was used without SSL, or the **Internal** URL was used from a different region
- The Postgres instance is not in the same Render account/region as the API

Fix: paste the Internal Database URL into `ConnectionStrings__Reign`, save env, and redeploy. External `*.render.com` URLs are supported with SSL.

4. Redeploy the API. Startup creates the schema from the current EF model, then seeds QV / HH / HR.
5. Confirm `GET /api/health` returns `"status":"ok"` and `"databaseStatus":"configured"`.

You do not need a `/data` disk for PostgreSQL.

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
