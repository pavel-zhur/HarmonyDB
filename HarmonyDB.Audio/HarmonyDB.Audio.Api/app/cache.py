"""The keeping of answers.

  * shared   - general answers (songs, albums, artists, charts ...): kept once for every caller,
               whoever's credentials fetched them;
  * personal - a person's own lists (playlists, likes, history, library): kept under the call's
               subject and given back to that subject only; person mode only;
  * private  - answers that may be private to whoever asks (a playlist) or shaped by who asks (a
               radio tuned to the account): personal when asked as a person, shared otherwise.

An answer, once kept, is given from the store; `fresh` asks the service anew and keeps the new one.
"""
from __future__ import annotations

from dataclasses import dataclass
from datetime import datetime
from typing import Any, Awaitable, Callable

import store
from acting import Acting
from errors import PERSON_REQUIRED, ApiError

Make = Callable[[], Awaitable[Any]]


@dataclass(frozen=True)
class Answer:
    body: Any
    stored: datetime
    fetched_now: bool


def require_person(acting: Acting) -> str:
    if acting.mode != "person":
        raise ApiError(400, PERSON_REQUIRED, "a person's own lists are asked in person mode only")
    return acting.subject


async def _kept(service: str, kind: str, key: str, subject: str, fresh: bool, make: Make) -> Answer:
    if not fresh:
        hit = await store.get_answer(service, kind, key, subject)
        if hit is not None:
            return Answer(hit[0], hit[1], False)
    asked = await store.clock()
    body = await make()
    # a subject purged while its answer was being asked does not get the answer kept again
    stored = await store.put_answer(service, kind, key, subject, body, asked)
    return Answer(body, stored or asked, True)


async def shared(service: str, kind: str, key: str, fresh: bool, make: Make) -> Answer:
    return await _kept(service, kind, key, store.SHARED, fresh, make)


async def personal(service: str, kind: str, key: str, acting: Acting, fresh: bool, make: Make) -> Answer:
    return await _kept(service, kind, key, require_person(acting), fresh, make)


async def private(service: str, kind: str, key: str, acting: Acting, fresh: bool, make: Make) -> Answer:
    subject = acting.subject if acting.mode == "person" else store.SHARED
    return await _kept(service, kind, key, subject, fresh, make)
