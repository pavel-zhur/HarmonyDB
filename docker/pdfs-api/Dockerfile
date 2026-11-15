# syntax=docker/dockerfile:1
# The PDF service (OneShelf.Pdfs.Api, an ASP.NET application). It turns HTML into PDF with a Chrome it carries with it,
# so it has an image of its own: the system libraries that Chrome needs, and fonts.
# Build context: the repository root.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN --mount=type=cache,target=/root/.nuget/packages,sharing=locked \
    dotnet publish OneShelf.Pdfs/OneShelf.Pdfs.Api --configuration Release --output /publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
# what the carried Chrome needs from the system, and fonts to draw the text with
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        libnss3 libatk1.0-0 libatk-bridge2.0-0 libcups2 libdrm2 libxkbcommon0 libxcomposite1 libxdamage1 \
        libxfixes3 libxrandr2 libgbm1 libasound2 libpango-1.0-0 libcairo2 libx11-6 libxext6 libxcb1 libdbus-1-3 \
        libglib2.0-0 libexpat1 \
        fonts-liberation fonts-dejavu-core \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /publish .
# The application is started by its own executable, not through a shell: a shell would drop the settings whose names
# are not shell names.
ENTRYPOINT ["/app/OneShelf.Pdfs.Api"]
