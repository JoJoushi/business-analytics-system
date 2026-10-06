# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first so the layer is cached until a csproj changes
COPY global.json BusinessAnalytics.sln ./
COPY src/BusinessAnalytics.Domain/*.csproj         src/BusinessAnalytics.Domain/
COPY src/BusinessAnalytics.Application/*.csproj    src/BusinessAnalytics.Application/
COPY src/BusinessAnalytics.Infrastructure/*.csproj src/BusinessAnalytics.Infrastructure/
COPY src/BusinessAnalytics.Api/*.csproj            src/BusinessAnalytics.Api/
RUN dotnet restore src/BusinessAnalytics.Api/BusinessAnalytics.Api.csproj

COPY src/ src/
RUN dotnet publish src/BusinessAnalytics.Api/BusinessAnalytics.Api.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# curl is used by the container HEALTHCHECK
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true
EXPOSE 8080

# Run as the non-root user shipped with the .NET 8 images
USER app

HEALTHCHECK --interval=15s --timeout=5s --start-period=20s --retries=3 \
  CMD curl -fsS http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "BusinessAnalytics.Api.dll"]
