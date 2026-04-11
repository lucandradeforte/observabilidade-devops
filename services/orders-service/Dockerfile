FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY src/OrdersService.Api/OrdersService.Api.csproj src/OrdersService.Api/
RUN dotnet restore src/OrdersService.Api/OrdersService.Api.csproj

COPY src/OrdersService.Api/. src/OrdersService.Api/
RUN dotnet publish src/OrdersService.Api/OrdersService.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime

WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    OTEL_SERVICE_NAME=orders-service

RUN apk add --no-cache curl

COPY --from=build /app/publish .

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --retries=3 CMD curl -fsS http://127.0.0.1:8080/healthz || exit 1

ENTRYPOINT ["dotnet", "OrdersService.Api.dll"]

