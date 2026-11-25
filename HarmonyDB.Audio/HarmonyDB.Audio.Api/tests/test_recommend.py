import asyncio
from types import SimpleNamespace

import yandex as Y
from acting import Acting
from cache import Answer


def test_a_track_with_no_album_is_left_out_of_the_recommendations(monkeypatch):
    # the service account is configured; the tracks' pages are as Yandex gives them; nothing is asked of Yandex
    monkeypatch.setattr(Y.config, "settings", lambda: SimpleNamespace(yandex_token="token"))
    pages = {"1": {"album": {"id": "10"}}, "2": {}, "3": {"album": None}}

    async def track(tid, acting, fresh):
        return Answer(pages[tid], None, False)
    monkeypatch.setattr(Y, "track", track)

    made = []

    async def shared(service, kind, key, fresh, make):
        made.append(key)
        return Answer({"key": key} if key else await make(), None, True)
    monkeypatch.setattr(Y.cache, "shared", shared)

    service = Acting("service")
    assert asyncio.run(Y.recommend_for([("1", None), ("2", None), ("3", None)], service, False)).body == {"key": "1:10"}
    assert asyncio.run(Y.recommend_for([("2", None), ("3", None)], service, False)).body == {"tracks": []}
    assert made == ["1:10", ""]
