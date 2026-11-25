"""How one call acts on a music service, as the caller says in its headers:

  X-Audio-Mode         anonymous | person | service
  X-Audio-Subject      person mode: whose personal answers these are (an opaque id the caller keeps)
  X-Audio-Credentials  person mode: base64 of the person's credentials (UTF-8 text) -
                       Yandex: the OAuth token; YouTube: the request headers as copied from the browser

A person's credentials live in this one call only: never stored, never logged. Sound and video
are never fetched with them: those calls take anonymous or service mode only.
"""
from __future__ import annotations

import base64
import binascii
from dataclasses import dataclass, field
from typing import Literal

from fastapi import Header

from errors import CREDENTIALS_INVALID, CREDENTIALS_MISSING, SERVICE_NOT_ALLOWED, SUBJECT_MISSING, ApiError

Mode = Literal["anonymous", "person", "service"]


@dataclass(frozen=True)
class Acting:
    mode: Mode
    subject: str | None = None
    credentials: str | None = field(default=None, repr=False)


def acting(
    x_audio_mode: Mode = Header(alias="X-Audio-Mode", description="How to act: anonymous, person or service."),
    x_audio_subject: str | None = Header(None, alias="X-Audio-Subject",
                                         description="Person mode: the subject personal answers are kept under."),
    x_audio_credentials: str | None = Header(None, alias="X-Audio-Credentials",
                                             description="Person mode: base64 of the person's credentials."),
) -> Acting:
    if x_audio_mode != "person":
        return Acting(x_audio_mode)
    subject = (x_audio_subject or "").strip()
    if not subject or len(subject) > 200:
        raise ApiError(400, SUBJECT_MISSING, "person mode needs X-Audio-Subject (1-200 characters)")
    if not x_audio_credentials:
        raise ApiError(400, CREDENTIALS_MISSING, "person mode needs X-Audio-Credentials")
    try:
        credentials = base64.b64decode(x_audio_credentials, validate=True).decode("utf-8").strip()
    except (binascii.Error, UnicodeDecodeError):
        raise ApiError(400, CREDENTIALS_INVALID, "X-Audio-Credentials is not base64 of UTF-8 text") from None
    if not credentials:
        raise ApiError(400, CREDENTIALS_MISSING, "X-Audio-Credentials is empty")
    return Acting("person", subject, credentials)


def playlist_acting(
    x_audio_mode: Mode = Header(alias="X-Audio-Mode", description="How to act: anonymous or person (not service)."),
    x_audio_subject: str | None = Header(None, alias="X-Audio-Subject",
                                         description="Person mode: the subject personal answers are kept under."),
    x_audio_credentials: str | None = Header(None, alias="X-Audio-Credentials",
                                             description="Person mode: base64 of the person's credentials."),
) -> Acting:
    """A playlist is asked anonymously or as a person, never as the service account: asked as it, a playlist (of the
    account's own, private ones too) would be given to whoever asks, and kept as shared."""
    if x_audio_mode == "service":
        raise ApiError(400, SERVICE_NOT_ALLOWED, "a playlist is asked anonymously or as a person, not as the service account")
    return acting(x_audio_mode, x_audio_subject, x_audio_credentials)


def media_acting(
    x_audio_mode: Literal["anonymous", "service"] = Header(
        alias="X-Audio-Mode", description="How to fetch the file: anonymous or service."),
) -> Acting:
    return Acting(x_audio_mode)
