"""HarmonyDB.Audio.Api: Yandex Music and YouTube Music for internal clients.

Navigation of both services, their sound and video, and a store of files, all kept in PostgreSQL.
Every request is an internal client's (auth.py); every call to a service says how it acts
(acting.py); answers are kept shared or per subject (cache.py).
"""
from __future__ import annotations

import logging
from contextlib import asynccontextmanager

import httpx
from fastapi import FastAPI, Request
from fastapi.exceptions import RequestValidationError
from fastapi.openapi.utils import get_openapi
from fastapi.responses import JSONResponse
from fastapi.routing import APIRoute

import api_common
import api_yandex
import api_youtube
import config
import store
from errors import BAD_REQUEST, INTERNAL_ERROR, MODE_INVALID, UPSTREAM_FAILED, ApiError, Problem

log = logging.getLogger("audio")


@asynccontextmanager
async def lifespan(app: FastAPI):
    config.settings()                       # a wrong configuration stops the start
    await store.open_pool()
    yield
    await store.close_pool()


def operation_id(route: APIRoute) -> str:
    """Plain operation ids for generated clients: the service, then the route's name - YandexArtistAlbums."""
    service = next((s for s in ("yandex", "youtube") if route.path.startswith(f"/internal/{s}/")), "")
    return "".join(w.capitalize() for w in f"{service}_{route.name}".split("_") if w)


app = FastAPI(title="HarmonyDB.Audio.Api", version="1", lifespan=lifespan, docs_url=None, redoc_url=None,
              generate_unique_id_function=operation_id,
              responses={500: {"model": Problem, "description": "An unexpected error: { code, message }"}},
              description="Yandex Music and YouTube Music for internal clients. See the README.")


def problem(status: int, code: str, message: str, headers: dict | None = None) -> JSONResponse:
    return JSONResponse({"code": code, "message": message}, status_code=status, headers=headers)


@app.exception_handler(ApiError)
async def api_error(request: Request, e: ApiError):
    return problem(e.status, e.code, e.message, e.headers)


@app.exception_handler(httpx.HTTPError)
async def upstream(request: Request, e: httpx.HTTPError):
    # a music service or its file host did not answer: timeouts, refused connections, broken streams
    return problem(502, UPSTREAM_FAILED, f"a music service did not answer: {type(e).__name__}")


@app.exception_handler(RequestValidationError)
async def invalid(request: Request, e: RequestValidationError):
    if any(err["loc"][:2] == ("header", "X-Audio-Mode") for err in e.errors()):
        return problem(400, MODE_INVALID, "X-Audio-Mode is missing, or not a mode this endpoint takes")
    where = "; ".join(f"{'.'.join(str(x) for x in err['loc'])}: {err['msg']}" for err in e.errors())
    return problem(400, BAD_REQUEST, where)


@app.exception_handler(Exception)
async def unexpected(request: Request, e: Exception):
    log.exception("unexpected error on %s %s", request.method, request.url.path)
    return problem(500, INTERNAL_ERROR, type(e).__name__)


def openapi() -> dict:
    """The document without FastAPI's 422: an invalid request is answered 400 with a code (above)."""
    if app.openapi_schema is None:
        schema = get_openapi(title=app.title, version=app.version, description=app.description, routes=app.routes)
        for path in schema["paths"].values():
            for operation in path.values():
                operation["responses"].pop("422", None)
        for name in ("HTTPValidationError", "ValidationError"):
            schema["components"]["schemas"].pop(name, None)
        app.openapi_schema = schema
    return app.openapi_schema


app.openapi = openapi


@app.get("/health", include_in_schema=False)
async def health():
    return {"ok": True}


app.include_router(api_yandex.nav)
app.include_router(api_yandex.files)
app.include_router(api_youtube.nav)
app.include_router(api_youtube.files)
app.include_router(api_common.read)
app.include_router(api_common.files)
