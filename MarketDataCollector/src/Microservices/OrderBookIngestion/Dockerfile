FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

COPY Shared/DataIngestion.Contracts.csproj Shared/
COPY OrderBookIngestion/DataIngestion.OrderBookService.csproj OrderBookIngestion/
RUN dotnet restore OrderBookIngestion/DataIngestion.OrderBookService.csproj

COPY Shared/ Shared/
COPY OrderBookIngestion/ OrderBookIngestion/
RUN dotnet publish OrderBookIngestion/DataIngestion.OrderBookService.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
WORKDIR /app

RUN adduser -D -u 1000 appuser && \
    apk add --no-cache curl && \
    mkdir -p /app/data && chown -R appuser:appuser /app

COPY --from=build /app/publish .
RUN chown -R appuser:appuser /app

USER appuser
EXPOSE 5002

ENV ASPNETCORE_URLS=http://+:5002
ENTRYPOINT ["dotnet", "DataIngestion.OrderBookService.dll"]
