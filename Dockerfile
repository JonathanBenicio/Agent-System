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

# Install Node.js (required for running npx-based MCP servers) and basic utilities
RUN apt-get update && \
    apt-get install -y wget libgssapi-krb5-2 curl && \
    curl -fsSL https://deb.nodesource.com/setup_20.x | bash - && \
    apt-get install -y nodejs && \
    rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# Copiar modelos ML/ONNX para o runtime
COPY fastpath_model.zip .
COPY fastpath_model.onnx .
# COPY embeddings_model.onnx . (Descomentar quando o arquivo existir)
# COPY reranker_model.onnx . (Descomentar quando o arquivo existir)

RUN mkdir -p models/rerank models/embeddings wwwroot/onnx-models && chown -R app:app /app

USER app

HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
  CMD wget -q -O /dev/null http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "AgenticSystem.Api.dll"]
