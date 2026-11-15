#!/bin/sh
# Runs when the container starts: gives the published site its settings.
# The settings file is published once with the site and replaced for every place the site runs in; the service worker
# checks every file against the hash listed for it, so the listed hashes of the replaced files are updated as well.
#
# With ALLOW_BAD_HASH=true (the local way in without Telegram) the site also gets BAD_HASH in its settings and the
# page /enter.html. Without it the site gets neither.
set -e

root=/usr/share/nginx/html
manifests="service-worker-assets.js service-worker-assets-compat.js"
settings=/tmp/appsettings.json

hash() { openssl dgst -sha256 -binary "$1" | openssl base64 -A; }

if [ "$ALLOW_BAD_HASH" = "true" ]; then
    jq --arg hash "$BAD_HASH" '. + { BadHash: $hash }' /config/appsettings.json > "$settings"
else
    cp /config/appsettings.json "$settings"
fi

replace() {
    old=$(hash "$root/$1")
    cp "$settings" "$root/$1"
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

# enter.html: stores the local identity in the browser, the way the login page stores the one it gets from Telegram,
# and opens the site. The Authorization service accepts it by its hash (BAD_HASH).
rm -f "$root/enter.html"
for manifest in $manifests; do
    sed -i 's| { "hash": "sha256-[^"]*", "url": "enter.html" },||' "$root/$manifest"
done

if [ "$ALLOW_BAD_HASH" = "true" ] && [ -n "$LOCAL_IDENTITY_ID" ]; then
    identity=$(jq -n -c --argjson id "$LOCAL_IDENTITY_ID" --arg first_name "$LOCAL_IDENTITY_FIRST_NAME" \
        --arg username "$LOCAL_IDENTITY_USERNAME" --arg hash "$BAD_HASH" \
        '{ id: $id, first_name: $first_name, username: $username, auth_date: 123, hash: $hash }')

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
        sed -i "s|\"assets\": \\[|\"assets\": [ { \"hash\": \"sha256-$entry_hash\", \"url\": \"enter.html\" },|" "$root/$manifest"
    done
fi
