#!/bin/sh
set -e

# Script to replace settings in appsettings.json with environment variables
# and update service worker hashes

CONFIG_FILE="/usr/share/nginx/html/appsettings.json"
SW_ASSETS="/usr/share/nginx/html/service-worker-assets.js"
SW_ASSETS_COMPAT="/usr/share/nginx/html/service-worker-assets-compat.js"

if [ -f "$CONFIG_FILE" ]; then
  echo "Creating backup of original appsettings.json..."
  cp "$CONFIG_FILE" "${CONFIG_FILE}.bak"
  
  echo "Configuring appsettings.json with environment variables..."
  
  # Replace service URLs if environment variables are set
  if [ ! -z "$Services__IndexApi" ]; then
    sed -i "s|\"IndexApi\": \"[^\"]*\"|\"IndexApi\": \"$Services__IndexApi\"|g" $CONFIG_FILE
  fi
  
  if [ ! -z "$Services__AuthorizationApi" ]; then
    sed -i "s|\"AuthorizationApi\": \"[^\"]*\"|\"AuthorizationApi\": \"$Services__AuthorizationApi\"|g" $CONFIG_FILE
  fi
  
  if [ ! -z "$Services__IllustrationsApi" ]; then
    sed -i "s|\"IllustrationsApi\": \"[^\"]*\"|\"IllustrationsApi\": \"$Services__IllustrationsApi\"|g" $CONFIG_FILE
  fi
  
  if [ ! -z "$Services__BillingApi" ]; then
    sed -i "s|\"BillingApi\": \"[^\"]*\"|\"BillingApi\": \"$Services__BillingApi\"|g" $CONFIG_FILE
  fi
  
  if [ ! -z "$Services__CollectivesApi" ]; then
    sed -i "s|\"CollectivesApi\": \"[^\"]*\"|\"CollectivesApi\": \"$Services__CollectivesApi\"|g" $CONFIG_FILE
  fi
  
  if [ ! -z "$Services__SourcesSelfApi" ]; then
    sed -i "s|\"SourcesSelfApi\": \"[^\"]*\"|\"SourcesSelfApi\": \"$Services__SourcesSelfApi\"|g" $CONFIG_FILE
  fi
  
  if [ ! -z "$Services__VideosApi" ]; then
    sed -i "s|\"VideosApi\": \"[^\"]*\"|\"VideosApi\": \"$Services__VideosApi\"|g" $CONFIG_FILE
  fi
  
  if [ ! -z "$Services__PdfsApi" ]; then
    sed -i "s|\"PdfsApi\": \"[^\"]*\"|\"PdfsApi\": \"$Services__PdfsApi\"|g" $CONFIG_FILE
  fi
  
  echo "Configuration updated successfully!"
  
  # Now update service worker hash files if they exist
  if [ -f "$SW_ASSETS" ] && [ -f "$SW_ASSETS_COMPAT" ]; then
    echo "Updating service worker file hashes..."
    
    # Calculate SHA256 hashes
    OLD_HASH=$(sha256sum "${CONFIG_FILE}.bak" | cut -d ' ' -f 1)
    NEW_HASH=$(sha256sum "$CONFIG_FILE" | cut -d ' ' -f 1)
    
    # Convert to base64 with proper escaping
    OLD_HASH_B64=$(printf "$OLD_HASH" | xxd -r -p | base64 | sed 's/\//\\\//g')
    NEW_HASH_B64=$(printf "$NEW_HASH" | xxd -r -p | base64 | sed 's/\//\\\//g')
    
    echo "Old hash (base64): $OLD_HASH_B64"
    echo "New hash (base64): $NEW_HASH_B64"
    
    # Update service worker files
    sed -i "s/$OLD_HASH_B64/$NEW_HASH_B64/g" "$SW_ASSETS"
    sed -i "s/$OLD_HASH_B64/$NEW_HASH_B64/g" "$SW_ASSETS_COMPAT"
    
    echo "Service worker hashes updated successfully!"
  else
    echo "Service worker asset files not found, skipping hash update."
  fi
else
  echo "Warning: appsettings.json not found at $CONFIG_FILE"
fi 