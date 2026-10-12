FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
# Kestrel escucha en el 80 por defecto. Lo movemos al 5000 para que el
# contenedor use el mismo puerto que "dotnet run" y que los manifiestos
# de Kubernetes (targetPort: 5000).
ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["NetKubernetes.csproj", "./"]
RUN dotnet restore "NetKubernetes.csproj"
COPY . .
RUN dotnet build "NetKubernetes.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "NetKubernetes.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "NetKubernetes.dll"]
