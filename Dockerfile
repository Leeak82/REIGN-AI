# REIGN AI production container
# Build from the repository root. The same image can run either the API (default)
# or the Blazor operator dashboard when REIGN_SERVICE=web.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

# Restore project graphs first for better layer caching.
COPY REIGN.API/REIGN.API.csproj REIGN.API/
COPY REIGN.Web/REIGN.Web.csproj REIGN.Web/
COPY REIGN.Core/REIGN.Core.csproj REIGN.Core/
COPY REIGN.Data/REIGN.Data.csproj REIGN.Data/
RUN dotnet restore REIGN.API/REIGN.API.csproj \
    && dotnet restore REIGN.Web/REIGN.Web.csproj

COPY . .
RUN dotnet publish REIGN.API/REIGN.API.csproj -c Release -o /app/api --no-restore \
    && dotnet publish REIGN.Web/REIGN.Web.csproj -c Release -o /app/web --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

USER root
RUN mkdir -p /app/api /app/web /data && chown -R $APP_UID /app /data
USER $APP_UID

ENV ASPNETCORE_HTTP_PORTS=8080
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_SYSTEM_NET_DISABLEIPV6=1
EXPOSE 8080

COPY --from=build /app/api /app/api
COPY --from=build /app/web /app/web

ENTRYPOINT ["/bin/sh", "-c", "if [ \"${REIGN_SERVICE:-api}\" = \"web\" ]; then cd /app/web && exec dotnet REIGN.Web.dll; else cd /app/api && exec dotnet REIGN.API.dll; fi"]
