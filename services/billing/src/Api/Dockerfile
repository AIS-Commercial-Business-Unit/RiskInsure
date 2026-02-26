# Multi-stage Dockerfile for ASP.NET Core Api projects
# Optimized for Azure Container Apps deployment

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ["RiskInsure.slnx", "."]
COPY ["Directory.Build.props", "."]
COPY ["Directory.Packages.props", "."]

# Copy PublicContracts (shared dependency)
COPY ["platform/RiskInsure.PublicContracts/RiskInsure.PublicContracts.csproj", "platform/RiskInsure.PublicContracts/"]

# Copy service projects
COPY ["services/billing/src/Domain/Domain.csproj", "services/billing/src/Domain/"]
COPY ["services/billing/src/Infrastructure/Infrastructure.csproj", "services/billing/src/Infrastructure/"]
COPY ["services/billing/src/Api/Api.csproj", "services/billing/src/Api/"]

# Restore dependencies
RUN dotnet restore "services/billing/src/Api/Api.csproj"

# Copy remaining source code
COPY ["platform/RiskInsure.PublicContracts/", "platform/RiskInsure.PublicContracts/"]
COPY ["services/billing/", "services/billing/"]

# Build and publish
WORKDIR "/src/services/billing/src/Api"
RUN dotnet publish --no-restore -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Create non-root user for security
RUN groupadd -r apiuser && useradd -r -g apiuser apiuser

# Copy published application
COPY --from=build /app/publish .

# Install curl for healthchecks
RUN apt-get update && apt-get install -y curl --no-install-recommends && rm -rf /var/lib/apt/lists/*

# Set ownership
RUN chown -R apiuser:apiuser /app

# Switch to non-root user
USER apiuser

# Expose HTTP port (Container Apps will map this)
EXPOSE 8080

# Health check endpoint
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
  CMD curl -f http://localhost:8080/api/billing/health || exit 1

# Set ASP.NET Core to listen on port 8080 (non-root)
ENV ASPNETCORE_URLS=http://+:8080

# Run the application
ENTRYPOINT ["dotnet", "Api.dll"]