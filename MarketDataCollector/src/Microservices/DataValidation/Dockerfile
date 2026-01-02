FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

COPY Shared/DataIngestion.Contracts.csproj Shared/
COPY DataValidation/DataIngestion.ValidationService.csproj DataValidation/
RUN dotnet restore DataValidation/DataIngestion.ValidationService.csproj

COPY Shared/ Shared/
COPY DataValidation/ DataValidation/
RUN dotnet publish DataValidation/DataIngestion.ValidationService.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
WORKDIR /app

RUN adduser -D -u 1000 appuser && \
    apk add --no-cache curl

COPY --from=build /app/publish .
RUN chown -R appuser:appuser /app

USER appuser
EXPOSE 5005

ENV ASPNETCORE_URLS=http://+:5005
ENTRYPOINT ["dotnet", "DataIngestion.ValidationService.dll"]
