"""YouTube Music: metadata from ytmusicapi (the internal youtubei API), sound and video by yt-dlp.

An account (browser request headers with the cookies) matters for the account's own things only:
its library, liked songs, history and private playlists. Everything else is asked anonymously in
every mode: it is the same, and under an account YouTube refuses the watch playlist (lyrics,
radio, related) with 400. The service account is optional; a person's account comes in the call.
Sound and video are fetched anonymously; only when YouTube refuses that, and the call is in
service mode, with the service account's cookies.
Read-only: nothing is liked, made or changed on any account.

YouTube tells "not to you" (stale cookies, a private or missing playlist) by a page without the
expected content, which ytmusicapi raises as KeyError; every other failure is the service's.
"""
from __future__ import annotations

import json
import os
import tempfile
import time
from functools import lru_cache
from typing import AsyncIterator

import anyio
import yt_dlp
from ytmusicapi import YTMusic
from ytmusicapi.auth.browser import setup_browser
from ytmusicapi.exceptions import YTMusicError

import cache
import config
from acting import Acting
from cache import Answer
from common import once_in_a_row
from errors import (BAD_REQUEST, CREDENTIALS_INVALID, CREDENTIALS_REJECTED, NOT_FOUND, SERVICE_ACCOUNT_REJECTED,
                    UPSTREAM_FAILED, ApiError)

S = "youtube"

_anon = YTMusic()


@lru_cache(maxsize=None)
def _anon_in(lang: str) -> YTMusic:
    """An anonymous client that has YouTube name things (moods, genres, shelves) in the language given."""
    return YTMusic(language=lang)


@lru_cache(maxsize=1)
def _service_client() -> YTMusic | None:
    auth = config.settings().youtube_auth
    return YTMusic(auth=auth) if auth else None


_NOT_HEADERS = "the YouTube credentials are not browser request headers of a signed-in YouTube Music page"


def _client_of(acting: Acting) -> YTMusic | None:
    """The call's account; None when it acts without one."""
    if acting.mode == "person":
        try:
            return YTMusic(auth=json.loads(setup_browser(headers_raw=acting.credentials.replace("\r", ""))))
        except YTMusicError:
            raise ApiError(400, CREDENTIALS_INVALID, _NOT_HEADERS) from None
    if acting.mode == "service":
        return _service_client()
    return None


def _rejected(acting: Acting) -> ApiError:
    if acting.mode == "person":
        return ApiError(403, CREDENTIALS_REJECTED, "YouTube did not accept the person's cookies")
    return ApiError(503, SERVICE_ACCOUNT_REJECTED, "YouTube did not accept the service account's cookies")


async def _run(fn, *args, refused: ApiError | None = None, **kwargs):
    """ytmusicapi and yt-dlp are blocking: they run in a thread. YouTube's "not to you" becomes
    `refused` when given; everything else that fails is upstream_failed."""
    try:
        return await anyio.to_thread.run_sync(lambda: fn(*args, **kwargs))
    except ApiError:
        raise
    except KeyError as e:
        if refused:
            raise refused from None
        raise ApiError(502, UPSTREAM_FAILED, f"YouTube: an answer of an unknown shape: {str(e)[:200]}") from None
    except Exception as e:
        raise ApiError(502, UPSTREAM_FAILED, f"YouTube: {type(e).__name__}: {str(e)[:200]}") from None


async def _account_name(yt: YTMusic) -> str | None:
    """The account's name, when YouTube really takes its cookies (they can parse while stale, and
    YouTube answers as to a stranger); None when it does not."""
    stranger = ApiError(403, CREDENTIALS_REJECTED, "")
    try:
        info = await _run(yt.get_account_info, refused=stranger)
    except ApiError as e:
        if e is stranger:
            return None
        raise
    return (info or {}).get("accountName")


async def _person(acting: Acting) -> YTMusic:
    """The person's client, checked to be accepted by YouTube."""
    yt = _client_of(acting)
    if not await _account_name(yt):
        raise _rejected(acting)
    return yt


# ---------------------------------------------------------------- shapers

def thumb(items, big=False):
    ts = sorted(items or [], key=lambda t: (t.get("width") or 0))
    if not ts:
        return None
    return (ts[-1] if big else ts[min(1, len(ts) - 1)]).get("url")


def n_artist(a: dict) -> dict:
    return {"id": a.get("id") or a.get("channelId") or a.get("browseId"),
            "name": a.get("name") or a.get("artist") or a.get("title"), "cover": thumb(a.get("thumbnails"))}


def n_album(a: dict) -> dict:
    return {"id": a.get("browseId") or a.get("id"), "playlistId": a.get("audioPlaylistId") or a.get("playlistId"),
            "title": a.get("title") or a.get("name"), "year": a.get("year"), "type": a.get("type"),
            "trackCount": a.get("trackCount"), "cover": thumb(a.get("thumbnails")),
            "artists": [n_artist(x) for x in (a.get("artists") or []) if isinstance(x, dict)],
            "explicit": a.get("isExplicit")}


def n_track(t: dict) -> dict:
    alb = t.get("album")
    if isinstance(alb, str):
        alb = {"title": alb}
    return {"id": t.get("videoId") or t.get("id"), "title": t.get("title"),
            "duration": t.get("duration") or t.get("length"),
            "durationMs": (t.get("duration_seconds") or 0) * 1000 or None,
            "cover": thumb(t.get("thumbnails") or t.get("thumbnail")),
            "artists": [n_artist(x) for x in (t.get("artists") or []) if isinstance(x, dict)],
            "album": n_album(alb) if isinstance(alb, dict) and (alb.get("title") or alb.get("name")) else None,
            "explicit": t.get("isExplicit"), "views": t.get("views"), "year": t.get("year"),
            # ATV = an "art track" (sound under a still cover), OMV/UGC = a real video
            "videoType": t.get("videoType"),
            "isVideo": (t.get("videoType") or "").endswith(("OMV", "UGC")) or None,
            "available": t.get("isAvailable", True)}


def n_playlist(p: dict) -> dict:
    author = p.get("author")
    return {"id": p.get("playlistId") or p.get("browseId") or p.get("id"), "title": p.get("title"),
            "cover": thumb(p.get("thumbnails")),
            "trackCount": p.get("count") or p.get("trackCount") or p.get("itemCount"),
            "author": author.get("name") if isinstance(author, dict) else (author if isinstance(author, str) else None),
            "description": p.get("description")}


# ---------------------------------------------------------------- who

async def whoami(acting: Acting) -> dict:
    """Who the call's account is, asked of YouTube now (nothing kept)."""
    yt = _client_of(acting)
    name = await _account_name(yt) if yt else None
    return {"authed": bool(name), "account": name, "premium": None}


# ---------------------------------------------------------------- a person's own

async def liked(acting: Acting, fresh: bool) -> Answer:
    async def make():
        p = await _run((await _person(acting)).get_liked_songs, limit=500)
        tracks = once_in_a_row([n_track(t) for t in (p.get("tracks") or [])])
        return {"total": len(tracks), "tracks": tracks}
    return await cache.personal(S, "likes", "", acting, fresh, make)


async def my_playlists(acting: Acting, fresh: bool) -> Answer:
    async def make():
        res = await _run((await _person(acting)).get_library_playlists, limit=200)
        return {"playlists": [n_playlist(p) for p in (res or [])]}
    return await cache.personal(S, "my-playlists", "", acting, fresh, make)


async def library_songs(acting: Acting, fresh: bool) -> Answer:
    async def make():
        res = await _run((await _person(acting)).get_library_songs, limit=500)
        return {"tracks": once_in_a_row([n_track(t) for t in (res or [])])}
    return await cache.personal(S, "library-songs", "", acting, fresh, make)


async def history(acting: Acting, fresh: bool) -> Answer:
    async def make():
        res = await _run((await _person(acting)).get_history)
        return {"tracks": once_in_a_row([n_track(t) for t in (res or [])])}
    return await cache.personal(S, "history", "", acting, fresh, make)


# ---------------------------------------------------------------- search

async def suggest(part: str, fresh: bool) -> Answer:
    async def make():
        res = await _run(_anon.get_search_suggestions, part)
        return {"suggestions": [s for s in res if isinstance(s, str)]}
    return await cache.shared(S, "suggest", part, fresh, make)


async def search(text: str, fresh: bool) -> Answer:
    async def make():
        out = {"text": text}
        for key, filt, fn in (("tracks", "songs", n_track), ("videos", "videos", n_track),
                              ("artists", "artists", n_artist), ("albums", "albums", n_album),
                              ("playlists", "playlists", n_playlist)):
            res = await _run(_anon.search, text, filter=filt, limit=20) or []
            out[key] = {"items": [fn(x) for x in res], "total": len(res)}
        return out
    return await cache.shared(S, "search", text, fresh, make)


# ---------------------------------------------------------------- artist / album / playlist

async def artist(cid: str, fresh: bool) -> Answer:
    async def make():
        a = await _run(_anon.get_artist, cid)

        def sect(name, fn):
            s = a.get(name) or {}
            res = s.get("results") if isinstance(s, dict) else s
            return [fn(x) for x in (res or []) if isinstance(x, dict)]
        return {"artist": {"id": cid, "name": a.get("name"), "cover": thumb(a.get("thumbnails"), big=True),
                           "description": a.get("description"), "views": a.get("views"),
                           "subscribers": a.get("subscribers"), "monthlyListeners": a.get("monthlyListeners"),
                           "radioId": a.get("radioId"), "shuffleId": a.get("shuffleId")},
                "songs": sect("songs", n_track), "albums": sect("albums", n_album),
                "singles": sect("singles", n_album), "videos": sect("videos", n_track),
                "related": sect("related", n_artist)}
    return await cache.shared(S, "artist", cid, fresh, make)


async def artist_albums(cid: str, fresh: bool) -> Answer:
    """The artist's whole discography: the artist's page names where it is (a browse id and
    params of its own), the albums are read from there."""
    async def make():
        sect = (await _run(_anon.get_artist, cid)).get("albums") or {}
        if not sect.get("browseId"):
            # an artist whose page shows all the albums there is has no separate discography
            return {"albums": [n_album(x) for x in (sect.get("results") or []) if isinstance(x, dict)]}
        res = await _run(_anon.get_artist_albums, sect["browseId"], sect.get("params"), limit=200)
        return {"albums": [n_album(x) for x in (res or [])]}
    return await cache.shared(S, "artist-albums", cid, fresh, make)


async def album(bid: str, fresh: bool) -> Answer:
    async def make():
        a = await _run(_anon.get_album, bid)
        alb = n_album({**a, "browseId": bid}) | {"description": a.get("description"), "duration": a.get("duration")}
        return {"album": alb, "tracks": [n_track(t) for t in (a.get("tracks") or [])]}
    return await cache.shared(S, "album", bid, fresh, make)


async def playlist(pid: str, acting: Acting, fresh: bool) -> Answer:
    """A private playlist is seen by its owner's account only: asked as a person, it is asked
    with the person's account (and kept for that person); otherwise anonymously."""
    async def make():
        yt = await _person(acting) if acting.mode == "person" else _anon
        p = await _run(yt.get_playlist, pid, limit=500, refused=ApiError(
            404, NOT_FOUND, "the playlist did not open: there is no such, or it is private"))
        return {"playlist": n_playlist({**p, "playlistId": pid}) | {
                    "duration": p.get("duration"), "views": p.get("views"),
                    "privacy": p.get("privacy"), "owned": p.get("owned")},
                "tracks": once_in_a_row([n_track(t) for t in (p.get("tracks") or [])])}
    return await cache.private(S, "playlist", pid, acting, fresh, make)


# ---------------------------------------------------------------- track, radio, related, lyrics

async def track(vid: str, fresh: bool) -> Answer:
    async def make():
        s = await _run(_anon.get_song, vid) or {}
        vd = s.get("videoDetails") or {}
        if not vd:
            raise ApiError(404, NOT_FOUND, "YouTube has no such track")
        mf = ((s.get("microformat") or {}).get("microformatDataRenderer")) or {}
        out = {"id": vid, "title": vd.get("title"),
               "artists": [{"id": vd.get("channelId"), "name": vd.get("author"), "cover": None}],
               "durationMs": int(vd.get("lengthSeconds") or 0) * 1000 or None, "views": vd.get("viewCount"),
               "cover": thumb((vd.get("thumbnail") or {}).get("thumbnails"), big=True),
               "description": mf.get("description"), "uploadDate": mf.get("uploadDate") or mf.get("publishDate"),
               "category": mf.get("category"), "unlisted": mf.get("unlisted"),
               "family": vd.get("isFamilySafe"), "live": vd.get("isLiveContent")}
        wp = await _run(_anon.get_watch_playlist, videoId=vid, limit=1)
        first = n_track((wp.get("tracks") or [{}])[0])
        out["album"] = first.get("album")
        if first.get("artists"):
            out["artists"] = first["artists"]
        out["videoType"] = first.get("videoType")
        out["isVideo"] = first.get("isVideo")
        out["lyricsAvailable"] = bool(wp.get("lyrics"))
        return out
    return await cache.shared(S, "track", vid, fresh, make)


async def radio(vid: str, fresh: bool) -> Answer:
    """Recommendations seeded by one track (YouTube Music's radio)."""
    async def make():
        wp = await _run(_anon.get_watch_playlist, videoId=vid, limit=50, radio=True)
        return {"tracks": [n_track(t) for t in (wp.get("tracks") or [])]}
    return await cache.shared(S, "radio", vid, fresh, make)


async def similar(vid: str, fresh: bool) -> Answer:
    """What YouTube relates to a track: tracks, albums and artists, by sections."""
    async def make():
        wp = await _run(_anon.get_watch_playlist, videoId=vid, limit=1)
        bid = wp.get("related")
        rel = (await _run(_anon.get_song_related, bid) or []) if bid else []
        out = []
        for sec in rel:
            if not isinstance(sec, dict):
                continue
            items = [x for x in (sec.get("contents") or []) if isinstance(x, dict)]
            tracks = [n_track(x) for x in items if x.get("videoId")]
            albums = [n_album(x) for x in items if x.get("browseId") and not x.get("videoId") and x.get("title")]
            artists = [n_artist(x) for x in items if (x.get("browseId") or x.get("channelId")) and x.get("subscribers")]
            if tracks or albums or artists:
                out.append({"title": sec.get("title"), "tracks": tracks, "albums": albums, "artists": artists})
        return {"sections": out}
    return await cache.shared(S, "similar", vid, fresh, make)


async def lyrics(vid: str, fresh: bool) -> Answer:
    """Timed by lines where YouTube has it (ytmusicapi asks as the mobile app for these: the web
    client gets the text without times), else the plain text."""
    async def make():
        none = {"available": False, "lines": [], "synced": False, "words": False, "source": None}
        wp = await _run(_anon.get_watch_playlist, videoId=vid, limit=1)
        bid = wp.get("lyrics")
        if not bid:
            return none
        ly = await _run(_anon.get_lyrics, bid, timestamps=True) or {}
        body, src = ly.get("lyrics"), ly.get("source")
        if isinstance(body, list):
            lines = [{"t": int(getattr(l, "start_time", 0) or 0), "end": getattr(l, "end_time", None),
                      "text": getattr(l, "text", "")} for l in body]
            if lines:
                return {"available": True, "lines": lines, "synced": True, "words": False, "source": src}
        if isinstance(body, str) and body.strip():
            return {"available": True, "lines": [{"t": None, "text": l} for l in body.splitlines()],
                    "synced": False, "words": False, "source": src}
        return none
    return await cache.shared(S, "lyrics", vid, fresh, make)


# ---------------------------------------------------------------- catalog

async def chart(country: str, fresh: bool) -> Answer:
    async def make():
        c = await _run(_anon.get_charts, country)

        def pick(key, fn):
            s = c.get(key) or {}
            items = s.get("items") if isinstance(s, dict) else s
            return [fn(x) for x in (items or []) if isinstance(x, dict)]
        return {"countries": [x for x in ((c.get("countries") or {}).get("options") or []) if isinstance(x, str)][:60],
                "songs": pick("songs", n_track), "videos": pick("videos", n_track),
                "artists": pick("artists", n_artist), "trending": pick("trending", n_track)}
    return await cache.shared(S, "chart", country, fresh, make)


def _sections(raw: dict) -> list:
    tabs = (((raw.get("contents") or {}).get("singleColumnBrowseResultsRenderer") or {}).get("tabs") or [])
    return ((((tabs[0] or {}).get("tabRenderer") or {}).get("content") or {})
            .get("sectionListRenderer") or {}).get("contents") or [] if tabs else []


def _moods_raw(lang: str) -> list:
    """Moods and genres from the raw browse: ytmusicapi drops each tile's colour (YouTube ships no
    pictures for them), so the grid is read here."""
    raw = _anon_in(lang)._send_request("browse", {"browseId": "FEmusic_moods_and_genres"})
    out = []
    for s in _sections(raw):
        grid = s.get("gridRenderer") or {}
        title = (((grid.get("header") or {}).get("gridHeaderRenderer") or {}).get("title") or {}).get("runs") or []
        items = []
        for it in grid.get("items") or []:
            b = it.get("musicNavigationButtonRenderer") or {}
            runs = (b.get("buttonText") or {}).get("runs") or []
            ep = ((b.get("clickCommand") or {}).get("browseEndpoint") or {})
            colour = (b.get("solid") or {}).get("leftStripeColor")
            if runs and ep.get("params"):
                items.append({"id": ep.get("params"), "title": runs[0].get("text"),
                              "color": ("#%06x" % (colour & 0xFFFFFF)) if isinstance(colour, int) else None})
        if items:
            out.append({"title": (title[0].get("text") if title else "") or "", "items": items})
    return out


async def categories(fresh: bool, lang: str) -> Answer:
    """Moods and genres, by sections, named in the language asked; an item's id opens its page (category)."""
    async def make():
        return {"sections": await _run(_moods_raw, lang)}
    return await cache.shared(S, "categories", lang, fresh, make)


def _two_row(it: dict):
    """One tile of a mood page: a playlist, an album, an artist or a song."""
    r = it.get("musicTwoRowItemRenderer") or {}
    runs = (r.get("title") or {}).get("runs") or []
    title = runs[0].get("text") if runs else None
    if not title:
        return None, None
    sub = " · ".join(x.get("text") for x in ((r.get("subtitle") or {}).get("runs") or [])
                     if x.get("text") and x.get("text").strip() != "•")
    thumbs = ((((r.get("thumbnailRenderer") or {}).get("musicThumbnailRenderer") or {})
               .get("thumbnail") or {}).get("thumbnails")) or []
    nav = r.get("navigationEndpoint") or {}
    be, we = nav.get("browseEndpoint") or {}, nav.get("watchEndpoint") or {}
    page = (((be.get("browseEndpointContextSupportedConfigs") or {})
             .get("browseEndpointContextMusicConfig") or {}).get("pageType") or "")
    by = [{"id": None, "name": sub, "cover": None}] if sub else []
    if we.get("videoId"):
        return "tracks", n_track({"videoId": we["videoId"], "title": title, "thumbnails": thumbs}) | {"artists": by}
    if "PLAYLIST" in page and be.get("browseId"):
        return "playlists", n_playlist({"playlistId": be["browseId"], "title": title, "thumbnails": thumbs,
                                        "author": sub or None})
    if "ALBUM" in page and be.get("browseId"):
        return "albums", n_album({"browseId": be["browseId"], "title": title, "thumbnails": thumbs}) | {"artists": by}
    if "ARTIST" in page and be.get("browseId"):
        return "artists", n_artist({"browseId": be["browseId"], "name": title, "thumbnails": thumbs})
    return None, None


def _mood_raw(params: str, lang: str) -> list:
    """A mood page from the raw browse: ytmusicapi trips over shelves whose tiles are songs."""
    raw = _anon_in(lang)._send_request("browse", {"browseId": "FEmusic_moods_and_genres_category", "params": params})
    out = []
    for s in _sections(raw):
        shelf, grid = s.get("musicCarouselShelfRenderer") or {}, s.get("gridRenderer") or {}
        head = ((shelf.get("header") or {}).get("musicCarouselShelfBasicHeaderRenderer")
                or (grid.get("header") or {}).get("gridHeaderRenderer") or {})
        hruns = (head.get("title") or {}).get("runs") or []
        bucket = {"title": (hruns[0].get("text") if hruns else "") or "",
                  "playlists": [], "albums": [], "tracks": [], "artists": []}
        for it in shelf.get("contents") or grid.get("items") or []:
            kind, obj = _two_row(it)
            if kind:
                bucket[kind].append(obj)
        if any(bucket[k] for k in ("playlists", "albums", "tracks", "artists")):
            out.append(bucket)
    return out


async def category(params: str, fresh: bool, lang: str) -> Answer:
    async def make():
        return {"sections": await _run(_mood_raw, params, lang)}
    return await cache.shared(S, "category", f"{params}|{lang}", fresh, make)


# ---------------------------------------------------------------- sound and video

AUDIO_FORMAT = "bestaudio[ext=m4a]/bestaudio"
# H.264 first: AV1 and VP9 do not play everywhere (Safari, older devices)
VIDEO_FORMAT = ("bv*[height<=720][vcodec^=avc1]+ba[ext=m4a]/bv*[height<=720][ext=mp4]+ba[ext=m4a]"
                "/b[height<=720]/bv*+ba/b")
MIME = {"m4a": "audio/mp4", "webm": "audio/webm", "mp4": "video/mp4", "mp3": "audio/mpeg", "opus": "audio/ogg"}


def _cookie_file(headers: dict, folder: str) -> str:
    """The service account's cookies as a Netscape cookie file for yt-dlp, in the call's temporary folder."""
    cookie = next((v for k, v in headers.items() if k.lower() == "cookie"), "")
    expires = int(time.time()) + 3600
    lines = ["# Netscape HTTP Cookie File"]
    for part in cookie.split(";"):
        name, sep, value = part.strip().partition("=")
        if sep and name:
            lines.append(f".youtube.com\tTRUE\t/\tTRUE\t{expires}\t{name}\t{value}")
    path = os.path.join(folder, "cookies.txt")
    with open(path, "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")
    return path


def _download(vid: str, video: bool, folder: str, cookies: str | None) -> dict:
    opts = {"quiet": True, "no_warnings": True, "noplaylist": True, "outtmpl": f"{folder}/%(id)s.%(ext)s",
            "format": VIDEO_FORMAT if video else AUDIO_FORMAT}
    if video:
        opts["merge_output_format"] = "mp4"
    if cookies:
        opts["cookiefile"] = cookies
    # googlevideo now and then answers a fresh address with 403; asked again, with addresses got
    # anew, it gives the file
    for attempt in range(3):
        try:
            with yt_dlp.YoutubeDL(opts) as y:
                return y.extract_info(f"https://music.youtube.com/watch?v={vid}", download=True)
        except yt_dlp.utils.DownloadError:
            if attempt == 2:
                raise


def _fetch_file(vid: str, video: bool, folder: str, service: dict | None) -> tuple[str, dict]:
    """Anonymously; when YouTube refuses that and the call is in service mode with a service
    account configured, with the service account's cookies."""
    try:
        info = _download(vid, video, folder, None)
    except yt_dlp.utils.DownloadError:
        if not service:
            raise
        info = _download(vid, video, folder, _cookie_file(service, folder))
    path = ((info.get("requested_downloads") or [{}])[0]).get("filepath")
    if not path or not os.path.exists(path):
        raise ApiError(502, UPSTREAM_FAILED, "yt-dlp downloaded nothing")
    ext = path.rsplit(".", 1)[-1]
    return path, {"ext": ext, "mime": MIME.get(ext, "application/octet-stream"),
                  "codec": info.get("acodec"), "vcodec": info.get("vcodec") if video else None,
                  "abr": info.get("abr"), "height": info.get("height") if video else None}


async def fetch_media(vid: str, video: bool, acting: Acting) -> tuple[str, dict, AsyncIterator[bytes]]:
    """Downloads the track's sound (or its video, up to 720p, sound and picture in one mp4) and
    gives it as pieces, to be kept. Answers (mime, info, pieces)."""
    if not vid or vid in ("null", "undefined"):
        raise ApiError(400, BAD_REQUEST, "no videoId")
    service = config.settings().youtube_auth if acting.mode == "service" else None
    folder = tempfile.mkdtemp(prefix="yt-")

    def clean():
        for name in os.listdir(folder):
            os.unlink(os.path.join(folder, name))
        os.rmdir(folder)
    try:
        path, info = await _run(_fetch_file, vid, video, folder, service)
    except BaseException:
        clean()
        raise

    async def pieces():
        try:
            with open(path, "rb") as f:
                while piece := f.read(1 << 20):
                    yield piece
        finally:
            clean()
    return info["mime"], info, pieces()
