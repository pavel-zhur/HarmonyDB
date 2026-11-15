# syntax=docker/dockerfile:1
# OneShelf.Frontend.Web (Blazor WebAssembly), built on Linux the way .github/workflows/frontend.yml builds it on Windows.
# Build context: the repository root (without the docker folder, see .dockerignore);
# the additional context "assets": this folder.

# The styles. On Windows the BuildWebCompiler2022 package compiles them during the build; it does not run on Linux,
# so compile-styles.js does the same with the same compiler versions (see that file).
FROM node:16.6.0-slim AS styles
WORKDIR /styles
COPY --from=assets package.json package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY --from=assets compile-styles.js ./
COPY OneShelf.Frontend/OneShelf.Frontend.Web /project
RUN node compile-styles.js /project /styles/node_modules

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
# python3 is what the wasm-tools (emscripten) need on Linux for the AOT compilation
RUN apt-get update \
    && apt-get install -y --no-install-recommends python3 \
    && rm -rf /var/lib/apt/lists/* \
    && dotnet workload install wasm-tools
WORKDIR /src
COPY . .
COPY --from=styles /project/ OneShelf.Frontend/OneShelf.Frontend.Web/
WORKDIR /src/OneShelf.Frontend/OneShelf.Frontend.Web

# as the "Replace" steps of the workflow
ARG CACHE_VERSION=local
RUN sed -i "s/{#CACHE_VERSION#}/${CACHE_VERSION}/g" wwwroot/service-worker.published.js wwwroot/index.html

RUN dotnet restore -p:RunWebCompiler=False

# What follows is publish.ps1.

# Normal build with SIMD and BlazorWebAssemblyJiterpreter enabled
RUN dotnet publish --nologo --no-restore --configuration Release --output /publish -p:RunWebCompiler=False

# Prepare the build with SIMD and BlazorWebAssemblyJiterpreter disabled (the project file is UTF-16)
RUN iconv -f UTF-16 -t UTF-8 OneShelf.Frontend.Web.csproj \
        | sed -e 's/compatiblity/no/g' -e 's/encoding="utf-16"/encoding="utf-8"/' > OneShelf.Frontend.Web.csproj.compat \
    && mv OneShelf.Frontend.Web.csproj.compat OneShelf.Frontend.Web.csproj

# Compat build with SIMD and BlazorWebAssemblyJiterpreter disabled
RUN dotnet publish --nologo --no-restore --configuration Release --output /publish-compat -p:RunWebCompiler=False

# Combine builds
RUN cp -r /publish-compat/wwwroot/_framework /publish/wwwroot/_frameworkCompat \
    && cp /publish-compat/wwwroot/service-worker-assets.js /publish/wwwroot/service-worker-assets-compat.js

FROM nginx:1.27-alpine
RUN apk add --no-cache openssl jq
# the nginx image fills the template from the environment and runs the script at start
COPY --from=assets nginx.conf.template /etc/nginx/templates/default.conf.template
COPY --from=assets configure.sh /docker-entrypoint.d/40-configure.sh
RUN chmod +x /docker-entrypoint.d/40-configure.sh
COPY --from=build /publish/wwwroot /usr/share/nginx/html
