# Etapa 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiar la solución y archivos .csproj para restaurar dependencias
COPY ["PROYECTO-SUBASTA.sln", "./"]
COPY ["PROYECTO_SUBASTA.Domain/PROYECTO_SUBASTA.Domain.csproj", "PROYECTO_SUBASTA.Domain/"]
COPY ["PROYECTO_SUBASTA.Application/PROYECTO_SUBASTA.Application.csproj", "PROYECTO_SUBASTA.Application/"]
COPY ["PROYECTO_SUBASTA.Infrastructure/PROYECTO_SUBASTA.Infrastructure.csproj", "PROYECTO_SUBASTA.Infrastructure/"]
COPY ["PROYECTO_SUBASTA.Api/PROYECTO_SUBASTA.Api.csproj", "PROYECTO_SUBASTA.Api/"]

RUN dotnet restore

# Copiar el resto del código fuente y compilar
COPY . .
RUN dotnet publish "PROYECTO_SUBASTA.Api/PROYECTO_SUBASTA.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Copiar explícitamente el directorio Frontend hacia /app/publish/Frontend para servir la SPA
COPY PROYECTO_SUBASTA.Api/Frontend/ /app/publish/Frontend/

# Etapa 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://+:${PORT:-8080} dotnet PROYECTO_SUBASTA.Api.dll"]
