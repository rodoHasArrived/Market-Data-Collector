# =============================================================================
# Market Data Collector - Production Docker Image
# =============================================================================
# Multi-stage build for optimized production deployment
#
# Build:   docker build -t marketdatacollector:latest .
# Run:     docker run -p 8080:8080 -v ./data:/app/data marketdatacollector:latest
# =============================================================================

# -----------------------------------------------------------------------------
# Stage 1: Build
# -----------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build

WORKDIR /src

# Copy solution and project files first for layer caching
COPY MarketDataCollector.sln ./
COPY src/MarketDataCollector/MarketDataCollector.csproj src/MarketDataCollector/
COPY src/MarketDataCollector.Ui/MarketDataCollector.Ui.csproj src/MarketDataCollector.Ui/
COPY tests/MarketDataCollector.Tests/MarketDataCollector.Tests.csproj tests/MarketDataCollector.Tests/
COPY benchmarks/MarketDataCollector.Benchmarks/MarketDataCollector.Benchmarks.csproj benchmarks/MarketDataCollector.Benchmarks/

# Restore dependencies
RUN dotnet restore src/MarketDataCollector/MarketDataCollector.csproj

# Copy source code
COPY src/ src/
COPY tests/ tests/
COPY benchmarks/ benchmarks/

# Build and publish
RUN dotnet publish src/MarketDataCollector/MarketDataCollector.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    -p:PublishSingleFile=false \
    -p:SelfContained=false \
    -p:PublishTrimmed=false

# -----------------------------------------------------------------------------
# Stage 2: Runtime
# -----------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime

# Install required packages
RUN apk add --no-cache \
    tzdata \
    curl \
    && rm -rf /var/cache/apk/*

# Create non-root user for security
RUN addgroup -g 1000 mdc && \
    adduser -u 1000 -G mdc -s /bin/sh -D mdc

WORKDIR /app

# Copy published application
COPY --from=build /app/publish ./

# Copy configuration template
COPY appsettings.sample.json ./appsettings.sample.json

# Create data directory with proper permissions
RUN mkdir -p /app/data /app/logs && \
    chown -R mdc:mdc /app

# Switch to non-root user
USER mdc

# Environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV TZ=UTC
ENV MDC_DATA_ROOT=/app/data

# Expose ports
EXPOSE 8080

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

# Default entrypoint - web dashboard mode
ENTRYPOINT ["dotnet", "MarketDataCollector.dll"]
CMD ["--ui", "--serve-status", "--watch-config"]

# =============================================================================
# Usage Examples:
# =============================================================================
#
# Build the image:
#   docker build -t marketdatacollector:latest .
#
# Run web dashboard:
#   docker run -d -p 8080:8080 \
#     -v $(pwd)/data:/app/data \
#     -v $(pwd)/appsettings.json:/app/appsettings.json:ro \
#     --name mdc marketdatacollector:latest
#
# Run with custom port:
#   docker run -d -p 9000:8080 \
#     -e ASPNETCORE_URLS=http://+:8080 \
#     marketdatacollector:latest
#
# Run backfill:
#   docker run --rm \
#     -v $(pwd)/data:/app/data \
#     marketdatacollector:latest \
#     --backfill --backfill-symbols AAPL,MSFT --backfill-from 2024-01-01
#
# Run with Alpaca credentials:
#   docker run -d -p 8080:8080 \
#     -e ALPACA__KEYID=your-key-id \
#     -e ALPACA__SECRETKEY=your-secret-key \
#     -v $(pwd)/data:/app/data \
#     marketdatacollector:latest
#
# View logs:
#   docker logs -f mdc
#
# =============================================================================
