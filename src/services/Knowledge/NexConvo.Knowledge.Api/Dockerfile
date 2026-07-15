# Multi-stage build (skill Standard 11). Build context = repo root.
# Pin the SDK to the global.json version so container builds use the same analyzers
# as local (a floating sdk:9.0 tag drifts to a newer SDK and breaks reproducibility).
FROM mcr.microsoft.com/dotnet/sdk:9.0.313 AS build
WORKDIR /src

# Central build/version files first for layer caching (central package management).
# .editorconfig carries the analyzer severity tuning (e.g. CA1000=none) — without it the
# build reverts to default severities and TreatWarningsAsErrors fails the publish.
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./

# Project files only — restore layer is cached unless a .csproj changes.
COPY src/services/Knowledge/NexConvo.Knowledge.Api/NexConvo.Knowledge.Api.csproj src/services/Knowledge/NexConvo.Knowledge.Api/
COPY src/services/Knowledge/NexConvo.Knowledge.Application/NexConvo.Knowledge.Application.csproj src/services/Knowledge/NexConvo.Knowledge.Application/
COPY src/services/Knowledge/NexConvo.Knowledge.Domain/NexConvo.Knowledge.Domain.csproj src/services/Knowledge/NexConvo.Knowledge.Domain/
COPY src/services/Knowledge/NexConvo.Knowledge.Infrastructure/NexConvo.Knowledge.Infrastructure.csproj src/services/Knowledge/NexConvo.Knowledge.Infrastructure/
COPY src/shared/NexConvo.BuildingBlocks.Domain/NexConvo.BuildingBlocks.Domain.csproj src/shared/NexConvo.BuildingBlocks.Domain/
COPY src/shared/NexConvo.BuildingBlocks.Application/NexConvo.BuildingBlocks.Application.csproj src/shared/NexConvo.BuildingBlocks.Application/
COPY src/shared/NexConvo.BuildingBlocks.Infrastructure/NexConvo.BuildingBlocks.Infrastructure.csproj src/shared/NexConvo.BuildingBlocks.Infrastructure/
COPY src/shared/NexConvo.BuildingBlocks.Ai/NexConvo.BuildingBlocks.Ai.csproj src/shared/NexConvo.BuildingBlocks.Ai/
COPY src/shared/NexConvo.Contracts/NexConvo.Contracts.csproj src/shared/NexConvo.Contracts/
RUN dotnet restore src/services/Knowledge/NexConvo.Knowledge.Api/NexConvo.Knowledge.Api.csproj

COPY src/ src/
RUN dotnet publish src/services/Knowledge/NexConvo.Knowledge.Api/NexConvo.Knowledge.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/sdk:9.0.313 AS runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "NexConvo.Knowledge.Api.dll"]
