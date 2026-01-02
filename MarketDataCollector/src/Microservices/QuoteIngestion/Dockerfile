FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

COPY Shared/DataIngestion.Contracts.csproj Shared/
COPY QuoteIngestion/DataIngestion.QuoteService.csproj QuoteIngestion/
RUN dotnet restore QuoteIngestion/DataIngestion.QuoteService.csproj

COPY Shared/ Shared/
COPY QuoteIngestion/ QuoteIngestion/
RUN dotnet publish QuoteIngestion/DataIngestion.QuoteService.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
WORKDIR /app

RUN adduser -D -u 1000 appuser && \
    apk add --no-cache curl && \
    mkdir -p /app/data && chown -R appuser:appuser /app

COPY --from=build /app/publish .
RUN chown -R appuser:appuser /app

USER appuser
EXPOSE 5003

ENV ASPNETCORE_URLS=http://+:5003
ENTRYPOINT ["dotnet", "DataIngestion.QuoteService.dll"]
