#!/bin/sh
# Run by nginx's image when the public page's container starts (/docker-entrypoint.d): puts into public.html what the
# page is told — the site's own origin (the messengers' preview picture) and the app's origin (where the page
# invites). And the preview of each address: the head's preview
# tags (the title, the description, og:title, og:description, og:image…, twitter:card) become the stub of an include
# (public.nginx.conf, /_page-preview) that puts in their place what shelf-api writes for the address, when it writes
# anything. robots.txt gets the sitemap's address.
#
#   SHELF_SITE_ORIGIN         this site, e.g. https://… (no slash at the end)
#   SHELF_APP_ORIGIN          the app, e.g. https://…

set -eu

origin='^https?://[A-Za-z0-9.-]+(:[0-9]+)?$'

check() {
    if ! printf '%s' "$2" | grep -Eq "$3"; then
        echo "public-site: $1 is not set right: '$2'" >&2
        exit 1
    fi
}

check SHELF_SITE_ORIGIN "${SHELF_SITE_ORIGIN:-}" "$origin"
check SHELF_APP_ORIGIN "${SHELF_APP_ORIGIN:-}" "$origin"

# from the image's copies, so that a restart with other values puts them anew
sed -e "s|__SITE_ORIGIN__|$SHELF_SITE_ORIGIN|g" \
    -e "s|__APP_ORIGIN__|$SHELF_APP_ORIGIN|g" \
    /usr/share/nginx/public.template.html > /tmp/public.html

# the preview tags, one to a line as the page is built: the stub, where the first of them was; a page without its
# title or its picture is not the page this is written for
if ! awk '
    /<title>|<meta name="description"|<meta property="og:(title|description|image)|<meta name="twitter:card"/ {
        if (!placed) { out[++n] = "\001"; placed = 1 }
        stub = stub $0 "\n"
        if ($0 ~ /<title>/) title = 1
        if ($0 ~ /property="og:image"/) image = 1
        next
    }
    { out[++n] = $0 }
    END {
        if (!title || !image) exit 1
        for (i = 1; i <= n; i++) {
            if (out[i] == "\001")
                printf "    <!--# block name=\"preview\" -->\n%s    <!--# endblock -->\n    <!--# include virtual=\"/_page-preview$page_path\" stub=\"preview\" -->\n", stub
            else
                print out[i]
        }
    }' /tmp/public.html > /usr/share/nginx/html/public.html; then
    echo "public-site: the page's head has not the preview tags it is built with" >&2
    exit 1
fi
rm /tmp/public.html

{ cat /usr/share/nginx/public-robots.txt; printf '\nSitemap: %s/sitemap.xml\n' "$SHELF_SITE_ORIGIN"; } > /usr/share/nginx/html/robots.txt
