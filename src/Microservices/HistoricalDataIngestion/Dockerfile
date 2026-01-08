FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

COPY Shared/DataIngestion.Contracts.csproj Shared/
COPY HistoricalDataIngestion/DataIngestion.HistoricalService.csproj HistoricalDataIngestion/
RUN dotnet restore HistoricalDataIngestion/DataIngestion.HistoricalService.csproj

COPY Shared/ Shared/
COPY HistoricalDataIngestion/ HistoricalDataIngestion/
RUN dotnet publish HistoricalDataIngestion/DataIngestion.HistoricalService.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
WORKDIR /app

RUN adduser -D -u 1000 appuser && \
    apk add --no-cache curl && \
    mkdir -p /app/data && chown -R appuser:appuser /app

COPY --from=build /app/publish .
RUN chown -R appuser:appuser /app

USER appuser
EXPOSE 5004

ENV ASPNETCORE_URLS=http://+:5004
ENTRYPOINT ["dotnet", "DataIngestion.HistoricalService.dll"]
