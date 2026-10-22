# Production image for Himma (used by Render / Railway / Azure).
# Build:  docker build -t himma .
# The platform supplies secrets as environment variables; nothing secret is baked into the image.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/Himma.Api/Himma.Api.csproj src/Himma.Api/
RUN dotnet restore src/Himma.Api/Himma.Api.csproj
COPY src/Himma.Api/ src/Himma.Api/
RUN dotnet publish src/Himma.Api/Himma.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .

# The platform terminates HTTPS and forwards plain HTTP to port 8080 with X-Forwarded-* headers.
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_HTTPS_PORT=443 \
    Database__Provider=Postgres \
    ForwardedHeaders__Enabled=true

# Run as the image's built-in non-root user.
USER app
EXPOSE 8080

# Start the app. For the deploy step, run the same image with "--migrate" first.
ENTRYPOINT ["dotnet", "Himma.Api.dll"]
