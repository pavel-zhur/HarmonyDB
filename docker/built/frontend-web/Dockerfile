FROM nginx:alpine

# Arguments for release settings
ARG RELEASE_VERSION
ARG RELEASE_FILENAME
ARG ASPNETCORE_ENVIRONMENT

# Install curl for downloading releases, unzip for extracting them,
# and other utilities for hash calculation
RUN apk add --no-cache curl unzip sed xxd coreutils

# Create app directory
WORKDIR /app

# Configure nginx
COPY frontend-web/nginx.conf /etc/nginx/conf.d/default.conf

# Download and extract the release package
RUN if [ ! -z "$RELEASE_VERSION" ] && [ ! -z "$RELEASE_FILENAME" ]; then \
    echo "Downloading release ${RELEASE_VERSION}/${RELEASE_FILENAME}" && \
    curl -L \
    "https://github.com/pavel-zhur/HarmonyDB/releases/download/${RELEASE_VERSION}/${RELEASE_FILENAME}" \
    > /tmp/release.zip && \
    unzip -o /tmp/release.zip -d /tmp/extract && \
    # Copy the wwwroot folder to nginx html directory
    cp -r /tmp/extract/wwwroot/* /usr/share/nginx/html/ && \
    # For debugging purposes
    echo "Files in /usr/share/nginx/html:" && \
    ls -la /usr/share/nginx/html && \
    # Clean up
    rm -rf /tmp/extract /tmp/release.zip; \
    else \
    echo "ERROR: Required arguments missing. Set RELEASE_VERSION and RELEASE_FILENAME"; \
    exit 1; \
    fi

# Copy a simple script to replace environment variables in appsettings.json at runtime
# and update service worker hashes
COPY frontend-web/replace-config.sh /docker-entrypoint.d/40-replace-config.sh
RUN chmod +x /docker-entrypoint.d/40-replace-config.sh

EXPOSE 80

# Nginx image already has an entrypoint defined 