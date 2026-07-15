# Multi-stage build (skill Standard 11). Build context = repo root.
# Notification is a WebApplication-hosted worker (exposes /health like the API services).
# Pin the SDK to the global.json version so container builds use the same analyzers
# as local (a floating sdk:9.0 tag drifts to a newer SDK and breaks reproducibility).
FROM mcr.microsoft.com/dotnet/sdk:9.0.313 AS build
WORKDIR /src

# Central build/version files first for layer caching (central package management).
# .editorconfig carries the analyzer severity tuning (e.g. CA1000=none) — without it the
# build reverts to default severities and TreatWarningsAsErrors fails the publish.
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./

# Project files only — restore layer is cached unless a .csproj changes.
COPY src/services/Notification/NexConvo.Notification.Worker/NexConvo.Notification.Worker.csproj src/services/Notification/NexConvo.Notification.Worker/
COPY src/services/Notification/NexConvo.Notification.Application/NexConvo.Notification.Application.csproj src/services/Notification/NexConvo.Notification.Application/
COPY src/services/Notification/NexConvo.Notification.Domain/NexConvo.Notification.Domain.csproj src/services/Notification/NexConvo.Notification.Domain/
COPY src/services/Notification/NexConvo.Notification.Infrastructure/NexConvo.Notification.Infrastructure.csproj src/services/Notification/NexConvo.Notification.Infrastructure/
COPY src/shared/NexConvo.BuildingBlocks.Domain/NexConvo.BuildingBlocks.Domain.csproj src/shared/NexConvo.BuildingBlocks.Domain/
COPY src/shared/NexConvo.BuildingBlocks.Application/NexConvo.BuildingBlocks.Application.csproj src/shared/NexConvo.BuildingBlocks.Application/
COPY src/shared/NexConvo.BuildingBlocks.Infrastructure/NexConvo.BuildingBlocks.Infrastructure.csproj src/shared/NexConvo.BuildingBlocks.Infrastructure/
COPY src/shared/NexConvo.Contracts/NexConvo.Contracts.csproj src/shared/NexConvo.Contracts/
RUN dotnet restore src/services/Notification/NexConvo.Notification.Worker/NexConvo.Notification.Worker.csproj

COPY src/ src/
RUN dotnet publish src/services/Notification/NexConvo.Notification.Worker/NexConvo.Notification.Worker.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/sdk:9.0.313 AS runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "NexConvo.Notification.Worker.dll"]
