# REIGN AI production container
# Build from the repository root so REIGN.API can resolve its project references.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

# Restore the API and its project dependencies first for better layer caching.
COPY REIGN.API/REIGN.API.csproj REIGN.API/
COPY REIGN.Core/REIGN.Core.csproj REIGN.Core/
COPY REIGN.Data/REIGN.Data.csproj REIGN.Data/
RUN dotnet restore REIGN.API/REIGN.API.csproj

# Copy the application source and publish the API.
COPY . .
RUN dotnet publish REIGN.API/REIGN.API.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

WORKDIR /app

# SQLite needs a writable directory. Render can mount a disk at /data.
USER root
RUN mkdir -p /data && chown -R $APP_UID /data
USER $APP_UID

# Render supplies PORT; default to 8080 for local/container use.
ENV ASPNETCORE_HTTP_PORTS=8080
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "REIGN.API.dll"]
