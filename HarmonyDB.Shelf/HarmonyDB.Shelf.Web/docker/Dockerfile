# The new shelf's web: the app built, served by nginx, which passes the API, the MCP and the well-known documents on
# to shelf-api (docker/wave2/docker-compose.yml, service shelf-web). Context: HarmonyDB.Shelf/HarmonyDB.Shelf.Web.

FROM node:22.23.3-slim AS build
WORKDIR /web
COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY . .
# the fingerings' model, from the build context "fingerings" (docker/wave2/docker-compose.yml)
COPY --from=fingerings fingerings.json /fingerings/fingerings.json
ENV SHELF_FINGERINGS=/fingerings/fingerings.json
RUN npm run build && npm test

FROM nginx:1.27.5-alpine
COPY docker/app.nginx.conf /etc/nginx/conf.d/default.conf
COPY docker/security.conf /etc/nginx/snippets/security.conf
COPY --from=build /web/dist /usr/share/nginx/html
