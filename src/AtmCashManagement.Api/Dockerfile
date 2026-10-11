# Build context is the repository root (see docker-compose.yml).

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

# Restore first, using only project files, so the package layer is cached until a csproj changes.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/AtmCashManagement.Domain/AtmCashManagement.Domain.csproj src/AtmCashManagement.Domain/
COPY src/AtmCashManagement.Application/AtmCashManagement.Application.csproj src/AtmCashManagement.Application/
COPY src/AtmCashManagement.Infrastructure/AtmCashManagement.Infrastructure.csproj src/AtmCashManagement.Infrastructure/
COPY src/AtmCashManagement.Api/AtmCashManagement.Api.csproj src/AtmCashManagement.Api/
RUN dotnet restore src/AtmCashManagement.Api/AtmCashManagement.Api.csproj

COPY src/ src/
RUN dotnet publish src/AtmCashManagement.Api/AtmCashManagement.Api.csproj --configuration Release --no-restore --output /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
# Npgsql probes for GSSAPI on connect; without this library Alpine logs a loader error.
RUN apk add --no-cache krb5-libs
WORKDIR /app
COPY --from=build /app .

# Built-in non-root user of the official .NET images.
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

HEALTHCHECK --interval=10s --timeout=3s --start-period=20s --retries=5 \
  CMD wget --quiet --spider http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "AtmCashManagement.Api.dll"]
