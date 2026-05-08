# GestorCampo.API/Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["GestorCampo.Domain/GestorCampo.Domain.csproj", "GestorCampo.Domain/"]
COPY ["GestorCampo.Application/GestorCampo.Application.csproj", "GestorCampo.Application/"]
COPY ["GestorCampo.Infrastructure/GestorCampo.Infrastructure.csproj", "GestorCampo.Infrastructure/"]
COPY ["GestorCampo.API/GestorCampo.API.csproj", "GestorCampo.API/"]
RUN dotnet restore "GestorCampo.API/GestorCampo.API.csproj"
COPY . .
RUN dotnet build "GestorCampo.API/GestorCampo.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "GestorCampo.API/GestorCampo.API.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "GestorCampo.API.dll"]
