# The new shelf's web: the app built, served by nginx, which passes the API, the MCP and the well-known documents on
# to shelf-api (docker/docker-compose.yml, service shelf-web). Context: HarmonyDB.Shelf/HarmonyDB.Shelf.Web.

FROM node:22.23.3-slim AS build
WORKDIR /web
COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY . .
# the fingerings' model, from the build context "fingerings" (docker/docker-compose.yml)
COPY --from=fingerings fingerings.json /fingerings/fingerings.json
ENV SHELF_FINGERINGS=/fingerings/fingerings.json
RUN npm run build && npm test

# The public page's web server tested against a stand-in for shelf-api: everything goes on as a guest's
# (public-web.test.sh); the stage public takes its mark, so the image is not made when it fails.
FROM nginx:1.27.5-alpine AS public-test
RUN apk add --no-cache curl
COPY docker/security.conf /etc/nginx/snippets/security.conf
COPY docker/public.nginx.conf docker/public-web.test.sh /test/
RUN sh /test/public-web.test.sh /test/public.nginx.conf && touch /test/passed

# The public page (target "public": service public-web): its page and the built files, not the app's page, worker or
# manifests; its container puts the addresses it is told, and the include of each address's preview written by
# shelf-api, into the page when it starts (public-site.sh): the page is this one build, the server writes only its head's
# preview.
FROM nginx:1.27.5-alpine AS public
COPY --from=public-test /test/passed /etc/nginx/public-web.tested
# nothing of the image's own pages (its welcome, its error page): every address but the built files is the page
RUN rm /usr/share/nginx/html/index.html /usr/share/nginx/html/50x.html
COPY docker/public.nginx.conf /etc/nginx/conf.d/default.conf
COPY docker/security.conf /etc/nginx/snippets/security.conf
COPY --chmod=755 docker/public-site.sh /docker-entrypoint.d/40-public-site.sh
COPY docker/public-robots.txt /usr/share/nginx/public-robots.txt
COPY --from=build /web/dist/public.html /usr/share/nginx/public.template.html
COPY --from=build /web/dist/assets /usr/share/nginx/html/assets
COPY --from=build /web/dist/img /usr/share/nginx/html/img
COPY --from=build /web/dist/favicon.png /web/dist/icon-192.png /web/dist/icon-512.png /usr/share/nginx/html/

# The app (the last stage: what a build without a target makes)
FROM nginx:1.27.5-alpine AS app
COPY docker/app.nginx.conf /etc/nginx/conf.d/default.conf
COPY docker/security.conf /etc/nginx/snippets/security.conf
COPY --from=build /web/dist /usr/share/nginx/html
# the public page is public-web's alone: not at the app's address
RUN rm /usr/share/nginx/html/public.html