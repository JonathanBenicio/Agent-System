# Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/AgenticSystem.Core/AgenticSystem.Core.csproj src/AgenticSystem.Core/
COPY src/AgenticSystem.Infrastructure/AgenticSystem.Infrastructure.csproj src/AgenticSystem.Infrastructure/
COPY src/AgenticSystem.Api/AgenticSystem.Api.csproj src/AgenticSystem.Api/
RUN dotnet restore src/AgenticSystem.Api/AgenticSystem.Api.csproj

COPY src/ src/
RUN dotnet publish src/AgenticSystem.Api/AgenticSystem.Api.csproj -c Release -o /app/publish --no-restore

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_EnableDiagnostics=0

COPY --from=build /app/publish .

RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser
USER appuser

HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
  CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "AgenticSystem.Api.dll"]
