# Multi-stage build (skill Standard 11). Build context = repo root.
# Automation is a WebApplication-hosted worker (exposes /health like the API services).
# Pin the SDK to the global.json version so container builds use the same analyzers
# as local (a floating sdk:9.0 tag drifts to a newer SDK and breaks reproducibility).
FROM mcr.microsoft.com/dotnet/sdk:9.0.313 AS build
WORKDIR /src

# Central build/version files first for layer caching (central package management).
# .editorconfig carries the analyzer severity tuning (e.g. CA1000=none) — without it the
# build reverts to default severities and TreatWarningsAsErrors fails the publish.
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./

# Project files only — restore layer is cached unless a .csproj changes.
COPY src/services/Automation/NexConvo.Automation.Worker/NexConvo.Automation.Worker.csproj src/services/Automation/NexConvo.Automation.Worker/
COPY src/services/Automation/NexConvo.Automation.Application/NexConvo.Automation.Application.csproj src/services/Automation/NexConvo.Automation.Application/
COPY src/services/Automation/NexConvo.Automation.Domain/NexConvo.Automation.Domain.csproj src/services/Automation/NexConvo.Automation.Domain/
COPY src/services/Automation/NexConvo.Automation.Infrastructure/NexConvo.Automation.Infrastructure.csproj src/services/Automation/NexConvo.Automation.Infrastructure/
COPY src/shared/NexConvo.BuildingBlocks.Domain/NexConvo.BuildingBlocks.Domain.csproj src/shared/NexConvo.BuildingBlocks.Domain/
COPY src/shared/NexConvo.BuildingBlocks.Application/NexConvo.BuildingBlocks.Application.csproj src/shared/NexConvo.BuildingBlocks.Application/
COPY src/shared/NexConvo.BuildingBlocks.Infrastructure/NexConvo.BuildingBlocks.Infrastructure.csproj src/shared/NexConvo.BuildingBlocks.Infrastructure/
COPY src/shared/NexConvo.Contracts/NexConvo.Contracts.csproj src/shared/NexConvo.Contracts/
RUN dotnet restore src/services/Automation/NexConvo.Automation.Worker/NexConvo.Automation.Worker.csproj

COPY src/ src/
RUN dotnet publish src/services/Automation/NexConvo.Automation.Worker/NexConvo.Automation.Worker.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "NexConvo.Automation.Worker.dll"]
