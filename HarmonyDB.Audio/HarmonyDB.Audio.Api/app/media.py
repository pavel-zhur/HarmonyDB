"""Files: a track's sound, a video, a picture, and files others keep here. Fetched from the service
once, kept in the store for good, shared by every caller, and given from there, in parts when
asked (HTTP Range: the player seeks). Each kept file has a validator (ETag) that changes when the
file is replaced; a Range asked with If-Range of another validator gets the whole new file.

A Yandex track's sound fetched anonymously (or by an account without a subscription) is the
30-second preview; it is kept like any file and replaced as soon as a call that may have the
whole file asks for it. A preview never replaces a whole file.
"""
from __future__ import annotations

import asyncio
import hashlib
import re
from typing import AsyncIterator, Awaitable, Callable

import httpx
from starlette.requests import Request
from starlette.responses import Response, StreamingResponse

import config
import store
import yandex
import youtube
from acting import Acting
from errors import HOST_NOT_ALLOWED, NOT_FOUND, RANGE_NOT_SATISFIABLE, TOO_LARGE, UPSTREAM_FAILED, ApiError

# what the services' answers name by address: covers and photos, Yandex's background videos
URL_HOSTS = ("avatars.yandex.net", "music.yandex.net", "strm.yandex.ru", "strm.yandex.net",
             "ytimg.com", "googleusercontent.com", "ggpht.com")
MAX_REDIRECTS = 5

_locks: dict[str, asyncio.Lock] = {}

Got = tuple[str, dict, AsyncIterator[bytes]]
# fetches a file, given the kept row (or None); answers None when the kept file is to stay
Fetch = Callable[[dict | None], Awaitable[Got | None]]


def _url_ok(u: httpx.URL) -> bool:
    host = (u.host or "").lower()
    return u.scheme == "https" and any(host == d or host.endswith("." + d) for d in URL_HOSTS)


async def _from_url(url: str, limit: int) -> Got:
    """A file by its address, following redirects only to the allowed hosts."""
    client = httpx.AsyncClient(timeout=120, follow_redirects=False, headers={"User-Agent": "Mozilla/5.0"})
    try:
        target = httpx.URL(url)
        for _ in range(MAX_REDIRECTS + 1):
            if not _url_ok(target):
                raise ApiError(400, HOST_NOT_ALLOWED, "the address is not on the music services' hosts")
            answer = await client.send(client.build_request("GET", target), stream=True)
            if not answer.is_redirect:
                break
            await answer.aclose()
            target = target.join(answer.headers["location"])
        else:
            raise ApiError(502, UPSTREAM_FAILED, "too many redirects")
        if answer.status_code >= 400:
            await answer.aclose()
            if answer.status_code == 404:
                raise ApiError(404, NOT_FOUND, "there is no file at this address")
            raise ApiError(502, UPSTREAM_FAILED, f"the file did not download: {answer.status_code}")
        if int(answer.headers.get("content-length") or 0) > limit:
            await answer.aclose()
            raise ApiError(413, TOO_LARGE, f"the file is larger than {limit} bytes")
    except BaseException:
        await client.aclose()
        raise
    mime = answer.headers.get("content-type", "application/octet-stream").split(";")[0]

    async def pieces():
        try:
            async for piece in answer.aiter_bytes():
                yield piece
        finally:
            await answer.aclose()
            await client.aclose()
    return mime, {"url": url}, pieces()


async def _ensure(blob_id: str, fetch: Fetch, fresh: bool, limit: int | None = None,
                  replace: Callable[[dict], Awaitable[bool]] | None = None) -> dict:
    """The file kept, fetching it first when it is not, when `fresh`, or when `replace` says the
    kept one is to be replaced. Answers its row."""
    lock = _locks.setdefault(blob_id, asyncio.Lock())
    async with lock:
        row = await store.blob(blob_id)
        if row and not fresh and not (replace and await replace(row)):
            return row
        got = await fetch(row)
        if got is None:
            return row
        mime, info, pieces = got
        return await store.write_blob(blob_id, mime, info, pieces, limit)


def track_blob_id(service: str, kind: str, ident: str) -> str:
    return f"{service}:{kind}:{ident}"


async def track_file(service: str, kind: str, ident: str, acting: Acting, fresh: bool) -> dict:
    """A track's sound (kind audio, both services) or video (YouTube), kept. `acting` is
    anonymous or service: sound and video are never fetched with a person's credentials."""
    blob_id = track_blob_id(service, kind, ident)
    if service == "yandex" and kind == "audio":
        async def fetch(row: dict | None) -> Got | None:
            url, info = await yandex.audio_source(ident, acting)
            if info["preview"] and row and not (row["info"] or {}).get("preview"):
                return None
            return "audio/mpeg", info, yandex.download(url)

        async def preview_to_replace(row: dict) -> bool:
            return bool((row["info"] or {}).get("preview")) and bool(yandex.token_of(acting)) \
                and await yandex.offers_full(ident, acting)
        return await _ensure(blob_id, fetch, fresh, replace=preview_to_replace)
    if service == "youtube" and kind in ("audio", "video"):
        async def fetch_youtube(row: dict | None) -> Got:
            return await youtube.fetch_media(ident, kind == "video", acting)
        return await _ensure(blob_id, fetch_youtube, fresh)
    raise ApiError(404, NOT_FOUND, f"{service} has no {kind} of a track")


async def kept_track_file(service: str, kind: str, ident: str) -> dict | None:
    """The track file's row when it is kept, without fetching anything."""
    return await store.blob(track_blob_id(service, kind, ident))


async def url_file(url: str, fresh: bool) -> dict:
    """A file the services name by its address (a cover, a short video), kept."""
    try:
        parsed = httpx.URL(url)
    except httpx.InvalidURL:
        raise ApiError(400, HOST_NOT_ALLOWED, "not an address") from None
    if not _url_ok(parsed):
        raise ApiError(400, HOST_NOT_ALLOWED, "the address is not on the music services' hosts")
    limit = config.settings().max_url_file_bytes

    async def fetch(row: dict | None) -> Got:
        return await _from_url(url, limit)
    return await _ensure("url:" + hashlib.sha256(url.encode()).hexdigest(), fetch, fresh, limit)


# ---------------------------------------------------------------- others' files

def _blob_id(key: str) -> str:
    return "ext:" + key


async def put_blob(key: str, mime: str, pieces: AsyncIterator[bytes]) -> dict:
    lock = _locks.setdefault(_blob_id(key), asyncio.Lock())
    async with lock:
        return await store.write_blob(_blob_id(key), mime, {}, pieces, config.settings().max_blob_bytes)


async def get_blob(key: str) -> dict:
    row = await store.blob(_blob_id(key))
    if not row:
        raise ApiError(404, NOT_FOUND, "no such blob")
    return row


# ---------------------------------------------------------------- serving

_RANGE = re.compile(r"bytes=(\d*)-(\d*)")


def etag(row: dict) -> str:
    return f'"{row["version"]}"'


def serve(request: Request, row: dict) -> Response:
    """A kept file as an HTTP answer, whole or the part the Range asks for. A Range with an
    If-Range that is not this file's validator gets the whole file."""
    size, mime, tag = row["size"], row["mime"], etag(row)
    headers = {"Accept-Ranges": "bytes", "ETag": tag}
    start, end, status = 0, size - 1, 200
    m = _RANGE.fullmatch(request.headers.get("range", "").strip())
    if_range = request.headers.get("if-range")
    if m and (m.group(1) or m.group(2)) and (if_range is None or if_range.strip() == tag):
        if m.group(1):
            start = int(m.group(1))
            end = min(int(m.group(2)), size - 1) if m.group(2) else size - 1
        else:                                   # the last N bytes
            start = max(size - int(m.group(2)), 0)
        if start >= size or start > end:
            raise ApiError(416, RANGE_NOT_SATISFIABLE, f"the file has {size} bytes",
                           headers={"Content-Range": f"bytes */{size}", "ETag": tag})
        status = 206
        headers["Content-Range"] = f"bytes {start}-{end}/{size}"
    headers["Content-Length"] = str(end - start + 1)
    if request.method == "HEAD" or size == 0:
        return Response(status_code=status, media_type=mime, headers=headers)
    return StreamingResponse(store.read_blob(row["version"], start, end), status_code=status,
                             media_type=mime, headers=headers)
