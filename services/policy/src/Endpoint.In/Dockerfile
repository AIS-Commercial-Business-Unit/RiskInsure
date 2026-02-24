# Multi-stage Dockerfile for NServiceBus Endpoint.In projects
# Optimized for Azure Container Apps deployment with KEDA scaling

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
COPY ["services/policy/src/Domain/Domain.csproj", "services/policy/src/Domain/"]
COPY ["services/policy/src/Endpoint.In/Endpoint.In.csproj", "services/policy/src/Endpoint.In/"]

# Restore dependencies
RUN dotnet restore "services/policy/src/Endpoint.In/Endpoint.In.csproj"

# Copy remaining source code
COPY ["platform/RiskInsure.PublicContracts/", "platform/RiskInsure.PublicContracts/"]
COPY ["services/policy/", "services/policy/"]

# Build and publish
WORKDIR "/src/services/policy/src/Endpoint.In"
RUN dotnet publish --no-restore -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Create non-root user for security
RUN groupadd -r nservicebus && useradd -r -g nservicebus nservicebus

# Copy published application
COPY --from=build /app/publish .

# Set ownership
RUN chown -R nservicebus:nservicebus /app

# Switch to non-root user
USER nservicebus

# NServiceBus endpoints are long-running console apps (no HTTP ports, no healthcheck needed)
ENTRYPOINT ["dotnet", "Endpoint.In.dll"]
