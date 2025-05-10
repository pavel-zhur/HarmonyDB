#!/bin/sh
set -e

# Create directory for certificates
mkdir -p /etc/nginx/ssl

SHARED=/shared

# Re-use existing certificate if it already exists in the shared volume (avoids
# the Functions containers seeing a different cert after the proxy is restarted)
if [ -f "$SHARED/cosmos-proxy.crt" ] && [ -f "$SHARED/cosmos-proxy.key" ]; then
  echo "Re-using existing certificate from shared volume"
  cp "$SHARED/cosmos-proxy.crt" /etc/nginx/ssl/cosmos-proxy.crt
  cp "$SHARED/cosmos-proxy.key" /etc/nginx/ssl/cosmos-proxy.key
else
  # Create config file for openssl
  cat > /tmp/openssl.cnf <<EOF
[req]
distinguished_name = req_distinguished_name
x509_extensions = v3_req
prompt = no

[req_distinguished_name]
CN = cosmos-proxy

[v3_req]
subjectAltName = @alt_names
basicConstraints = critical,CA:TRUE
keyUsage = critical,keyCertSign,digitalSignature,keyEncipherment
extendedKeyUsage = serverAuth,clientAuth

[alt_names]
DNS.1 = cosmos-proxy
DNS.2 = localhost
EOF
  
  # Generate private key and certificate
  openssl req -x509 -nodes -days 365 -newkey rsa:2048 \
    -keyout /etc/nginx/ssl/cosmos-proxy.key \
    -out /etc/nginx/ssl/cosmos-proxy.crt \
    -config /tmp/openssl.cnf
  
  echo "Certificate generated at /etc/nginx/ssl/cosmos-proxy.crt"
  
  # Copy certificate to shared volume for applications
  if [ -d "$SHARED" ]; then
    cp /etc/nginx/ssl/cosmos-proxy.crt "$SHARED/cosmos-proxy.crt"
    cp /etc/nginx/ssl/cosmos-proxy.key "$SHARED/cosmos-proxy.key"
    echo "Certificate & key copied to shared volume"
  fi
fi

# Start nginx
nginx -g "daemon off;" 