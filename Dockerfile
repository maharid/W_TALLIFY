# --- Stage 1: Build Stage ---
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files and restore dependencies
COPY ["ProjectTallify.csproj", "./"]
RUN dotnet restore "ProjectTallify.csproj"

# Copy the rest of the source code
COPY . .

# Build and publish the application in Release mode
# This creates a 'publish' directory with all runtime-necessary files
RUN dotnet publish "ProjectTallify.csproj" -c Release -o /app/publish /p:UseAppHost=false

# --- Stage 2: Runtime Stage ---
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Create a non-privileged user for security
RUN adduser --disabled-password --gecos "" appuser && chown -R appuser /app
USER appuser

# Copy the published output from the build stage
COPY --from=build /app/publish .

# Expose the ports the app listens on
EXPOSE 8080
EXPOSE 8081

# Set the entry point to run the application
ENTRYPOINT ["dotnet", "ProjectTallify.dll"]
