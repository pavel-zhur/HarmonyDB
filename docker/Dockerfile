# syntax=docker/dockerfile:1
# The .NET 8 applications of this repository: the API (all the services, see OneShelf.Api), the new shelf's API and its
# import, the web sites, the poller.
# Build context: the repository root.
#   --target app --build-arg PROJECT=<project name>   an ASP.NET or worker app
#   --target transfer                                 the tool that fills PostgreSQL from SQL Server
#
# All of them are published in one stage; every image then takes its own project out of that stage.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN --mount=type=cache,target=/root/.nuget/packages,sharing=locked \
    set -e; \
    for project in \
        HarmonyDB.Index/HarmonyDB.Index.LongBalancer \
        HarmonyDB.Playground/HarmonyDB.Playground.Web \
        HarmonyDB.Shelf/HarmonyDB.Shelf.Api \
        HarmonyDB.Shelf/HarmonyDB.Shelf.Import \
        OneShelf.Admin/OneShelf.Admin.Web \
        OneShelf.Api/OneShelf.Api \
        OneShelf.Telegram/OneShelf.Telegram.Polling \
        ; do \
        dotnet publish "$project" --configuration Release --output "/publish/$(basename "$project")"; \
    done

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS transfer-build
WORKDIR /src
COPY . .
RUN --mount=type=cache,target=/root/.nuget/packages,sharing=locked \
    dotnet publish OneShelf.Common/OneShelf.Common.Database.Transfer.Tool --configuration Release --output /publish

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS transfer
WORKDIR /app
COPY --from=transfer-build /publish .
ENTRYPOINT ["dotnet", "OneShelf.Common.Database.Transfer.Tool.dll"]

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS app
ARG PROJECT
WORKDIR /app
COPY --from=build /publish/${PROJECT} .
# The application is started by its own executable, not through a shell: a shell would drop the settings whose names
# are not shell names (Logging__LogLevel__Microsoft.EntityFrameworkCore).
RUN ln -s "/app/${PROJECT}" /app/entrypoint
ENTRYPOINT ["/app/entrypoint"]
