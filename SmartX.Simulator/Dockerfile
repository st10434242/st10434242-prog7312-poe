# Build from the repository root, since the simulator needs SmartX.Core alongside it
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY SmartX.Core/SmartX.Core.csproj SmartX.Core/
COPY SmartX.Simulator/SmartX.Simulator.csproj SmartX.Simulator/
RUN dotnet restore SmartX.Simulator/SmartX.Simulator.csproj

COPY SmartX.Core/ SmartX.Core/
COPY SmartX.Simulator/ SmartX.Simulator/
RUN dotnet publish SmartX.Simulator/SmartX.Simulator.csproj -c Release -o /app

# Plain runtime rather than aspnet, the simulator only makes outgoing requests
FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /app .

ENTRYPOINT ["dotnet", "SmartX.Simulator.dll"]
