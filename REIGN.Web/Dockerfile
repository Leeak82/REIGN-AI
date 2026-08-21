# REIGN AI dashboard container
# Build from the repository root: docker build -t reign-web -f REIGN.Web/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY REIGN.Web/REIGN.Web.csproj REIGN.Web/
COPY REIGN.Core/REIGN.Core.csproj REIGN.Core/
RUN dotnet restore REIGN.Web/REIGN.Web.csproj

COPY . .
RUN dotnet publish REIGN.Web/REIGN.Web.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "REIGN.Web.dll"]
