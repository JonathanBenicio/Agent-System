# Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/AgenticSystem.Core/AgenticSystem.Core.csproj src/AgenticSystem.Core/
COPY src/AgenticSystem.Infrastructure/AgenticSystem.Infrastructure.csproj src/AgenticSystem.Infrastructure/
COPY src/AgenticSystem.Api/AgenticSystem.Api.csproj src/AgenticSystem.Api/
RUN dotnet restore src/AgenticSystem.Api/AgenticSystem.Api.csproj

COPY src/ src/
RUN dotnet publish src/AgenticSystem.Api/AgenticSystem.Api.csproj -c Release -o /app/publish --no-restore

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_EnableDiagnostics=0

RUN apt-get update && apt-get install -y wget libgssapi-krb5-2 && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

USER app

HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
  CMD wget -q -O /dev/null http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "AgenticSystem.Api.dll"]
