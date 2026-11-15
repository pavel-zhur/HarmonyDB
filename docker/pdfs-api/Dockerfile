# syntax=docker/dockerfile:1
# OneShelf.Pdfs.Api: an Azure Functions app of the in-process kind on .NET 6, published as
# .github/workflows/microservices-pdfs.yml publishes it. It turns HTML into PDF with a Chrome it carries with it.
# Build context: the repository root.

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY OneShelf.Pdfs/OneShelf.Pdfs.Api OneShelf.Pdfs/OneShelf.Pdfs.Api
RUN dotnet publish OneShelf.Pdfs/OneShelf.Pdfs.Api/OneShelf.Pdfs.Api.csproj --configuration Release --output /publish

FROM mcr.microsoft.com/azure-functions/dotnet:4
# The image is on Debian 11, whose packages have moved to Debian's archive.
RUN sed -i -e 's|deb.debian.org/debian-security|archive.debian.org/debian-security|g' \
           -e 's|security.debian.org/debian-security|archive.debian.org/debian-security|g' \
           -e 's|deb.debian.org/debian|archive.debian.org/debian|g' \
           -e '/bullseye-updates/d' /etc/apt/sources.list
# what the carried Chrome needs from the system, and fonts to draw the text with
RUN apt-get -o Acquire::Check-Valid-Until=false update \
    && apt-get install -y --no-install-recommends \
        libnss3 libatk1.0-0 libatk-bridge2.0-0 libcups2 libdrm2 libxkbcommon0 libxcomposite1 libxdamage1 \
        libxfixes3 libxrandr2 libgbm1 libasound2 libpango-1.0-0 libcairo2 libx11-6 libxext6 libxcb1 libdbus-1-3 \
        libglib2.0-0 libexpat1 \
        fonts-liberation fonts-dejavu-core \
    && rm -rf /var/lib/apt/lists/*
ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true \
    FUNCTIONS_WORKER_RUNTIME=dotnet
COPY --from=build /publish /home/site/wwwroot
