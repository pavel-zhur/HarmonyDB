FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy only the polling project - it uses NuGet packages for dependencies
COPY ["OneShelf.Telegram/OneShelf.Telegram.Polling", "OneShelf.Telegram/OneShelf.Telegram.Polling/"]

# Build the application
WORKDIR "/src/OneShelf.Telegram/OneShelf.Telegram.Polling"
RUN dotnet build "OneShelf.Telegram.Polling.csproj" -c Release -o /app/build

# Publish the application
FROM build AS publish
RUN dotnet publish "OneShelf.Telegram.Polling.csproj" -c Release -o /app/publish

# Create the final image
FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=publish /app/publish .

# Set environment variables
ENV ASPNETCORE_ENVIRONMENT=Development

# Start the application
ENTRYPOINT ["dotnet", "OneShelf.Telegram.Polling.dll"] 