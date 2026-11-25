"""What the routes share."""
from __future__ import annotations

from typing import Annotated

from fastapi import Query

import media
from cache import Answer
from errors import Problem

Fresh = Annotated[bool, Query(description="Ask the service anew instead of giving the stored answer; "
                                          "the new answer is stored.")]

FILE_RESPONSES = {
    200: {"description": "The whole file", "content": {"application/octet-stream": {}}},
    206: {"description": "The part the Range asked for", "content": {"application/octet-stream": {}}},
}


def problems(*statuses: int) -> dict:
    """The refusals a route really gives, for the OpenAPI document."""
    return {s: {"model": Problem, "description": "A refusal: { code, message }"} for s in statuses}


# what a call to a music service may answer besides its result
SERVICE_PROBLEMS = problems(400, 401, 403, 404, 502, 503)


def envelope(a: Answer) -> dict:
    return {"storedAt": a.stored, "fetchedNow": a.fetched_now, "data": a.body}


def file_info(row: dict | None) -> dict:
    if row is None:
        return {"kept": False}
    info = row["info"] or {}
    return {"kept": True, "mime": row["mime"], "bytes": row["size"], "storedAt": row["stored"],
            "preview": info.get("preview"), "codec": info.get("codec"), "bitrate": info.get("bitrate") or info.get("abr"),
            "vcodec": info.get("vcodec"), "height": info.get("height"), "etag": media.etag(row)}


def file_route(router, path: str, summary: str, responses: dict | None = None):
    """A file: GET in the OpenAPI document, HEAD beside it (the same, without the body)."""
    def register(handler):
        router.add_api_route(path, handler, methods=["GET"], responses={**FILE_RESPONSES, **(responses or {})},
                             summary=summary)
        router.add_api_route(path, handler, methods=["HEAD"], include_in_schema=False, name=handler.__name__ + "_head")
        return handler
    return register
