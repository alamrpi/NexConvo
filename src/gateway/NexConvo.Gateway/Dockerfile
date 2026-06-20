# Multi-stage build (skill Standard 11). Build context = repo root.
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Central build/version files first for layer caching.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/gateway/NexConvo.Gateway/NexConvo.Gateway.csproj src/gateway/NexConvo.Gateway/
COPY src/shared/NexConvo.BuildingBlocks/NexConvo.BuildingBlocks.csproj src/shared/NexConvo.BuildingBlocks/
RUN dotnet restore src/gateway/NexConvo.Gateway/NexConvo.Gateway.csproj

COPY src/ src/
RUN dotnet publish src/gateway/NexConvo.Gateway/NexConvo.Gateway.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "NexConvo.Gateway.dll"]
