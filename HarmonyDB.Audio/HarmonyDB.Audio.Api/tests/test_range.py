import os
import uuid

import pytest
from fastapi.testclient import TestClient

from conftest import MEDIA
from main import app

SIZE = 2 * (1 << 20) + 1000              # three chunks


@pytest.fixture(scope="module")
def stored():
    data = os.urandom(SIZE)
    key = f"tests/{uuid.uuid4().hex}/file.bin"
    with TestClient(app) as c:
        r = c.put(f"/internal/blobs/{key}", content=data, headers={**MEDIA, "Content-Type": "audio/mpeg"})
        assert r.status_code == 200 and r.json()["bytes"] == SIZE
        yield c, key, data


def test_whole_and_parts(stored):
    c, key, data = stored
    url = f"/internal/blobs/{key}"
    whole = c.get(url, headers=MEDIA)
    assert whole.status_code == 200 and whole.content == data and whole.headers["content-type"] == "audio/mpeg"

    across = c.get(url, headers={**MEDIA, "Range": "bytes=1048570-2097160"})
    assert across.status_code == 206 and across.content == data[1048570:2097161]
    assert across.headers["content-range"] == f"bytes 1048570-2097160/{SIZE}"

    tail = c.get(url, headers={**MEDIA, "Range": "bytes=-100"})
    assert tail.status_code == 206 and tail.content == data[-100:]

    open_end = c.get(url, headers={**MEDIA, "Range": f"bytes={SIZE - 10}-"})
    assert open_end.content == data[-10:]

    head = c.head(url, headers={**MEDIA, "Range": "bytes=0-9"})
    assert head.status_code == 206 and head.headers["content-length"] == "10"

    beyond = c.get(url, headers={**MEDIA, "Range": f"bytes={SIZE}-"})
    assert beyond.status_code == 416 and beyond.json()["code"] == "range_not_satisfiable"
    assert beyond.headers["content-range"] == f"bytes */{SIZE}"


def test_if_range_of_a_replaced_file_gets_the_whole_new_file(stored):
    c, key, data = stored
    url = f"/internal/blobs/{key}"
    old_tag = c.head(url, headers=MEDIA).headers["etag"]
    assert c.get(url, headers={**MEDIA, "Range": "bytes=0-9", "If-Range": old_tag}).status_code == 206
    new = os.urandom(1000)
    put = c.put(url, content=new, headers={**MEDIA, "Content-Type": "audio/mpeg"})
    assert put.json()["etag"] != old_tag
    stale = c.get(url, headers={**MEDIA, "Range": "bytes=0-9", "If-Range": old_tag})
    assert stale.status_code == 200 and stale.content == new


def test_a_write_cut_short_or_too_large_keeps_the_kept_file():
    import asyncio

    import store

    async def body():
        await store.open_pool()
        try:
            key = "ext:tests/" + uuid.uuid4().hex
            await store.write_blob(key, "audio/mpeg", {}, one(b"kept"), None)

            async def broken():
                yield b"x" * (3 << 20)
                raise ConnectionError("cut")
            with pytest.raises(ConnectionError):
                await store.write_blob(key, "audio/mpeg", {}, broken(), None)
            with pytest.raises(Exception) as too_large:
                await store.write_blob(key, "audio/mpeg", {}, one(b"y" * 100), 10)
            assert getattr(too_large.value, "code", None) == "too_large"
            row = await store.blob(key)
            assert row["size"] == 4 and b"".join([p async for p in store.read_blob(row["version"], 0, 3)]) == b"kept"
        finally:
            await store.close_pool()

    async def one(data):
        yield data
    asyncio.run(body())


def test_an_upload_over_the_limit_is_refused():
    with TestClient(app) as c:
        r = c.put(f"/internal/blobs/tests/{uuid.uuid4().hex}", content=b"z" * ((4 << 20) + 1), headers=MEDIA)
        assert r.status_code == 413 and r.json()["code"] == "too_large"


def test_a_file_tells_its_sha256_and_is_forgotten_when_deleted():
    import base64
    import hashlib

    data = os.urandom(3000)
    url = f"/internal/blobs/tests/{uuid.uuid4().hex}/gone.bin"
    with TestClient(app) as c:
        put = c.put(url, content=data, headers={**MEDIA, "Content-Type": "audio/flac"})
        assert put.json()["sha256"] == hashlib.sha256(data).hexdigest()
        digest = base64.b64encode(hashlib.sha256(data).digest()).decode()
        assert c.head(url, headers=MEDIA).headers["repr-digest"] == f"sha-256=:{digest}:"

        assert c.delete(url, headers=MEDIA).status_code == 204
        assert c.get(url, headers=MEDIA).status_code == 404
        assert c.delete(url, headers=MEDIA).json()["code"] == "not_found"
