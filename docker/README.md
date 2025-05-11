# Pre-built Docker Environment

This Docker setup uses pre-built releases from GitHub to run your services in containers, eliminating the need to build projects in the Docker environment.

## Understanding Release Tags

The GitHub releases use specific tag patterns:

- `v*-m`: Microservices release (includes all backend APIs like Index, Authorization, Billing, etc.)
- `v*-f`: Frontend Web release
- `v*-a`: Admin Web release
- `v*-mp`: PDF API microservice release
- `v*-p`: Playground Web release

Current latest versions:
- `v1.0.140-m`: Latest microservices
- `v1.0.130-f`: Latest frontend
- `v1.0.109-a`: Latest admin
- `v1.0.63-mp`: Latest PDF service
- `v1.0.88-p`: Latest playground

## Configuration

The setup uses the following files:

- `.env` - Environment variables for configuration including release versions (all variables are required, no defaults)
- `Dockerfile.dotnet-service` - Base Dockerfile for .NET services
- `docker-compose.yml` - Main Docker Compose file that orchestrates all services
- `configs/*.json` - Configuration files for each service that override appsettings.json

## Getting Started

1. Ensure all environment variables in `.env` are properly set (all are required)

2. Start the environment:
   ```bash
   cd docker
   docker compose up -d
   ```
   
   Alternatively, use VS Code's Docker extension "Run All Services" button.

3. Check the status:
   ```bash
   docker compose ps
   ```

4. View logs:
   ```bash
   docker compose logs -f
   ```

## Available Services

### Core APIs
- Index API: http://localhost:13001
- Authorization API: http://localhost:13002
- Illustrations API: http://localhost:13005
- Billing API: http://localhost:13006
- PDFs API: http://localhost:13007
- Collectives API: http://localhost:13009
- Sources Self API: http://localhost:13010
- Frontend API: http://localhost:13011
- Telegram Runner: http://localhost:13012

### Web Applications
- Frontend Web: http://localhost:13003
- Admin Web: http://localhost:13004
- Playground Web: http://localhost:13008

### Internal Services
- Telegram Polling: Internal service (no exposed port)

### Infrastructure
- SQL Server: localhost:13433
- Azurite Storage: http://localhost:13000
- Cosmos DB Emulator: http://localhost:13013
- Cosmos DB Explorer: http://localhost:13015

## Updating Services

To update a service to a new release:

1. Edit the `.env` file and update the version:
   ```
   INDEX_API_VERSION=v1.2.4-m
   ```

2. Rebuild and restart the service:
   ```bash
   docker-compose up -d --build index-api
   ```

## Service Configuration

To customize service configurations:

1. Create a JSON config file in the `configs/` folder, for example:
   ```
   configs/index-api.json
   ```

2. Mount it in docker-compose.yml (this is already set up):
   ```yaml
   volumes:
     - ./configs/index-api.json:/app/appsettings.Development.json
   ```

## Troubleshooting

- If a service fails to start, check the logs:
  ```bash
  docker-compose logs index-api
  ```

- If the GitHub release download fails, verify:
  - The release version exists on GitHub
  - The filename matches the asset in the GitHub release 