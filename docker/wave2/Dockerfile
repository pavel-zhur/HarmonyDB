# syntax=docker/dockerfile:1
# One .NET 8 application of this repository, built alone.
# Build context: the repository root.
#   --build-arg PROJECT=<the project's folder from the root>, e.g. HarmonyDB.Shelf/HarmonyDB.Shelf.Api

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG PROJECT
WORKDIR /src
COPY . .
RUN --mount=type=cache,target=/root/.nuget/packages,sharing=locked \
    dotnet publish "$PROJECT" --configuration Release --output /publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS app
ARG PROJECT
WORKDIR /app
COPY --from=build /publish .
# The application is started by its own executable, not through a shell: a shell would drop the settings whose names
# are not shell names.
RUN ln -s "/app/$(basename "$PROJECT")" /app/entrypoint
ENTRYPOINT ["/app/entrypoint"]
