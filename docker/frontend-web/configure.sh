#!/bin/sh
# Runs when the container starts: gives the published site its settings and the page that enters without Telegram.
# The settings file is published once with the site and replaced for every place the site runs in; the service worker
# checks every file against the hash listed for it, so the listed hashes of the replaced files are updated as well.
set -e

root=/usr/share/nginx/html
manifests="service-worker-assets.js service-worker-assets-compat.js"

hash() { openssl dgst -sha256 -binary "$1" | openssl base64 -A; }

replace() {
    old=$(hash "$root/$1")
    cp /config/appsettings.json "$root/$1"
    new=$(hash "$root/$1")
    for manifest in $manifests; do
        sed -i "s|sha256-$old|sha256-$new|g" "$root/$manifest"
    done
    # the precompressed copies of the published file
    rm -f "$root/$1.br" "$root/$1.gz"
}

replace appsettings.json
# loaded on top of the first one when the environment is Development
replace appsettings.Development.json

# enter.html: stores the identity of /config/local-identity.json in the browser, the way the login page stores
# the one it gets from Telegram, and opens the site. The Authorization service accepts it by its hash (BadHash).
identity=$(tr -d '\n' < /config/local-identity.json)
cat > "$root/enter.html" <<EOF
<!DOCTYPE html>
<html>
<head><meta charset="utf-8"><title>Вход</title></head>
<body>
<script>
    localStorage.setItem('authv2', JSON.stringify($identity));
    location.replace('/');
</script>
</body>
</html>
EOF

# The page is listed for the service worker, which otherwise answers every address it does not know with the site itself.
entry_hash=$(hash "$root/enter.html")
for manifest in $manifests; do
    sed -i 's| { "hash": "sha256-[^"]*", "url": "enter.html" },||' "$root/$manifest"
    sed -i "s|\"assets\": \\[|\"assets\": [ { \"hash\": \"sha256-$entry_hash\", \"url\": \"enter.html\" },|" "$root/$manifest"
done
