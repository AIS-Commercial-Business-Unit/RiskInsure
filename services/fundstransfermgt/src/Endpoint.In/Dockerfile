# Multi-stage Dockerfile for NServiceBus Endpoint.In projects
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
COPY ["services/fundstransfermgt/src/Domain/Domain.csproj", "services/fundstransfermgt/src/Domain/"]
COPY ["services/fundstransfermgt/src/Infrastructure/Infrastructure.csproj", "services/fundstransfermgt/src/Infrastructure/"]
COPY ["services/fundstransfermgt/src/Endpoint.In/Endpoint.In.csproj", "services/fundstransfermgt/src/Endpoint.In/"]

# Restore dependencies
RUN dotnet restore "services/fundstransfermgt/src/Endpoint.In/Endpoint.In.csproj"

# Copy remaining source code
COPY ["platform/RiskInsure.PublicContracts/", "platform/RiskInsure.PublicContracts/"]
COPY ["services/fundstransfermgt/", "services/fundstransfermgt/"]

# Build and publish
WORKDIR "/src/services/fundstransfermgt/src/Endpoint.In"
RUN dotnet publish --no-restore -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app

# Create non-root user for security
RUN groupadd -r nservicebus && useradd -r -g nservicebus nservicebus

# Copy published application
COPY --from=build /app/publish .

# Set ownership
RUN chown -R nservicebus:nservicebus /app

# Switch to non-root user
USER nservicebus

# Run the NServiceBus endpoint (long-running console application)
# No HTTP exposure, no healthcheck - this is a message-driven endpoint
ENTRYPOINT ["dotnet", "Endpoint.In.dll"]
