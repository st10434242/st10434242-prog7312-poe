# Build from the repository root, since the API needs SmartX.Core alongside it
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copying just the project files first means restore is cached until a dependency changes
COPY SmartX.Core/SmartX.Core.csproj SmartX.Core/
COPY SmartX.Api/SmartX.Api.csproj SmartX.Api/
RUN dotnet restore SmartX.Api/SmartX.Api.csproj

COPY SmartX.Core/ SmartX.Core/
COPY SmartX.Api/ SmartX.Api/
RUN dotnet publish SmartX.Api/SmartX.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

# curl is only here so compose can health check the gateway
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app .

EXPOSE 8080
ENTRYPOINT ["dotnet", "SmartX.Api.dll"]
