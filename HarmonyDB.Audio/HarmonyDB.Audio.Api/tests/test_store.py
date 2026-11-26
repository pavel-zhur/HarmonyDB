import asyncio
import uuid

import pytest

import store


def test_a_write_cut_short_or_too_large_keeps_the_kept_file():
    async def body():
        await store.open_pool()
        try:
            key = "tests:" + uuid.uuid4().hex
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