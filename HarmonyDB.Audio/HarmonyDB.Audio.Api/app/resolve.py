"""A link a person gives, of either service, as what it names: {service, type, id}."""
from __future__ import annotations

import urllib.parse

from errors import LINK_UNKNOWN, ApiError


def resolve(url: str) -> dict:
    u = urllib.parse.urlparse(url.strip())
    host = (u.hostname or "").lower()
    path = [p for p in u.path.split("/") if p]
    q = urllib.parse.parse_qs(u.query)

    if host.startswith("music.yandex.") or host.endswith(".music.yandex.ru"):
        s = "yandex"
        if path[:1] == ["playlists"] and len(path) > 1:            # a shared link: by uuid
            return {"service": s, "type": "playlist", "id": path[1]}
        if path[:1] == ["users"] and len(path) > 3 and path[2] == "playlists":
            return {"service": s, "type": "playlist", "id": f"{path[1]}:{path[3]}"}
        if path[:1] == ["album"] and len(path) > 1:
            if len(path) > 3 and path[2] == "track":
                return {"service": s, "type": "track", "id": path[3]}
            return {"service": s, "type": "album", "id": path[1]}
        if path[:1] == ["artist"] and len(path) > 1:
            return {"service": s, "type": "artist", "id": path[1]}
        if path[:1] == ["track"] and len(path) > 1:
            return {"service": s, "type": "track", "id": path[1]}
        raise ApiError(400, LINK_UNKNOWN, "a Yandex Music link that names nothing known")

    if host == "youtu.be" or host == "youtube.com" or host.endswith(".youtube.com"):
        s = "youtube"
        v, lst = (q.get("v") or [None])[0], (q.get("list") or [None])[0]
        if host == "youtu.be" and path:
            return {"service": s, "type": "track", "id": path[0]}
        if path[:1] == ["watch"] and v:
            return {"service": s, "type": "track", "id": v}
        if path[:1] == ["playlist"] and lst:
            return {"service": s, "type": "playlist", "id": lst}
        if path[:1] == ["channel"] and len(path) > 1:
            return {"service": s, "type": "artist", "id": path[1]}
        if path[:1] == ["browse"] and len(path) > 1:
            return {"service": s, "type": "album", "id": path[1]}
        if lst:
            return {"service": s, "type": "playlist", "id": lst}
        if v:
            return {"service": s, "type": "track", "id": v}
        raise ApiError(400, LINK_UNKNOWN, "a YouTube link that names nothing known")

    raise ApiError(400, LINK_UNKNOWN, "not a link of Yandex Music or YouTube")
