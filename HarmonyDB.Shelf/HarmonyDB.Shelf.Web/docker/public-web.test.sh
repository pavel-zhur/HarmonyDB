#!/bin/sh
# The public page's web server against a stand-in for shelf-api (run when the image is built: the Dockerfile's stage
# public-test): each call the page makes goes on — as a guest, whatever cookies and credentials the browser sends, and
# no cookie comes back; anything else under /api stops here with 404. Fails when the list of what goes on widens.
#
#   sh public-web.test.sh <public.nginx.conf>

set -eu

conf=$1
work=$(mktemp -d)

# the page's server, with shelf-api at the stand-in's address
sed -e '/resolver 127.0.0.11/d' -e 's|set $api http://shelf-api;|set $api http://127.0.0.1:8081;|' "$conf" > /etc/nginx/conf.d/default.conf
grep -q 'set $api http://127.0.0.1:8081;' /etc/nginx/conf.d/default.conf

# the stand-in: says what reached it, and hands a cookie back
cat > /etc/nginx/conf.d/stand-in.conf <<'EOF'
server {
    listen 127.0.0.1:8081;
    location / {
        add_header Set-Cookie "shelf-csrf=from-the-api; Path=/" always;
        return 200 "reached $request_method $uri cookie=[$http_cookie] authorization=[$http_authorization]\n";
    }
}
EOF
echo '<!doctype html><title>page</title>' > /usr/share/nginx/html/public.html

nginx -t -q
nginx

token=AbCdEfGhIjKlMnOpQrStUvWx
uuid=0be02e7c-6bd1-4ecd-939a-75a67fb4dc20
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

stops() {
    ask "$@"
    if grep -q '^reached' "$work/body" || ! head -1 "$work/head" | grep -q ' 404'; then
        echo "FAIL: $1 $2 should stop with 404: $(head -1 "$work/head")"; failed=1
    fi
}

# what the page makes
passes GET  /api/app/configuration
passes GET  /api/share/$token
passes GET  "/api/share/$token/chords?kinds=sheet"
passes POST /api/share/$token/media
passes GET  "/api/chords/ha-153cebd750cd378cfafe36d26c4ffb83?kinds=sheet"
passes GET  /api/chords/mj-e1e9acdf-c303-42b0-98df-0123456789ab
passes GET  /api/chords/icons/ha-icon
passes GET  /api/playlists/$uuid
passes GET  "/api/playlists/$uuid/songs?sort=order&offset=0&limit=200"
passes GET  /api/playlists/$uuid/songs/$uuid
passes GET  /api/illustrations/images/abc123/small
passes POST /api/media/chords '{"id":"ha-1"}'
passes GET  /api/media/eyJLIjoiYmxvYiJ9.uwjAckjOsLC6bXANl1dlRWbB5e351L2

# what it does not
stops GET    /api/auth/session
stops POST   /api/auth/sign-out
stops GET    /api/me
stops GET    /api/app/health
stops GET    /api/app/version
stops POST   /api/share '{"kind":"chords"}'
stops GET    /api/share/mine
stops DELETE /api/share/$token
stops GET    /api/share/$token/other
stops POST   /api/chords/headers
stops DELETE /api/chords/ha-1
stops GET    /api/playlists
stops GET    /api/playlists/public
stops POST   /api/playlists/import
stops DELETE /api/playlists/$uuid
stops PATCH  /api/playlists/$uuid
stops POST   /api/playlists/$uuid/follow
stops GET    /api/playlists/$uuid/members/$uuid
stops POST   /api/playlists/$uuid/songs
stops PUT    /api/playlists/$uuid/songs/$uuid/main
stops GET    /api/playlists/invitations/$token
stops POST   /api/media/tracks
stops GET    /api/media/chords
stops POST   /api/sync
stops GET    /api/library
stops GET    /api/public/sitemap
stops GET    /api/public/preview/

nginx -s quit
rm -rf "$work"
[ "$failed" = 0 ] && echo "public-web: the list of what goes on to shelf-api is as it should be"
exit "$failed"
