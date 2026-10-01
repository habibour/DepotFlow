# Build context is the repository root:  docker build -f src/DepotFlow.Api/Dockerfile .

# ---- build: the full SDK compiles and publishes the API ---------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy only the project files first, so the restore layer is cached until a package or reference changes.
COPY Directory.Build.props ./
COPY src/DepotFlow.Domain/DepotFlow.Domain.csproj src/DepotFlow.Domain/
COPY src/DepotFlow.Application/DepotFlow.Application.csproj src/DepotFlow.Application/
COPY src/DepotFlow.Infrastructure/DepotFlow.Infrastructure.csproj src/DepotFlow.Infrastructure/
COPY src/DepotFlow.Api/DepotFlow.Api.csproj src/DepotFlow.Api/
RUN dotnet restore src/DepotFlow.Api/DepotFlow.Api.csproj

COPY src/ src/
RUN dotnet publish src/DepotFlow.Api/DepotFlow.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---- run: the small ASP.NET runtime image, as a non-root user -------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Port 8080 (above 1024) so the non-root user can bind it. APP_UID is defined by the official .NET images.
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "DepotFlow.Api.dll"]
