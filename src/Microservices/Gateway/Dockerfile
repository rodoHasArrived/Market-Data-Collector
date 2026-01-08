FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

COPY Shared/DataIngestion.Contracts.csproj Shared/
COPY Gateway/DataIngestion.Gateway.csproj Gateway/
RUN dotnet restore Gateway/DataIngestion.Gateway.csproj

COPY Shared/ Shared/
COPY Gateway/ Gateway/
RUN dotnet publish Gateway/DataIngestion.Gateway.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
WORKDIR /app

RUN adduser -D -u 1000 appuser && \
    apk add --no-cache curl

COPY --from=build /app/publish .
RUN chown -R appuser:appuser /app

USER appuser
EXPOSE 5000

ENV ASPNETCORE_URLS=http://+:5000
ENV DOTNET_RUNNING_IN_CONTAINER=true

ENTRYPOINT ["dotnet", "DataIngestion.Gateway.dll"]
