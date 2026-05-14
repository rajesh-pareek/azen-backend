# syntax=docker/dockerfile:1.7

# =========================================================================
# Stage 1: build
# Uses the .NET 8 SDK image - has dotnet CLI, NuGet, build tools.
# This stage produces a compiled, published app. It is NOT shipped.
# =========================================================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy ONLY the solution + csproj files first.
# Docker layer caching: as long as these files don't change, the slow
# 'dotnet restore' step is reused from cache on subsequent builds even
# when application source code changes.
COPY Azen.sln ./
COPY src/Azen.Domain/Azen.Domain.csproj         src/Azen.Domain/
COPY src/Azen.Application/Azen.Application.csproj src/Azen.Application/
COPY src/Azen.Infrastructure/Azen.Infrastructure.csproj src/Azen.Infrastructure/
COPY src/Azen.Api/Azen.Api.csproj               src/Azen.Api/

# Pull NuGet packages for every project in the solution.
RUN dotnet restore Azen.sln

# Now copy the actual source code.
COPY . .

# Compile + publish the API into /app/publish.
# --no-restore: we already restored above; skip the redundant work.
# -c Release: optimised, no debug symbols by default.
# -o /app/publish: output directory.
RUN dotnet publish src/Azen.Api/Azen.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore


# =========================================================================
# Stage 2: runtime
# Uses the ASP.NET Core 8 runtime image - no SDK, no build tools.
# Much smaller. This is what gets pushed to registries and run in prod.
# =========================================================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Copy the published output from the build stage (NOT from your machine).
COPY --from=build /app/publish .

# Run as a non-root user. ASP.NET base image ships a pre-created
# 'app' user (UID 1654). Avoids container processes running as root.
USER app

# The app listens on 8080 inside the container. The host can map this
# to any port via 'docker run -p 5000:8080' or compose.
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Azen.Api.dll"]
