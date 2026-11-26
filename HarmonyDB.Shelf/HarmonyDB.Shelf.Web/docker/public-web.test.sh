#!/bin/sh
# The public page's web server against a stand-in for shelf-api (run when the image is built: the Dockerfile's stage
# public-test): a call goes on as a guest's, whatever cookies and credentials the browser sends, and no cookie comes
# back — a signed-in person's session never reaches the API through this page; and the API is told the reader's
# address as the server takes it — the connection's, or a trusted proxy's word (edge-proxies.sh) — never what a reader
# wrote in X-Forwarded-For.
#
#   sh public-web.test.sh <public.nginx.conf> <edge-proxies.sh>

set -eu

conf=$1
edge=$2
work=$(mktemp -d)

# the page's server, with shelf-api at the stand-in's address
sed -e '/resolver 127.0.0.11/d' -e 's|set $api http://shelf-api.shelf-web;|set $api http://127.0.0.1:8081;|' "$conf" > /etc/nginx/conf.d/default.conf
grep -q 'set $api http://127.0.0.1:8081;' /etc/nginx/conf.d/default.conf

# the stand-in: says what reached it, and hands a cookie back
cat > /etc/nginx/conf.d/stand-in.conf <<'EOF'
server {
    listen 127.0.0.1:8081;
    location / {
        add_header Set-Cookie "shelf-csrf=from-the-api; Path=/" always;
        return 200 "reached $request_method $uri cookie=[$http_cookie] authorization=[$http_authorization] for=[$http_x_forwarded_for]\n";
    }
}
EOF
echo '<!doctype html><title>page</title>' > /usr/share/nginx/html/public.html

# no proxy in front: readers come straight here
SHELF_EDGE_PROXIES='' sh "$edge" /etc/nginx/snippets/edge-proxies.conf

nginx -t -q
nginx

token=AbCdEfGhIjKlMnOpQrStUvWx
failed=0

ask() {
    curl -s -o "$work/body" -D "$work/head" -X "$1" "http://127.0.0.1:80$2" \
        -H 'Cookie: shelf-session=a-person; shelf-csrf=theirs' -H 'Authorization: Bearer a-key' -H 'Content-Type: application/json' \
        ${3:+--data "$3"}
}

passes() {
    ask "$@"
    if ! grep -q "^reached $1 " "$work/body"; then
        echo "FAIL: $1 $2 should reach shelf-api: $(head -c 200 "$work/body")"; failed=1; return
    fi
    if ! grep -q 'cookie=\[\] authorization=\[\]' "$work/body"; then
        echo "FAIL: $1 $2 went on with the browser's cookie or credentials: $(cat "$work/body")"; failed=1
    fi
    if grep -qi '^set-cookie' "$work/head"; then
        echo "FAIL: $1 $2 handed the API's cookie back"; failed=1
    fi
}

# reading and asking, of any address: never with the person's cookie or key, never a cookie back
passes GET  /api/app/configuration
passes GET  "/api/chords/ha-153cebd750cd378cfafe36d26c4ffb83?kinds=sheet"
passes POST /api/media/chords '{"id":"ha-1"}'
passes GET  /api/media/kPUdpP5acv6jq1L0r1in2znEnsLTNPtOrB3nxmkAsk-VgnYmbws1yMyZXFup-bQp8uIImrq2mXdgk
passes GET  /api/auth/session
passes POST /api/auth/sign-out
passes DELETE /api/share/$token

# the reader's address the API is told: its own, whatever it wrote
told() {
    curl -s -o "$work/body" "http://127.0.0.1:80/api/app/configuration" -H "X-Forwarded-For: $1"
    if ! grep -q "for=\[$2\]" "$work/body"; then
        echo "FAIL: with X-Forwarded-For \"$1\" the API should be told $2: $(cat "$work/body")"; failed=1
    fi
}

told "203.0.113.9" 127.0.0.1

# a proxy in front, trusted (here the connection's own address): its word is taken, a reader's own words before it are not
SHELF_EDGE_PROXIES='127.0.0.1/32 10.0.0.0/8' sh "$edge" /etc/nginx/snippets/edge-proxies.conf
nginx -t -q
nginx -s reload
sleep 1
told "198.51.100.7, 203.0.113.9" 203.0.113.9
told "198.51.100.7, 203.0.113.9, 10.1.2.3" 203.0.113.9

nginx -s quit
rm -rf "$work"
[ "$failed" = 0 ] && echo "public-web: everything goes on as a guest's"
exit "$failed"
