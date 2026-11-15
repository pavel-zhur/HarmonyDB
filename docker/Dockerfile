# syntax=docker/dockerfile:1
# The .NET 8 services of this repository: the Azure Functions apps (isolated worker) and the ASP.NET / worker apps.
# Build context: the repository root.
#   --target functions --build-arg PROJECT=<project name>   an Azure Functions app
#   --target app       --build-arg PROJECT=<project name>   an ASP.NET or worker app
#
# All of them are published in one stage, as .github/workflows/microservices.yml publishes them one after another;
# every image then takes its own project out of that stage.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN --mount=type=cache,target=/root/.nuget/packages,sharing=locked \
    set -e; \
    for project in \
        HarmonyDB.Index/HarmonyDB.Index.Api \
        HarmonyDB.Index/HarmonyDB.Index.LongBalancer \
        HarmonyDB.Playground/HarmonyDB.Playground.Web \
        OneShelf.Admin/OneShelf.Admin.Web \
        OneShelf.Authorization/OneShelf.Authorization.Api \
        OneShelf.Billing/OneShelf.Billing.Api \
        OneShelf.Collectives/OneShelf.Collectives.Api \
        OneShelf.Frontend/OneShelf.Frontend.Api \
        OneShelf.Illustrations/OneShelf.Illustrations.Api \
        OneShelf.OneDog/OneShelf.OneDog.Runner.Functions \
        OneShelf.OneDragon/OneShelf.OneDragon.Runner.Functions \
        OneShelf.Sources/OneShelf.Sources.Self.Api \
        OneShelf.Telegram/OneShelf.Telegram.Polling \
        OneShelf.Telegram/OneShelf.Telegram.Runner.Functions \
        ; do \
        dotnet publish "$project" --configuration Release --output "/publish/$(basename "$project")"; \
    done

FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated8.0 AS functions
ARG PROJECT
ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true \
    FUNCTIONS_WORKER_RUNTIME=dotnet-isolated
COPY --from=build /publish/${PROJECT} /home/site/wwwroot

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS app
ARG PROJECT
ENV PROJECT=${PROJECT}
WORKDIR /app
COPY --from=build /publish/${PROJECT} .
ENTRYPOINT ["sh", "-c", "exec dotnet \"$PROJECT.dll\""]
