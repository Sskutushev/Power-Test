# Multi-stage build: the SDK never reaches the runtime image.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first so the layer is reused whenever only sources change.
# .editorconfig is part of the build contract: analyzer severities live there, so omitting it would
# make the container build enforce different rules than the local one.
COPY Directory.Build.props Directory.Packages.props global.json .editorconfig ./
COPY src/ ./src/
RUN dotnet restore src/Weather.Web/Weather.Web.csproj

RUN dotnet publish src/Weather.Web/Weather.Web.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    -p:DebugType=none

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0

# curl exists solely for the container health probe.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# Data protection keys live on a mounted volume so the root filesystem can stay read-only.
RUN mkdir -p /keys && chown -R $APP_UID:$APP_UID /keys /app
ENV Security__DataProtectionKeyPath=/keys

USER $APP_UID
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
    CMD curl -fsS http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "Weather.Web.dll"]
