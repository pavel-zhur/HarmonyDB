"""Yandex Music over HTTP: /internal/yandex/..."""
from __future__ import annotations

from typing import Literal

from fastapi import APIRouter, Body, Depends, Query, Request

import media
import yandex as Y
from acting import Acting, acting, media_acting, playlist_acting
from auth import MEDIA, READ, require
from errors import NOT_FOUND, ApiError
from models import (Answer, FileInfo, Who, YAlbumPage, YAlbums, YArtistPage, YArtists, YCategories, YCategory,
                    YChart, YHistory, YLikes, YLyrics, YPagedTracks, YPlaylistPage, YPlaylists, YRecommendFor,
                    YSearch, YSuggest, YTrack, YTracks)
from shared import SERVICE_PROBLEMS, Fresh, envelope, file_info, file_route, problems

nav = APIRouter(prefix="/internal/yandex", tags=["yandex"], dependencies=[Depends(require(READ))],
                responses=SERVICE_PROBLEMS)
files = APIRouter(prefix="/internal/yandex", tags=["yandex files"], dependencies=[Depends(require(MEDIA))],
                  responses=problems(400, 401, 403, 404, 416, 502, 503))





@nav.get("/me", response_model=Who, summary="Who the call's account is (asked now, not kept)")
async def me(a: Acting = Depends(acting)):
    return await Y.whoami(a)


@nav.get("/search", response_model=Answer[YSearch])
async def search(text: str = Query(min_length=1, max_length=300), fresh: Fresh = False, a: Acting = Depends(acting)):
    return envelope(await Y.search(text, a, fresh))


@nav.get("/suggest", response_model=Answer[YSuggest])
async def suggest(part: str = Query(min_length=1, max_length=100), fresh: Fresh = False, a: Acting = Depends(acting)):
    return envelope(await Y.suggest(part, a, fresh))


@nav.get("/artists/{id}", response_model=Answer[YArtistPage])
async def artist(id: str, fresh: Fresh = False, a: Acting = Depends(acting)):
    return envelope(await Y.artist(id, a, fresh))


@nav.get("/artists/{id}/albums", response_model=Answer[YAlbums], summary="The artist's albums, 24 a page")
async def artist_albums(id: str, page: int = Query(0, ge=0), sort: Literal["year", "rating"] = "year",
                        fresh: Fresh = False, a: Acting = Depends(acting)):
    return envelope(await Y.artist_albums(id, page, sort, a, fresh))


@nav.get("/artists/{id}/tracks", response_model=Answer[YPagedTracks], summary="The artist's tracks, 50 a page")
async def artist_tracks(id: str, page: int = Query(0, ge=0), fresh: Fresh = False, a: Acting = Depends(acting)):
    return envelope(await Y.artist_tracks(id, page, a, fresh))


@nav.get("/artists/{id}/similar", response_model=Answer[YArtists], summary="Artists similar to the artist")
async def similar_artists(id: str, fresh: Fresh = False, a: Acting = Depends(acting)):
    return envelope(await Y.similar_artists(id, a, fresh))


@nav.get("/albums/{id}", response_model=Answer[YAlbumPage])
async def album(id: str, fresh: Fresh = False, a: Acting = Depends(acting)):
    return envelope(await Y.album(id, a, fresh))


@nav.get("/tracks/{id}", response_model=Answer[YTrack])
async def track(id: str, fresh: Fresh = False, a: Acting = Depends(acting)):
    return envelope(await Y.track(id, a, fresh))


@nav.get("/tracks/{id}/similar", response_model=Answer[YTracks])
async def similar(id: str, fresh: Fresh = False, a: Acting = Depends(acting)):
    return envelope(await Y.similar(id, a, fresh))


@nav.get("/tracks/{id}/radio", response_model=Answer[YTracks], summary="A radio seeded by the track")
async def radio(id: str, fresh: Fresh = False, a: Acting = Depends(acting)):
    return envelope(await Y.radio(id, a, fresh))


@nav.get("/tracks/{id}/lyrics", response_model=Answer[YLyrics])
async def lyrics(id: str, fresh: Fresh = False, a: Acting = Depends(acting)):
    return envelope(await Y.lyrics(id, a, fresh))


@nav.get("/playlists/{id}", response_model=Answer[YPlaylistPage],
         summary="A playlist by its id: \"uid:kind\" or its uuid (personal when asked as a person)")
async def playlist(id: str, fresh: Fresh = False, a: Acting = Depends(playlist_acting)):
    return envelope(await Y.playlist(id, a, fresh))


@nav.get("/playlists/{id}/recommendations", response_model=Answer[YTracks])
async def playlist_recommendations(id: str, fresh: Fresh = False, a: Acting = Depends(playlist_acting)):
    return envelope(await Y.playlist_recommendations(id, a, fresh))


@nav.post("/recommendations", response_model=Answer[YTracks],
          summary="Recommendations for a set of tracks (service mode only)")
async def recommend_for(body: YRecommendFor = Body(...), fresh: Fresh = False, a: Acting = Depends(acting)):
    return envelope(await Y.recommend_for([(t.id, t.albumId) for t in body.tracks], a, fresh))


@nav.get("/chart", response_model=Answer[YChart])
async def chart(fresh: Fresh = False, a: Acting = Depends(acting)):
    return envelope(await Y.chart(a, fresh))


@nav.get("/categories", response_model=Answer[YCategories], summary="Genres, with their sub-genres")
async def categories(fresh: Fresh = False, a: Acting = Depends(acting)):
    return envelope(await Y.categories(a, fresh))


@nav.get("/categories/{id}", response_model=Answer[YCategory], summary="A genre's albums, page by page")
async def category(id: str, offset: int = Query(0, ge=0), limit: int = Query(24, ge=1, le=100),
                   fresh: Fresh = False, a: Acting = Depends(acting)):
    return envelope(await Y.category(id, offset, limit, a, fresh))


@nav.get("/me/playlists", response_model=Answer[YPlaylists], summary="The person's playlists (person mode)")
async def my_playlists(fresh: Fresh = False, a: Acting = Depends(acting)):
    return envelope(await Y.my_playlists(a, fresh))


@nav.get("/me/likes", response_model=Answer[YLikes], summary="The person's liked tracks (person mode)")
async def my_likes(fresh: Fresh = False, a: Acting = Depends(acting)):
    return envelope(await Y.likes(a, fresh))


@nav.get("/me/history", response_model=Answer[YHistory], summary="The person's history (person mode)")
async def my_history(fresh: Fresh = False, a: Acting = Depends(acting)):
    return envelope(await Y.history(a, fresh))


# ---------------------------------------------------------------- files

@files.get("/tracks/{id}/audio/info", response_model=FileInfo,
           summary="The track's sound: fetched and kept first (unless fetch=false), then described")
async def audio_info(id: str, fetch: bool = True, fresh: Fresh = False, a: Acting = Depends(media_acting)):
    row = await media.track_file("yandex", "audio", id, a, fresh) if fetch \
        else await media.kept_track_file("yandex", "audio", id)
    return file_info(row)


@file_route(files, "/tracks/{id}/audio",
            summary="The track's sound (mp3), with Range")
async def audio(request: Request, id: str, fresh: Fresh = False, a: Acting = Depends(media_acting)):
    return media.serve(request, await media.track_file("yandex", "audio", id, a, fresh))


@file_route(files, "/tracks/{id}/video",
            summary="The track's background video (a short loop), with Range")
async def video(request: Request, id: str, fresh: Fresh = False, a: Acting = Depends(media_acting)):
    url = (await Y.track(id, a, False)).body.get("backgroundVideo")
    if not url:
        raise ApiError(404, NOT_FOUND, "the track has no background video")
    return media.serve(request, await media.url_file(url, fresh))
