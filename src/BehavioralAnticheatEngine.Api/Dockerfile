FROM mcr.microsoft.com/dotnet/sdk:10.0.103 AS build
WORKDIR /src

COPY BehavioralAnticheatEngine.slnx ./
COPY src/BehavioralAnticheatEngine.Domain/BehavioralAnticheatEngine.Domain.csproj src/BehavioralAnticheatEngine.Domain/
COPY src/BehavioralAnticheatEngine.Application/BehavioralAnticheatEngine.Application.csproj src/BehavioralAnticheatEngine.Application/
COPY src/BehavioralAnticheatEngine.Infrastructure/BehavioralAnticheatEngine.Infrastructure.csproj src/BehavioralAnticheatEngine.Infrastructure/
COPY src/BehavioralAnticheatEngine.Api/BehavioralAnticheatEngine.Api.csproj src/BehavioralAnticheatEngine.Api/
RUN dotnet restore src/BehavioralAnticheatEngine.Api/BehavioralAnticheatEngine.Api.csproj

COPY src ./src
RUN rm -rf src/*/obj && dotnet restore src/BehavioralAnticheatEngine.Api/BehavioralAnticheatEngine.Api.csproj
RUN dotnet publish src/BehavioralAnticheatEngine.Api/BehavioralAnticheatEngine.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0.3 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "BehavioralAnticheatEngine.Api.dll"]
