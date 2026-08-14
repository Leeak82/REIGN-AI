FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY REIGN.API/REIGN.API.csproj REIGN.API/
COPY REIGN.Core/REIGN.Core.csproj REIGN.Core/
COPY REIGN.Data/REIGN.Data.csproj REIGN.Data/
RUN dotnet restore REIGN.API/REIGN.API.csproj

COPY REIGN.API/ REIGN.API/
COPY REIGN.Core/ REIGN.Core/
COPY REIGN.Data/ REIGN.Data/
RUN dotnet publish REIGN.API/REIGN.API.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
ENTRYPOINT ["dotnet", "REIGN.API.dll"]
