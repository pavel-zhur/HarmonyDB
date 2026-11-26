"""What is not one service's: links, the status, the personal cache, files by address."""
from __future__ import annotations

from fastapi import APIRouter, Depends, Path, Query, Request

import config
import media
import store
import yandex as Y
import youtube as T
from acting import Acting, acting, playlist_acting
from auth import MEDIA, READ, require
from errors import LINK_NOT_PLAYLIST, ApiError
from models import Answer, LinkedPlaylist, Purged, Resolved, Status
from resolve import resolve
from shared import SERVICE_PROBLEMS, Fresh, file_route, problems

read = APIRouter(prefix="/internal", tags=["common"], dependencies=[Depends(require(READ))],
                 responses=SERVICE_PROBLEMS)
files = APIRouter(prefix="/internal", tags=["files"], dependencies=[Depends(require(MEDIA))])


@read.get("/status", response_model=Status, summary="The service accounts (asked now) and what is stored")
async def status():
    s = config.settings()

    async def account(service, configured: bool) -> dict:
        who = {"authed": False, "account": None}
        if configured:
            try:
                who = await service.whoami(Acting("service"))
            except ApiError:
                pass                    # a refused service account is told as not authed
        return {"configured": configured, "authed": who["authed"], "account": who["account"]}
    return {"yandex": await account(Y, bool(s.yandex_token)), "youtube": await account(T, bool(s.youtube_auth)),
            "stored": await store.counts()}


@read.get("/resolve", response_model=Resolved, summary="What a link of either service names")
async def resolve_link(url: str = Query(min_length=1, max_length=2000)):
    return resolve(url)


@read.get("/playlists/by-link", response_model=Answer[LinkedPlaylist],
          summary="A playlist of either service by its link; personal when asked as a person")
async def playlist_by_link(url: str = Query(min_length=1, max_length=2000), fresh: Fresh = False,
                           a: Acting = Depends(playlist_acting)):
    r = resolve(url)
    if r["type"] != "playlist":
        raise ApiError(400, LINK_NOT_PLAYLIST, f"the link is not a playlist's: it names: {r['type']}")
    kept = await (Y.playlist(r["id"], a, fresh) if r["service"] == "yandex" else T.playlist(r["id"], a, fresh))
    return {"storedAt": kept.stored, "fetchedNow": kept.fetched_now,
            "data": {"service": r["service"], "id": r["id"], r["service"]: kept.body}}


@read.delete("/subjects/{subject}", response_model=Purged, summary="Forget every personal answer of a subject")
async def purge_subject(subject: str = Path(min_length=1, max_length=200)):
    return {"answers": await store.purge_subject(subject)}



# ---------------------------------------------------------------- files

@file_route(files, "/files", responses=problems(400, 401, 403, 404, 413, 416, 502),
            summary="A file the services name by its address (a cover, a short video), kept, with Range")
async def file_by_url(request: Request, url: str = Query(min_length=1, max_length=2000), fresh: Fresh = False):
    return media.serve(request, await media.url_file(url, fresh))
