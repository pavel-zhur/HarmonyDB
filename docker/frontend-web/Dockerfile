FROM nginx:alpine

# Arguments for release settings
ARG RELEASE_VERSION
ARG RELEASE_FILENAME
ARG ASPNETCORE_ENVIRONMENT
ARG CONFIG_JSON

# Install curl for downloading releases, unzip for extracting them,
# and other utilities for hash calculation and JSON handling
RUN apk add --no-cache curl unzip sed xxd coreutils jq

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

# Copy and apply the config file during build
COPY ${CONFIG_JSON} /tmp/frontend-web.json
RUN if [ -f "/usr/share/nginx/html/appsettings.json" ] && [ -f "/tmp/frontend-web.json" ]; then \
    # Create backup of original appsettings.json
    cp /usr/share/nginx/html/appsettings.json /usr/share/nginx/html/appsettings.json.original && \
    # Merge the two JSON files with jq
    jq -s '.[0] * .[1]' /usr/share/nginx/html/appsettings.json /tmp/frontend-web.json > /tmp/merged.json && \
    cp /tmp/merged.json /usr/share/nginx/html/appsettings.json && \
    # Calculate SHA256 hashes for service worker updates
    if [ -f "/usr/share/nginx/html/service-worker-assets.js" ] && [ -f "/usr/share/nginx/html/service-worker-assets-compat.js" ]; then \
        echo "Updating service worker file hashes..." && \
        # Calculate hex hashes
        OLD_HASH=$(sha256sum "/usr/share/nginx/html/appsettings.json.original" | cut -d ' ' -f 1) && \
        NEW_HASH=$(sha256sum "/usr/share/nginx/html/appsettings.json" | cut -d ' ' -f 1) && \
        # Convert hex to binary and then to base64 without newlines
        OLD_HASH_B64=$(echo -n $OLD_HASH | xxd -r -p | base64 -w 0 | sed 's/\//\\\//g') && \
        NEW_HASH_B64=$(echo -n $NEW_HASH | xxd -r -p | base64 -w 0 | sed 's/\//\\\//g') && \
        echo "Old hash: $OLD_HASH" && \
        echo "New hash: $NEW_HASH" && \
        echo "Old hash (base64): $OLD_HASH_B64" && \
        echo "New hash (base64): $NEW_HASH_B64" && \
        # Update service worker files with properly quoted patterns
        sed -i "s|$OLD_HASH_B64|$NEW_HASH_B64|g" "/usr/share/nginx/html/service-worker-assets.js" && \
        sed -i "s|$OLD_HASH_B64|$NEW_HASH_B64|g" "/usr/share/nginx/html/service-worker-assets-compat.js" && \
        echo "Service worker hashes updated successfully!"; \
    else \
        echo "Service worker asset files not found, skipping hash update."; \
    fi && \
    # Clean up temporary files
    rm /tmp/frontend-web.json /tmp/merged.json /usr/share/nginx/html/appsettings.json.original; \
    else \
    echo "WARNING: Config files not found. appsettings.json will not be updated."; \
    fi

EXPOSE 80

# Nginx image already has an entrypoint defined 