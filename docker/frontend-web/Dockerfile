# syntax=docker/dockerfile:1
# The site: OneShelf.Frontend.WebTs (TypeScript + React), built with Node and served by nginx.
# Build context: the repository root (without the docker folder, see .dockerignore);
# the additional context "assets": this folder.

FROM node:22.19.0-slim AS build
WORKDIR /src
COPY OneShelf.Frontend/OneShelf.Frontend.WebTs/package.json OneShelf.Frontend/OneShelf.Frontend.WebTs/package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY OneShelf.Frontend/OneShelf.Frontend.WebTs/ ./

# the version the service worker names its cache by (see PORTING.md, "The service worker")
ARG CACHE_VERSION=local
RUN sed -i "s/{#CACHE_VERSION#}/${CACHE_VERSION}/g" public/service-worker.published.js index.html

# type-checks and builds into dist (with the service worker and its manifest, build/serviceWorker.ts)
RUN npm run build

# The compressed copies nginx serves to browsers that accept them (gzip_static); the files already compressed in
# their own format (fonts, pictures) are served as they are.
RUN find dist -type f \( -name '*.js' -o -name '*.css' -o -name '*.json' -o -name '*.html' -o -name '*.svg' \
        -o -name '*.ttf' -o -name '*.eot' -o -name '*.scss' \) -exec gzip -k -n -9 {} +

FROM nginx:1.27-alpine
RUN apk add --no-cache openssl jq
# the nginx image fills the template from the environment and runs the script at start
COPY --from=assets nginx.conf.template /etc/nginx/templates/default.conf.template
COPY --from=assets configure.sh /docker-entrypoint.d/40-configure.sh
RUN chmod +x /docker-entrypoint.d/40-configure.sh
COPY --from=build /src/dist /usr/share/nginx/html
