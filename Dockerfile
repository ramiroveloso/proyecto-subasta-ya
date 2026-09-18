# ==========================================
# ETAPA 1: Compilación y Publicación
# ==========================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiar los archivos de solución y proyectos para restaurar dependencias
COPY ["PROYECTO_SUBASTA.sln", "./"]
COPY ["PROYECTO_SUBASTA.Api/PROYECTO_SUBASTA.Api.csproj", "PROYECTO_SUBASTA.Api/"]
COPY ["PROYECTO_SUBASTA.Application/PROYECTO_SUBASTA.Application.csproj", "PROYECTO_SUBASTA.Application/"]
COPY ["PROYECTO_SUBASTA.Domain/PROYECTO_SUBASTA.Domain.csproj", "PROYECTO_SUBASTA.Domain/"]
COPY ["PROYECTO_SUBASTA.Infrastructure/PROYECTO_SUBASTA.Infrastructure.csproj", "PROYECTO_SUBASTA.Infrastructure/"]

# Restaurar paquetes NuGet
RUN dotnet restore "PROYECTO_SUBASTA.Api/PROYECTO_SUBASTA.Api.csproj"

# Copiar el resto del código fuente
COPY . .
WORKDIR "/src/PROYECTO_SUBASTA.Api"
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# ==========================================
# ETAPA 2: Ejecución final (Runtime ligero)
# ==========================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

COPY --from=build /app/publish .

# Comando de inicio del contenedor (por eso en Render el Start Command va vacío)
ENTRYPOINT ["dotnet", "PROYECTO_SUBASTA.Api.dll"]