# REIGN AI hosting — PostgreSQL on Render

Production uses PostgreSQL via Npgsql. Set **only**:

```
ConnectionStrings__Reign=<Render Internal Database URL>
```

Do not put the connection string in source files.

## Render

1. Dashboard → **New** → **PostgreSQL**. Use the free instance.
2. Open the database → copy **Internal Database URL** (same region as the web service).
3. On the REIGN API service → **Environment**:

```
ASPNETCORE_ENVIRONMENT=Production
DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false
ConnectionStrings__Reign=<Internal Database URL>
```

Internal URLs look like `postgresql://USER:PASSWORD@dpg-xxxx-a/reign`.
External URLs (`.render.com`) also work; the app enables SSL for `render.com` hosts.

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
