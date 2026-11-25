"""The service's configuration, read from the environment once, at start.

  AUDIO_DATABASE_URL        postgresql://user:password@host:port/audio
  MUSIC_YANDEX_SIGN_KEY     Yandex's lyrics signing key
  MUSIC_YANDEX_MP3_SALT     Yandex's file address salt
  AUDIO_YANDEX_TOKEN_FILE   optional: a file with the service account's Yandex token
  AUDIO_YOUTUBE_AUTH_FILE   optional: a file with the service account's YouTube request headers (JSON)
  AUDIO_MAX_BLOB_BYTES      the largest file PUT under /internal/blobs
  AUDIO_MAX_URL_FILE_BYTES  the largest file kept from an address (/internal/files)
  InternalClients__N__Name, InternalClients__N__Key, InternalClients__N__Scopes__M
                            the internal clients allowed in, with their keys and scopes
"""
from __future__ import annotations

import json
import os
import re
from dataclasses import dataclass
from functools import lru_cache


@dataclass(frozen=True)
class InternalClient:
    name: str
    key: str
    scopes: frozenset[str]


@dataclass(frozen=True)
class Settings:
    database_url: str
    yandex_sign_key: bytes
    yandex_mp3_salt: str
    yandex_token: str | None          # the service account's, when configured
    youtube_auth: dict | None         # the service account's, when configured
    max_blob_bytes: int
    max_url_file_bytes: int
    clients: tuple[InternalClient, ...]


def _required(name: str) -> str:
    value = os.environ.get(name, "").strip()
    if not value:
        raise RuntimeError(f"{name} is not set")
    return value


def _bytes(name: str) -> int:
    value = _required(name)
    if not value.isdigit() or int(value) <= 0:
        raise RuntimeError(f"{name} must be a positive number of bytes")
    return int(value)


def _optional_file(name: str) -> str | None:
    path = os.environ.get(name, "").strip()
    if not path:
        return None
    with open(path, encoding="utf-8") as f:
        return f.read().strip() or None


def _clients() -> tuple[InternalClient, ...]:
    """Internal clients in the .NET configuration shape: InternalClients__0__Name and so on."""
    found: dict[int, dict] = {}
    pattern = re.compile(r"InternalClients__(\d+)__(Name|Key|Scopes__(\d+))", re.IGNORECASE)
    for name, value in os.environ.items():
        m = pattern.fullmatch(name)
        if not m:
            continue
        entry = found.setdefault(int(m.group(1)), {"scopes": {}})
        field = m.group(2).lower()
        if field.startswith("scopes"):
            entry["scopes"][int(m.group(3))] = value.strip()
        else:
            entry[field] = value.strip()
    clients = []
    for n in sorted(found):
        e = found[n]
        if not e.get("name") or not e.get("key"):
            raise RuntimeError(f"InternalClients__{n} needs both Name and Key")
        clients.append(InternalClient(e["name"], e["key"], frozenset(s for s in e["scopes"].values() if s)))
    if not clients:
        raise RuntimeError("no InternalClients are configured")
    return tuple(clients)


@lru_cache(maxsize=1)
def settings() -> Settings:
    youtube = _optional_file("AUDIO_YOUTUBE_AUTH_FILE")
    return Settings(
        database_url=_required("AUDIO_DATABASE_URL"),
        yandex_sign_key=_required("MUSIC_YANDEX_SIGN_KEY").encode(),
        yandex_mp3_salt=_required("MUSIC_YANDEX_MP3_SALT"),
        yandex_token=_optional_file("AUDIO_YANDEX_TOKEN_FILE"),
        youtube_auth=json.loads(youtube) if youtube else None,
        max_blob_bytes=_bytes("AUDIO_MAX_BLOB_BYTES"),
        max_url_file_bytes=_bytes("AUDIO_MAX_URL_FILE_BYTES"),
        clients=_clients())
