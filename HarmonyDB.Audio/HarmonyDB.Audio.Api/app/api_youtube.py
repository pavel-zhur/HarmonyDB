"""YouTube Music over HTTP: /internal/youtube/...

Every call says its mode, as on Yandex. General navigation is asked of YouTube anonymously in every
mode (it is the same, and YouTube refuses some of it under an account); the mode decides the
person's own lists, playlists (a person's private ones), and the account used when YouTube refuses
to give a file anonymously.
"""
from __future__ import annotations

from fastapi import APIRouter, Depends, Query, Request

import media
import youtube as T
from acting import Acting, acting, media_acting
from auth import MEDIA, READ, require
from models import (Answer, FileInfo, TAlbumPage, TAlbums, TArtistPage, TCategories, TCategory, TChart, TLikes,
                    TLyrics, TPlaylistPage, TPlaylists, TSearch, TSimilar, TSuggest, TTrackPage, TTracks, Who)
from shared import SERVICE_PROBLEMS, Fresh, envelope, file_info, file_route, problems

nav = APIRouter(prefix="/internal/youtube", tags=["youtube"],
                dependencies=[Depends(require(READ)), Depends(acting)], responses=SERVICE_PROBLEMS)
files = APIRouter(prefix="/internal/youtube", tags=["youtube files"],
                  dependencies=[Depends(require(MEDIA))], responses=problems(400, 401, 403, 416, 502))


@nav.get("/me", response_model=Who, summary="Who the call's account is (asked now, not kept)")
async def me(a: Acting = Depends(acting)):
    return await T.whoami(a)


@nav.get("/search", response_model=Answer[TSearch])
async def search(text: str = Query(min_length=1, max_length=300), fresh: Fresh = False):
    return envelope(await T.search(text, fresh))


@nav.get("/suggest", response_model=Answer[TSuggest])
async def suggest(part: str = Query(min_length=1, max_length=100), fresh: Fresh = False):
    return envelope(await T.suggest(part, fresh))


@nav.get("/artists/{id}", response_model=Answer[TArtistPage], summary="An artist by channel id (UC...)")
async def artist(id: str, fresh: Fresh = False):
    return envelope(await T.artist(id, fresh))


@nav.get("/artists/{id}/albums", response_model=Answer[TAlbums],
         summary="The artist's whole discography")
async def artist_albums(id: str, fresh: Fresh = False):
    return envelope(await T.artist_albums(id, fresh))


@nav.get("/albums/{id}", response_model=Answer[TAlbumPage], summary="An album by browse id (MPRE...)")
async def album(id: str, fresh: Fresh = False):
    return envelope(await T.album(id, fresh))


@nav.get("/tracks/{id}", response_model=Answer[TTrackPage], summary="A track by videoId")
async def track(id: str, fresh: Fresh = False):
    return envelope(await T.track(id, fresh))


@nav.get("/tracks/{id}/similar", response_model=Answer[TSimilar],
         summary="What YouTube relates to the track: tracks, albums, artists by sections")
async def similar(id: str, fresh: Fresh = False):
    return envelope(await T.similar(id, fresh))


@nav.get("/tracks/{id}/radio", response_model=Answer[TTracks], summary="A radio seeded by the track")
async def radio(id: str, fresh: Fresh = False):
    return envelope(await T.radio(id, fresh))


@nav.get("/tracks/{id}/lyrics", response_model=Answer[TLyrics])
async def lyrics(id: str, fresh: Fresh = False):
    return envelope(await T.lyrics(id, fresh))


@nav.get("/playlists/{id}", response_model=Answer[TPlaylistPage],
         summary="A playlist by id (PL..., RD..., OLAK...); personal when asked as a person")
async def playlist(id: str, fresh: Fresh = False, a: Acting = Depends(acting)):
    return envelope(await T.playlist(id, a, fresh))


@nav.get("/chart", response_model=Answer[TChart], summary="The chart of a country (ZZ: the world)")
async def chart(country: str = Query("ZZ", min_length=2, max_length=2), fresh: Fresh = False):
    return envelope(await T.chart(country.upper(), fresh))


@nav.get("/categories", response_model=Answer[TCategories], summary="Moods and genres, by sections")
async def categories(fresh: Fresh = False):
    return envelope(await T.categories(fresh))


@nav.get("/categories/{id}", response_model=Answer[TCategory],
         summary="A mood or genre: its playlists, albums, artists and songs by sections")
async def category(id: str, fresh: Fresh = False):
    return envelope(await T.category(id, fresh))


@nav.get("/me/playlists", response_model=Answer[TPlaylists], summary="The person's playlists (person mode)")
async def my_playlists(fresh: Fresh = False, a: Acting = Depends(acting)):
    return envelope(await T.my_playlists(a, fresh))


@nav.get("/me/likes", response_model=Answer[TLikes], summary="The person's liked songs (person mode)")
async def my_likes(fresh: Fresh = False, a: Acting = Depends(acting)):
    return envelope(await T.liked(a, fresh))


@nav.get("/me/songs", response_model=Answer[TTracks], summary="The songs in the person's library (person mode)")
async def my_songs(fresh: Fresh = False, a: Acting = Depends(acting)):
    return envelope(await T.library_songs(a, fresh))


@nav.get("/me/history", response_model=Answer[TTracks], summary="The person's history (person mode)")
async def my_history(fresh: Fresh = False, a: Acting = Depends(acting)):
    return envelope(await T.history(a, fresh))


# ---------------------------------------------------------------- files

async def _info(kind: str, id: str, fetch: bool, fresh: bool, a: Acting) -> dict:
    row = await media.track_file("youtube", kind, id, a, fresh) if fetch \
        else await media.kept_track_file("youtube", kind, id)
    return file_info(row)


@files.get("/tracks/{id}/audio/info", response_model=FileInfo,
           summary="The track's sound: fetched and kept first (unless fetch=false), then described")
async def audio_info(id: str, fetch: bool = True, fresh: Fresh = False, a: Acting = Depends(media_acting)):
    return await _info("audio", id, fetch, fresh, a)


@files.get("/tracks/{id}/video/info", response_model=FileInfo,
           summary="The track's video: fetched and kept first (unless fetch=false), then described")
async def video_info(id: str, fetch: bool = True, fresh: Fresh = False, a: Acting = Depends(media_acting)):
    return await _info("video", id, fetch, fresh, a)


@file_route(files, "/tracks/{id}/audio",
            summary="The track's sound (m4a), with Range")
async def audio(request: Request, id: str, fresh: Fresh = False, a: Acting = Depends(media_acting)):
    return media.serve(request, await media.track_file("youtube", "audio", id, a, fresh))


@file_route(files, "/tracks/{id}/video",
            summary="The track's video (mp4, up to 720p, sound and picture), with Range")
async def video(request: Request, id: str, fresh: Fresh = False, a: Acting = Depends(media_acting)):
    return media.serve(request, await media.track_file("youtube", "video", id, a, fresh))
