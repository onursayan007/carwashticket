# Bağlam repo kökü: docker build -f api/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Önce sadece csproj: paket katmanı kod değişince yeniden çözülmesin.
COPY api/CarWashTicket.Api.csproj api/
RUN dotnet restore api/CarWashTicket.Api.csproj

COPY api/ api/
RUN dotnet publish api/CarWashTicket.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app

# Root olarak çalışmaya gerek yok.
RUN useradd --uid 1001 --create-home app
USER app

COPY --from=build /app .

EXPOSE 8080
ENTRYPOINT ["dotnet", "CarWashTicket.Api.dll"]
