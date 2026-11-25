"""Internal clients: every request carries X-Internal-Key, the key of one configured client, and
may use only the areas (scopes) that client has.

Scopes: `audio.read` - navigation and the cache of answers; `audio.media` - sound, video, files and blobs.
"""
from __future__ import annotations

import hmac

from fastapi import Depends
from fastapi.security import APIKeyHeader

import config
from errors import KEY_INVALID, KEY_MISSING, SCOPE_MISSING, ApiError

READ = "audio.read"
MEDIA = "audio.media"

_key_header = APIKeyHeader(name="X-Internal-Key", auto_error=False,
                           description="The key of an internal client, as configured.")


def client_of(key: str | None) -> config.InternalClient:
    if not key:
        raise ApiError(401, KEY_MISSING, "X-Internal-Key is required")
    found = None
    given = key.encode()
    for client in config.settings().clients:
        # every key is compared, in constant time, so the timing tells nothing
        if hmac.compare_digest(given, client.key.encode()):
            found = client
    if found is None:
        raise ApiError(401, KEY_INVALID, "X-Internal-Key is not a known client's key")
    return found


def require(scope: str):
    """A dependency: the request's client, which must have `scope`."""
    def check(key: str | None = Depends(_key_header)) -> config.InternalClient:
        client = client_of(key)
        if scope not in client.scopes:
            raise ApiError(403, SCOPE_MISSING, f"the client has no scope {scope}")
        return client
    return check
