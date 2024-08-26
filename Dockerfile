FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["JsonToPdf.csproj", "./"]
RUN dotnet restore "JsonToPdf.csproj"

COPY . .
RUN dotnet build "JsonToPdf.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "JsonToPdf.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .

RUN mkdir -p /app/data /app/wwwroot/images
COPY wwwroot/images/logo.png /app/wwwroot/images/logo.png

RUN apt-get update && apt-get install -y openssl
RUN openssl req -x509 -newkey rsa:4096 -keyout /app/cert.key -out /app/cert.crt -days 365 -nodes -subj "/CN=localhost"
RUN openssl pkcs12 -export -out /app/cert.pfx -inkey /app/cert.key -in /app/cert.crt -passout pass:YourSecurePassword

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS="http://+:8081;https://+:8444"
ENV ASPNETCORE_Kestrel__Certificates__Default__Path=/app/cert.pfx
ENV ASPNETCORE_Kestrel__Certificates__Default__Password=YourSecurePassword

EXPOSE 8081
EXPOSE 8444

RUN adduser --disabled-password --gecos '' appuser
USER appuser

ENTRYPOINT ["dotnet", "JsonToPdf.dll"]
