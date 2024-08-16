# Verwenden des .NET Core ASP.NET Runtime-Bildes für die Basis
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

# Verwenden des .NET Core SDK-Bildes für den Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY JsonToPdf/JsonToPdf.csproj .
RUN dotnet restore "JsonToPdf.csproj"

COPY . .

# Stellen Sie sicher, dass das Verzeichnis für den Build-Prozess existiert
RUN mkdir -p /app/build && chmod -R 777 /app/build

# Führen Sie den Build- und Publish-Befehl als root-Benutzer aus
USER root
RUN dotnet publish "JsonToPdf.csproj" -c Release -o /app/build /p:UseAppHost=false

# Finales Image, das das veröffentlichte Projekt enthält
FROM base AS final
WORKDIR /app
COPY --from=build /app/build .
COPY JsonToPdf/wwwroot/images/logo.png /app/wwwroot/images/logo.png

# Setzen der Umgebungsvariablen
ENV ASPNETCORE_ENVIRONMENT=Production

# Erstellen Sie einen nicht-root Benutzer
RUN adduser --disabled-password --gecos '' appuser
USER appuser

ENTRYPOINT ["dotnet", "JsonToPdf.dll"]