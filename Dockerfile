# 1. Imagen base para compilar (SDK de .NET 8)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# 2. Copiar los archivos de proyecto (csproj) de cada capa
COPY ["FacturasSRI.Web/FacturasSRI.Web.csproj", "FacturasSRI.Web/"]
COPY ["FacturasSRI.Application/FacturasSRI.Application.csproj", "FacturasSRI.Application/"]
COPY ["FacturasSRI.Domain/FacturasSRI.Domain.csproj", "FacturasSRI.Domain/"]
COPY ["FacturasSRI.Infrastructure/FacturasSRI.Infrastructure.csproj", "FacturasSRI.Infrastructure/"]
COPY ["FacturasSRI.Core/FacturasSRI.Core.csproj", "FacturasSRI.Core/"]

# 3. Restaurar dependencias (descargar librerías)
RUN dotnet restore "FacturasSRI.Web/FacturasSRI.Web.csproj"

# 4. Copiar el resto del código fuente
COPY . .

# 5. Compilar la aplicación en modo Release
WORKDIR "/src/FacturasSRI.Web"
RUN dotnet build "FacturasSRI.Web.csproj" -c Release -o /app/build

# 6. Publicar los archivos finales
FROM build AS publish
RUN dotnet publish "FacturasSRI.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

# 7. Imagen final para ejecutar (más ligera)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Librerías necesarias para manejo de imágenes/reportes en Linux (System.Drawing)
RUN apt-get update && apt-get install -y libgdiplus

# Configurar puerto para Render
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "FacturasSRI.Web.dll"]