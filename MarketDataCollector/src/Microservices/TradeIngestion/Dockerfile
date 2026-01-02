FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

COPY Shared/DataIngestion.Contracts.csproj Shared/
COPY TradeIngestion/DataIngestion.TradeService.csproj TradeIngestion/
RUN dotnet restore TradeIngestion/DataIngestion.TradeService.csproj

COPY Shared/ Shared/
COPY TradeIngestion/ TradeIngestion/
RUN dotnet publish TradeIngestion/DataIngestion.TradeService.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
WORKDIR /app

RUN adduser -D -u 1000 appuser && \
    apk add --no-cache curl && \
    mkdir -p /app/data && chown -R appuser:appuser /app

COPY --from=build /app/publish .
RUN chown -R appuser:appuser /app

USER appuser
EXPOSE 5001

ENV ASPNETCORE_URLS=http://+:5001
ENTRYPOINT ["dotnet", "DataIngestion.TradeService.dll"]
