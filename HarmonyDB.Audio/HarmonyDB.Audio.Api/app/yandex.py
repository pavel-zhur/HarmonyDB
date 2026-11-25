"""Yandex Music: its internal API.

  Authorization: OAuth <token>; X-Yandex-Music-Client: YandexMusicAndroid/24026561
  lyrics sign = base64( HMAC_SHA256(KEY, trackId + ts) ),  KEY from MUSIC_YANDEX_SIGN_KEY
  a file:  download-info -> its XML -> https://{host}/get-mp3/{md5(SALT + path[1:] + s)}/{ts}{path},
           SALT from MUSIC_YANDEX_MP3_SALT

Each call acts as its Acting says: anonymously (no token), as a person (the token given in the
call), or as the service account (the configured token; with none configured, anonymously).
Anonymous Yandex answers searches, pages and lyrics alike, but gives only a 30-second preview
(mp3 128 kbps) of a track's sound.

Read-only with respect to the accounts, with one exception: recommendations for a set of tracks
make a temporary playlist in the service account and delete it again (try/finally).
"""
from __future__ import annotations

import base64
import hashlib
import hmac
import json
import re
import time
import xml.etree.ElementTree as ET
from typing import AsyncIterator

import httpx

import cache
import config
from acting import Acting
from cache import Answer
from common import once_in_a_row
from errors import (ACCOUNT_REQUIRED, BAD_REQUEST, CREDENTIALS_REJECTED, NO_FILE, NOT_FOUND, SERVICE_ACCOUNT_MISSING,
                    SERVICE_ACCOUNT_REJECTED, SERVICE_REQUIRED, UPSTREAM_FAILED, ApiError)

API = "https://api.music.yandex.net"
CLIENT = "YandexMusicAndroid/24026561"
S = "yandex"


def token_of(acting: Acting) -> str | None:
    """The token this call acts with; None is anonymous."""
    if acting.mode == "person":
        return acting.credentials
    if acting.mode == "service":
        return config.settings().yandex_token
    return None


class _Session:
    """One call's conversation with Yandex, under one token (or none)."""

    def __init__(self, acting: Acting, timeout: float):
        self.mode = acting.mode
        self.token = token_of(acting)
        self.http = httpx.AsyncClient(timeout=timeout, follow_redirects=True)

    async def __aenter__(self):
        return self

    async def __aexit__(self, *exc):
        await self.http.aclose()

    def headers(self) -> dict:
        h = {"X-Yandex-Music-Client": CLIENT, "Accept-Language": "ru", "User-Agent": "Yandex-Music-API"}
        if self.token:
            h["Authorization"] = f"OAuth {self.token}"
        return h

    def require_account(self) -> None:
        """For what Yandex gives an account only."""
        if not self.token:
            raise ApiError(403, ACCOUNT_REQUIRED, "Yandex gives this to an account only")

    def _refused(self, r: httpx.Response) -> ApiError:
        # 401 is about the token; 403 is also Yandex's answer to too many questions
        if r.status_code == 401:
            if not self.token:
                return ApiError(403, ACCOUNT_REQUIRED, "Yandex gives this to an account only")
            if self.mode == "person":
                return ApiError(403, CREDENTIALS_REJECTED, "Yandex refused the person's token")
            return ApiError(503, SERVICE_ACCOUNT_REJECTED, "Yandex refused the service account")
        # 410: what was there is deleted (a playlist)
        if r.status_code in (404, 410):
            return ApiError(404, NOT_FOUND, "Yandex has no such thing")
        if r.status_code == 400:
            return ApiError(400, BAD_REQUEST, f"Yandex: {r.text[:200]}")
        return ApiError(502, UPSTREAM_FAILED, f"Yandex answered {r.status_code}: {r.text[:200]}")

    async def get(self, path: str, params=None):
        r = await self.http.get(f"{API}/{path}", params=params, headers=self.headers())
        if r.status_code >= 400:
            raise self._refused(r)
        return _result(r)

    async def post(self, path: str, json_body=None, params=None, data=None):
        r = await self.http.post(f"{API}/{path}", json=json_body, params=params, data=data, headers=self.headers())
        if r.status_code >= 400:
            raise self._refused(r)
        return _result(r)

    async def fetch_text(self, url: str, headers: dict | None = None) -> str:
        """A file Yandex names by its address (lyrics, a file's description)."""
        r = await self.http.get(url, headers=headers)
        if r.status_code >= 400:
            raise ApiError(502, UPSTREAM_FAILED, f"Yandex's file did not download: {r.status_code}")
        return r.text

    async def uid(self) -> str:
        self.require_account()
        uid = ((await self.get("account/status")).get("account") or {}).get("uid")
        if not uid:
            raise ApiError(502, UPSTREAM_FAILED, "Yandex told no account for the token")
        return uid


def _result(r: httpx.Response):
    try:
        return r.json().get("result")
    except ValueError:
        raise ApiError(502, UPSTREAM_FAILED, f"Yandex answered {r.status_code} with no JSON") from None


def _session(acting: Acting, timeout: float = 30) -> _Session:
    return _Session(acting, timeout)


def img(uri: str | None, size: str = "200x200") -> str | None:
    if not uri:
        return None
    uri = uri.replace("%%", size)
    return uri if uri.startswith("http") else "https://" + uri


# ---------------------------------------------------------------- shapers

def n_artist(a: dict) -> dict:
    cov = (a.get("cover") or {}).get("uri") or a.get("ogImage")
    return {"id": str(a.get("id")), "name": a.get("name"), "cover": img(cov),
            "genres": a.get("genres") or [], "likes": a.get("likesCount"),
            "counts": a.get("counts") or {}, "various": a.get("various")}


def n_album(al: dict) -> dict:
    return {"id": str(al.get("id")), "title": al.get("title"), "version": al.get("version"),
            "type": al.get("type") or al.get("metaType"), "year": al.get("year"),
            "releaseDate": al.get("releaseDate"), "genre": al.get("genre"),
            "cover": img(al.get("coverUri") or al.get("ogImage")), "trackCount": al.get("trackCount"),
            "likes": al.get("likesCount"), "artists": [n_artist(x) for x in (al.get("artists") or [])],
            "labels": [l.get("name") for l in (al.get("labels") or []) if isinstance(l, dict)]}


def n_track(t: dict) -> dict:
    albums = [n_album(a) for a in (t.get("albums") or [])]
    li = t.get("lyricsInfo") or {}
    cover = t.get("coverUri") or t.get("ogImage") or ((t.get("albums") or [{}])[0].get("coverUri"))
    return {
        "id": str(t.get("id")), "realId": str(t.get("realId") or t.get("id")),
        "title": t.get("title"), "version": t.get("version"), "durationMs": t.get("durationMs"),
        "available": t.get("available", True),
        # lyricsAvailable is unreliable; lyricsInfo is the real signal
        "lyricsAvailable": bool(li.get("hasAvailableSyncLyrics") or li.get("hasAvailableTextLyrics")
                                or t.get("lyricsAvailable")),
        "lyricsSynced": bool(li.get("hasAvailableSyncLyrics")),
        "cover": img(cover), "coverBig": img(cover, "400x400"),
        "artists": [n_artist(a) for a in (t.get("artists") or [])],
        "albums": albums, "album": albums[0] if albums else None,
        "year": albums[0].get("year") if albums else None,
        "genre": albums[0].get("genre") if albums else None,
        "best": t.get("best"), "label": (t.get("major") or {}).get("name"),
        "trackSource": t.get("trackSource"), "type": t.get("type"),
        "explicit": bool((t.get("contentWarning") or "")) or None,
        "previewDurationMs": t.get("previewDurationMs"), "fileSize": t.get("fileSize") or None,
        "r128": t.get("r128"), "fade": t.get("fade"), "colors": t.get("derivedColors") or {},
        "backgroundVideo": t.get("backgroundVideoUri"),
        "specialAudio": t.get("specialAudioResources") or [], "disclaimers": t.get("disclaimers") or [],
        "availableForPremium": t.get("availableForPremiumUsers"),
        "availableForOptions": t.get("availableForOptions") or [], "storageDir": t.get("storageDir"),
    }


def n_playlist(p: dict) -> dict:
    cov = (p.get("cover") or {})
    uri = cov.get("uri") or (cov.get("itemsUri") or [None])[0] or p.get("ogImage")
    uid = p.get("uid") or (p.get("owner") or {}).get("uid")
    return {"id": f"{uid}:{p.get('kind')}", "uid": str(uid) if uid is not None else None, "kind": p.get("kind"),
            "uuid": p.get("playlistUuid"), "title": p.get("title"), "description": p.get("description"),
            "trackCount": p.get("trackCount"), "durationMs": p.get("durationMs"),
            "likes": p.get("likesCount"), "cover": img(uri, "300x300"),
            "owner": (p.get("owner") or {}).get("name") or (p.get("owner") or {}).get("login"),
            "modified": p.get("modified"), "visibility": p.get("visibility")}


def n_hist_context(c: dict | None) -> dict:
    """Where a piece of the history was played from: its kind, and the playlist when it was one."""
    c = c or {}
    out = {"type": c.get("type"), "playlist": None}
    full = (c.get("data") or {}).get("fullModel") or {}
    pl = full.get("playlist")
    if pl:
        out["playlist"] = n_playlist(pl) | {"trackCount": full.get("tracksCount")}
    return out


def n_clip(c: dict) -> dict:
    return {"id": str(c.get("clipId")), "title": c.get("title"), "version": c.get("version"),
            "duration": c.get("duration"), "thumb": img(c.get("thumbnail"), "400x400"),
            "preview": c.get("previewUrl"), "trackIds": [str(x) for x in (c.get("trackIds") or [])],
            "artists": [n_artist(a) for a in (c.get("artists") or [])], "explicit": c.get("explicit")}


def _tracks(items) -> list:
    """Tracks of a list whose items are tracks, or {track: ...} wrappers."""
    out = []
    for item in items or []:
        t = item.get("track") if isinstance(item, dict) and "track" in item else item
        if t:
            out.append(n_track(t))
    return out


def _playlist_doc(res: dict) -> dict:
    return {"playlist": n_playlist(res), "tracks": once_in_a_row(_tracks(res.get("tracks"))),
            "similar": [n_playlist(p) for p in (res.get("similarPlaylists") or [])],
            "wave": (res.get("customWave") or {}).get("header")}


# ---------------------------------------------------------------- who

async def whoami(acting: Acting) -> dict:
    """Who the call's token is, asked of Yandex now (nothing kept)."""
    async with _session(acting, 20) as y:
        if not y.token:
            return {"authed": False, "account": None, "premium": None}
        acc = await y.get("account/status")
    a = acc.get("account") or {}
    return {"authed": bool(a.get("uid")), "account": a.get("displayName") or a.get("login"),
            "premium": (acc.get("plus") or {}).get("hasPlus")}


# ---------------------------------------------------------------- a person's own

async def my_playlists(acting: Acting, fresh: bool) -> Answer:
    async def make():
        async with _session(acting) as y:
            uid = await y.uid()
            pls = await y.get(f"users/{uid}/playlists/list") or []
        return {"playlists": [n_playlist(p) for p in pls]}
    return await cache.personal(S, "my-playlists", "", acting, fresh, make)


async def likes(acting: Acting, fresh: bool) -> Answer:
    """The liked tracks: ids from the library, then the tracks themselves in batches."""
    async def make():
        async with _session(acting, 60) as y:
            uid = await y.uid()
            lib = (await y.get(f"users/{uid}/likes/tracks") or {}).get("library") or {}
            ids = [str(x.get("id")) for x in (lib.get("tracks") or []) if x.get("id")]
            tracks = []
            for i in range(0, len(ids), 100):
                chunk = await y.get("tracks", {"track-ids": ",".join(ids[i:i + 100])}) or []
                tracks += [n_track(t) for t in chunk if t]
        return {"total": len(ids), "tracks": once_in_a_row(tracks)}
    return await cache.personal(S, "likes", "", acting, fresh, make)


async def history(acting: Acting, fresh: bool) -> Answer:
    """The app's history: one tab per date; in each, the places the music was played from, each
    with its tracks. Yandex keeps only a little of it."""
    async def make():
        async with _session(acting, 60) as y:
            res = await y.get("music-history", {"fullModelsCount": 200}) or {}
        tabs = []
        for tab in res.get("historyTabs") or []:
            items = []
            for it in tab.get("items") or []:
                tracks = [n_track(full) for tr in (it.get("tracks") or [])
                          if (full := (tr.get("data") or {}).get("fullModel"))]
                items.append({"context": n_hist_context(it.get("context")), "tracks": once_in_a_row(tracks)})
            tabs.append({"date": tab.get("date"), "items": items})
        return {"tabs": tabs, "total": sum(len(i["tracks"]) for t in tabs for i in t["items"])}
    return await cache.personal(S, "history", "", acting, fresh, make)


# ---------------------------------------------------------------- search

async def suggest(part: str, acting: Acting, fresh: bool) -> Answer:
    async def make():
        async with _session(acting, 15) as y:
            res = await y.get("search/suggest", {"part": part}) or {}
        best = res.get("best") or {}
        return {"suggestions": [s for s in (res.get("suggestions") or []) if isinstance(s, str)],
                "best": {"type": best.get("type"), "text": best.get("text"),
                         "id": str((best.get("result") or {}).get("id") or "")} if best else None}
    return await cache.shared(S, "suggest", part, fresh, make)


async def search(text: str, acting: Acting, fresh: bool) -> Answer:
    async def make():
        async with _session(acting) as y:
            res = await y.get("search", {"text": text, "type": "all", "page": 0, "nocorrection": "false"}) or {}

        def sect(name, fn):
            s = res.get(name) or {}
            return {"total": s.get("total", 0), "items": [fn(x) for x in (s.get("results") or [])]}
        best = res.get("best") or {}
        return {"text": res.get("text"), "bestType": best.get("type"),
                "tracks": sect("tracks", n_track), "artists": sect("artists", n_artist),
                "albums": sect("albums", n_album), "playlists": sect("playlists", n_playlist),
                "podcasts": sect("podcasts", n_album), "clips": sect("clips", n_clip)}
    return await cache.shared(S, "search", text, fresh, make)


# ---------------------------------------------------------------- artist / album / track

async def artist(aid: str, acting: Acting, fresh: bool) -> Answer:
    async def make():
        async with _session(acting) as y:
            bi = await y.get(f"artists/{aid}/brief-info") or {}
        a = bi.get("artist") or {}
        return {
            "artist": {**n_artist(a), "description": (a.get("description") or {}).get("text"),
                       "countries": a.get("countries") or [], "initDate": a.get("initDate"),
                       "wiki": a.get("enWikipediaLink"),
                       "links": [{"title": l.get("title"), "href": l.get("href")} for l in (a.get("links") or [])],
                       "colors": a.get("derivedColors") or {},
                       "covers": [{"small": img(x.get("uri"), "400x400"), "big": img(x.get("uri"), "1000x1000")}
                                  for x in (bi.get("allCovers") or [])][:8]},
            "stats": bi.get("stats") or {},
            "popularTracks": [n_track(t) for t in (bi.get("popularTracks") or [])],
            "albums": [n_album(x) for x in (bi.get("albums") or [])],
            "alsoAlbums": [n_album(x) for x in (bi.get("alsoAlbums") or [])],
            "similarArtists": [n_artist(x) for x in (bi.get("similarArtists") or [])],
            "lastReleases": [n_album(x) for x in (bi.get("lastReleases") or [])],
            "videos": [{"title": v.get("title"), "embedUrl": v.get("embedUrl"), "cover": v.get("cover")}
                       for v in (bi.get("videos") or [])]}
    return await cache.shared(S, "artist", aid, fresh, make)


async def artist_albums(aid: str, page: int, sort: str, acting: Acting, fresh: bool) -> Answer:
    async def make():
        async with _session(acting) as y:
            res = await y.get(f"artists/{aid}/direct-albums", {"page": page, "page-size": 24, "sort-by": sort}) or {}
        return {"pager": res.get("pager") or {}, "albums": [n_album(x) for x in (res.get("albums") or [])]}
    return await cache.shared(S, "artist-albums", f"{aid}|{page}|{sort}", fresh, make)


async def artist_tracks(aid: str, page: int, acting: Acting, fresh: bool) -> Answer:
    async def make():
        async with _session(acting) as y:
            res = await y.get(f"artists/{aid}/tracks", {"page": page, "page-size": 50}) or {}
        return {"pager": res.get("pager") or {}, "tracks": [n_track(t) for t in (res.get("tracks") or [])]}
    return await cache.shared(S, "artist-tracks", f"{aid}|{page}", fresh, make)


async def similar_artists(aid: str, acting: Acting, fresh: bool) -> Answer:
    """Artists similar to an artist (more of them than the artist's page gives)."""
    async def make():
        async with _session(acting) as y:
            res = await y.get(f"artists/{aid}/similar") or {}
        return {"artists": [n_artist(a) for a in (res.get("similarArtists") or [])]}
    return await cache.shared(S, "similar-artists", aid, fresh, make)


async def album(alid: str, acting: Acting, fresh: bool) -> Answer:
    async def make():
        async with _session(acting) as y:
            res = await y.get(f"albums/{alid}/with-tracks") or {}
        tracks = [n_track(t) for vol in (res.get("volumes") or []) for t in vol]
        return {"album": n_album(res), "bests": [str(x) for x in (res.get("bests") or [])], "tracks": tracks}
    return await cache.shared(S, "album", alid, fresh, make)


async def track(tid: str, acting: Acting, fresh: bool) -> Answer:
    async def make():
        async with _session(acting) as y:
            res = await y.get("tracks", {"track-ids": tid}) or []
        if not res:
            raise ApiError(404, NOT_FOUND, "Yandex has no such track")
        return n_track(res[0])
    return await cache.shared(S, "track", tid, fresh, make)


async def similar(tid: str, acting: Acting, fresh: bool) -> Answer:
    async def make():
        async with _session(acting) as y:
            res = await y.get(f"tracks/{tid}/similar") or {}
        return {"tracks": [n_track(t) for t in (res.get("similarTracks") or [])]}
    return await cache.shared(S, "similar", tid, fresh, make)


async def radio(tid: str, acting: Acting, fresh: bool) -> Answer:
    """Recommendations seeded by one track (the rotor "wave"), tuned to the account that asks:
    personal when asked as a person."""
    async def make():
        async with _session(acting) as y:
            y.require_account()             # anonymously Yandex answers 400, not 401
            res = await y.get(f"rotor/station/track:{tid}/tracks", {"settings2": "true"}) or {}
        return {"tracks": [n_track(x.get("track")) for x in (res.get("sequence") or []) if x.get("track")]}
    return await cache.private(S, "radio", tid, acting, fresh, make)


# ---------------------------------------------------------------- lyrics

def _parse_lrc(body: str) -> list:
    lines = []
    for raw in body.splitlines():
        stamps = re.findall(r"\[(\d+):(\d+)(?:[.:](\d+))?\]", raw)
        text = re.sub(r"\[[^\]]*\]", "", raw).strip()
        for mm, ss, frac in stamps:
            ms = int(mm) * 60000 + int(ss) * 1000
            if frac:
                ms += int(frac.ljust(3, "0")[:3]) if len(frac) > 2 else int(frac) * 10
            lines.append({"t": ms, "text": text})
    lines.sort(key=lambda x: x["t"])
    return lines


def _parse_rich(j) -> list:
    """RICH_JSON: word-level timings when the track has them."""
    out = []

    def walk(node):
        if isinstance(node, dict):
            t = node.get("startTimeMs", node.get("timeMs", node.get("start")))
            words = node.get("words") or node.get("syllables")
            text = node.get("text") or node.get("line")
            if t is not None and (text or words):
                item = {"t": int(t), "text": text or ""}
                if isinstance(words, list):
                    item["words"] = [{"t": int(w.get("startTimeMs", w.get("start", 0))),
                                      "text": w.get("text") or w.get("word") or ""}
                                     for w in words if isinstance(w, dict)]
                    if not item["text"]:
                        item["text"] = " ".join(w["text"] for w in item["words"])
                out.append(item)
            for v in node.values():
                walk(v)
        elif isinstance(node, list):
            for v in node:
                walk(v)
    walk(j)
    out.sort(key=lambda x: x["t"])
    return out


async def lyrics(tid: str, acting: Acting, fresh: bool) -> Answer:
    """The richest form there is: RICH_JSON (by words), then LRC (by lines), then TEXT; a form the
    track lacks answers 404. Yandex gives lyrics to an account only, and answers too-frequent
    questions with 403 "Invalid Sign": a limit, not a wrong signature."""
    key = config.settings().yandex_sign_key

    async def make():
        async with _session(acting) as y:
            for fmt in ("RICH_JSON", "LRC", "TEXT"):
                ts = int(time.time())
                sign = base64.b64encode(hmac.new(key, f"{tid}{ts}".encode(), hashlib.sha256).digest()).decode()
                try:
                    meta = await y.get(f"tracks/{tid}/lyrics", {"format": fmt, "timeStamp": ts, "sign": sign})
                except ApiError as e:
                    if e.code == NOT_FOUND:
                        continue
                    raise
                if not meta or not meta.get("downloadUrl"):
                    continue
                body = await y.fetch_text(meta["downloadUrl"])
                info = {"format": fmt, "writers": meta.get("writers"),
                        "major": (meta.get("major") or {}).get("prettyName")}
                if fmt == "RICH_JSON":
                    try:
                        lines = _parse_rich(json.loads(body))
                    except ValueError:
                        lines = []
                    if lines:
                        return {**info, "available": True, "lines": lines, "synced": True,
                                "words": any("words" in l for l in lines)}
                    continue
                if fmt == "LRC":
                    lines = _parse_lrc(body)
                    if lines:
                        return {**info, "available": True, "lines": lines, "synced": True, "words": False}
                    continue
                return {**info, "available": True, "lines": [{"t": None, "text": l} for l in body.splitlines()],
                        "synced": False, "words": False}
        return {"format": None, "writers": None, "major": None, "available": False, "lines": [],
                "synced": False, "words": False}
    return await cache.shared(S, "lyrics", tid, fresh, make)


# ---------------------------------------------------------------- playlists

def _playlist_path(pid: str) -> str:
    """A playlist's id here: "uid:kind" (a user's playlist) or its uuid (a shared link)."""
    if ":" in pid:
        uid, kind = pid.split(":", 1)
        return f"users/{uid}/playlists/{kind}"
    return f"playlist/{pid}"


async def playlist(pid: str, acting: Acting, fresh: bool) -> Answer:
    """A playlist that is not open to the caller is not told from one that is not there: Yandex refuses both (400, 404,
    or an account only), and both are not found."""
    async def make():
        async with _session(acting) as y:
            try:
                res = await y.get(_playlist_path(pid), {"rich-tracks": "true", "withSimilarPlaylists": "true"}) or {}
            except ApiError as e:
                if e.status in (400, 404) or e.code == ACCOUNT_REQUIRED:
                    raise ApiError(404, NOT_FOUND, "the playlist did not open: there is no such, or it is private") from None
                raise
        return _playlist_doc(res)
    return await cache.private(S, "playlist", pid, acting, fresh, make)


async def playlist_recommendations(pid: str, acting: Acting, fresh: bool) -> Answer:
    if ":" not in pid:
        pid = (await playlist(pid, acting, False)).body["playlist"]["id"]
    uid, kind = pid.split(":", 1)

    async def make():
        async with _session(acting) as y:
            res = await y.get(f"users/{uid}/playlists/{kind}/recommendations") or {}
        return {"tracks": [n_track(t) for t in (res.get("tracks") or [])]}
    return await cache.private(S, "playlist-recommendations", pid, acting, fresh, make)


async def recommend_for(tracks: list[tuple[str, str | None]], acting: Acting, fresh: bool) -> Answer:
    """Recommendations for any set of tracks, given as (trackId, albumId or None). Yandex
    recommends only for a playlist, so a temporary private one is made in the service account,
    filled, read and deleted again in `finally`. A track without its album id gets it from the
    track's own page."""
    if acting.mode != "service":
        raise ApiError(400, SERVICE_REQUIRED, "recommendations for a set of tracks are made by the service account")
    if not config.settings().yandex_token:
        raise ApiError(503, SERVICE_ACCOUNT_MISSING, "no Yandex service account is configured")
    want = []
    for tid, alb in tracks:
        if not alb:
            alb = ((await track(tid, acting, False)).body.get("album") or {}).get("id")
        if not alb:
            raise ApiError(400, BAD_REQUEST, f"track {tid} has no album")
        want.append({"id": tid, "albumId": alb})

    async def make():
        async with _session(acting, 60) as y:
            uid = await y.uid()
            pl = await y.post(f"users/{uid}/playlists/create",
                              params={"title": f"tmp-reco-{int(time.time())}", "visibility": "private"}) or {}
            kind = pl.get("kind")
            if not kind:
                raise ApiError(502, UPSTREAM_FAILED, "Yandex did not make the temporary playlist")
            try:
                diff = json.dumps([{"op": "insert", "at": 0, "tracks": want}])
                await y.post(f"users/{uid}/playlists/{kind}/change-relative",
                             params={"revision": pl.get("revision", 1)}, data={"diff": diff})
                res = await y.get(f"users/{uid}/playlists/{kind}/recommendations") or {}
            finally:
                await y.post(f"users/{uid}/playlists/{kind}/delete")
        return {"tracks": [n_track(t) for t in (res.get("tracks") or [])]}
    key = ",".join(sorted(f"{t['id']}:{t['albumId']}" for t in want))
    return await cache.shared(S, "recommend-for", key, fresh, make)


# ---------------------------------------------------------------- catalog

async def chart(acting: Acting, fresh: bool) -> Answer:
    async def make():
        async with _session(acting) as y:
            res = await y.get("landing3/chart") or {}
            ch = res.get("chart") or {}
            tracks = _tracks(ch.get("tracks"))
            if not tracks and ch.get("uid") and ch.get("kind"):
                pl = await y.get(f"users/{ch['uid']}/playlists/{ch['kind']}", {"rich-tracks": "true"}) or {}
                tracks = _tracks(pl.get("tracks"))
        return {"title": res.get("title") or ch.get("title"), "tracks": tracks}
    return await cache.shared(S, "chart", "", fresh, make)


async def categories(acting: Acting, fresh: bool) -> Answer:
    """The genres, with their sub-genres; a genre's id opens its page (category)."""
    async def make():
        async with _session(acting) as y:
            res = await y.get("genres") or []
        out = []
        for g in res:
            t = g.get("titles") or {}
            imgs = g.get("images") or {}
            out.append({"id": g.get("urlPart") or g.get("id"),
                        "title": (t.get("ru") or t.get("en") or {}).get("title") or g.get("id"),
                        "image": imgs.get("300x300") or imgs.get("208x208"), "color": g.get("color"),
                        "subGenres": [{"id": s.get("urlPart") or s.get("id"),
                                       "title": ((s.get("titles") or {}).get("ru") or {}).get("title") or s.get("id")}
                                      for s in (g.get("subGenres") or [])]})
        return {"genres": out}
    return await cache.shared(S, "categories", "", fresh, make)


async def category(tag: str, offset: int, limit: int, acting: Acting, fresh: bool) -> Answer:
    """Albums of a genre / meta-tag, page by page (a catalogue, not a search)."""
    async def make():
        async with _session(acting) as y:
            res = await y.get(f"metatags/{tag}/albums", {"offset": offset, "limit": limit}) or {}
        return {"title": (res.get("title") or {}).get("title"), "pager": res.get("pager") or {},
                "albums": [n_album(a) for a in (res.get("albums") or [])]}
    return await cache.shared(S, "category", f"{tag}|{offset}|{limit}", fresh, make)


# ---------------------------------------------------------------- sound

async def audio_source(tid: str, acting: Acting) -> tuple[str, dict]:
    """Where the track's sound is now (a URL that lives minutes), and what it is: the best
    whole file this call's token may have; anonymously Yandex gives only the preview."""
    salt = config.settings().yandex_mp3_salt
    async with _session(acting) as y:
        infos = await y.get(f"tracks/{tid}/download-info") or []
        if not infos:
            raise ApiError(404, NO_FILE, "Yandex gives no file for this track")
        pool = [i for i in infos if i.get("codec") == "mp3" and not i.get("preview")] \
            or [i for i in infos if not i.get("preview")] or infos
        best = sorted(pool, key=lambda i: i.get("bitrateInKbps", 0))[-1]
        xml = await y.fetch_text(best["downloadInfoUrl"], y.headers())
    try:
        root = ET.fromstring(xml)
    except ET.ParseError:
        raise ApiError(502, UPSTREAM_FAILED, "Yandex gave a file description that does not parse") from None
    host, path, ts, s = (root.findtext(k) for k in ("host", "path", "ts", "s"))
    sign = hashlib.md5((salt + path[1:] + s).encode()).hexdigest()
    return (f"https://{host}/get-mp3/{sign}/{ts}{path}",
            {"codec": best.get("codec"), "bitrate": best.get("bitrateInKbps"),
             "preview": bool(best.get("preview"))})


async def offers_full(tid: str, acting: Acting) -> bool:
    """Whether this call's token may have the whole file (not only the preview)."""
    async with _session(acting) as y:
        infos = await y.get(f"tracks/{tid}/download-info") or []
    return any(not i.get("preview") for i in infos)


async def download(url: str) -> AsyncIterator[bytes]:
    async with httpx.AsyncClient(timeout=120, follow_redirects=True) as c:
        async with c.stream("GET", url) as r:
            if r.status_code >= 400:
                raise ApiError(502, UPSTREAM_FAILED, f"the file did not download: {r.status_code}")
            async for piece in r.aiter_bytes():
                yield piece
