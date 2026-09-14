FROM mcr.microsoft.com/dotnet/sdk:10.0.103 AS build
WORKDIR /src

COPY BehavioralAnticheatEngine.slnx ./
COPY src/BehavioralAnticheatEngine.Domain/BehavioralAnticheatEngine.Domain.csproj src/BehavioralAnticheatEngine.Domain/
COPY src/BehavioralAnticheatEngine.Application/BehavioralAnticheatEngine.Application.csproj src/BehavioralAnticheatEngine.Application/
COPY src/BehavioralAnticheatEngine.Infrastructure/BehavioralAnticheatEngine.Infrastructure.csproj src/BehavioralAnticheatEngine.Infrastructure/
COPY src/BehavioralAnticheatEngine.Worker/BehavioralAnticheatEngine.Worker.csproj src/BehavioralAnticheatEngine.Worker/
RUN dotnet restore src/BehavioralAnticheatEngine.Worker/BehavioralAnticheatEngine.Worker.csproj

COPY src ./src
RUN rm -rf src/*/obj && dotnet restore src/BehavioralAnticheatEngine.Worker/BehavioralAnticheatEngine.Worker.csproj
RUN dotnet publish src/BehavioralAnticheatEngine.Worker/BehavioralAnticheatEngine.Worker.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0.3 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "BehavioralAnticheatEngine.Worker.dll"]
