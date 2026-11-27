FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/MesaAyuda.Domain/MesaAyuda.Domain.csproj src/MesaAyuda.Domain/
COPY src/MesaAyuda.Application/MesaAyuda.Application.csproj src/MesaAyuda.Application/
COPY src/MesaAyuda.Infrastructure/MesaAyuda.Infrastructure.csproj src/MesaAyuda.Infrastructure/
COPY src/MesaAyuda.Api/MesaAyuda.Api.csproj src/MesaAyuda.Api/

RUN dotnet restore src/MesaAyuda.Api/MesaAyuda.Api.csproj

COPY src/ src/

RUN dotnet publish src/MesaAyuda.Api/MesaAyuda.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .
COPY --from=build /src/src/MesaAyuda.Infrastructure/Persistence/Migrations ./Migrations

EXPOSE 8080

ENTRYPOINT ["dotnet", "MesaAyuda.Api.dll"]
