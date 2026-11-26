#!/bin/sh
# Who may tell this web server the visitor's address: the proxies in front of it, by their networks in
# SHELF_EDGE_PROXIES (CIDR, separated by spaces), whose X-Forwarded-For it then takes; nobody when it is empty —
# visitors come straight here (the local stack), and their own X-Forwarded-For counts for nothing. Written for nginx
# when the container starts (the image's /docker-entrypoint.d); the server tells shelf-api only the address it took.
#
#   sh edge-proxies.sh [<the file to write>]

set -eu

out=${1:-/etc/nginx/snippets/edge-proxies.conf}
: > "$out"
for network in ${SHELF_EDGE_PROXIES:-}; do
    echo "set_real_ip_from $network;" >> "$out"
done

if [ -s "$out" ]; then
    echo "real_ip_header X-Forwarded-For;" >> "$out"
    echo "real_ip_recursive on;" >> "$out"
fi
