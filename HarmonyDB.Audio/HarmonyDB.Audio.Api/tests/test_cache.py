import asyncio
import uuid

import cache
import store
from acting import Acting


def run(body):
    async def wrapped():
        await store.open_pool()
        try:
            await body()
        finally:
            await store.close_pool()
    asyncio.run(wrapped())


def test_personal_answers_stay_with_their_subject():
    async def body():
        key = uuid.uuid4().hex
        ann = Acting("person", "subject-ann-" + key, "token-a")
        bob = Acting("person", "subject-bob-" + key, "token-b")

        async def mine_a():
            return {"of": "ann"}

        async def mine_b():
            return {"of": "bob"}
        assert (await cache.personal("yandex", "likes", key, ann, False, mine_a)).body == {"of": "ann"}
        b = await cache.personal("yandex", "likes", key, bob, False, mine_b)
        assert b.body == {"of": "bob"} and b.fetched_now
        again = await cache.personal("yandex", "likes", key, ann, False, mine_b)
        assert again.body == {"of": "ann"} and not again.fetched_now

        # a playlist asked as a person is that person's; asked otherwise it is shared
        assert (await cache.private("yandex", "playlist", key, ann, False, mine_a)).body == {"of": "ann"}
        shared = await cache.private("yandex", "playlist", key, Acting("service"), False, mine_b)
        assert shared.body == {"of": "bob"} and shared.fetched_now

        assert await store.purge_subject(ann.subject) == 2
        fresh = await cache.personal("yandex", "likes", key, ann, False, mine_b)
        assert fresh.fetched_now and fresh.body == {"of": "bob"}
        assert (await cache.personal("yandex", "likes", key, bob, False, mine_a)).body == {"of": "bob"}
    run(body)


def test_shared_answers_are_one_for_everyone():
    async def body():
        key = uuid.uuid4().hex
        calls = []

        async def make():
            calls.append(1)
            return {"n": len(calls)}
        first = await cache.shared("youtube", "track", key, False, make)
        second = await cache.shared("youtube", "track", key, False, make)
        assert first.fetched_now and not second.fetched_now and second.body == {"n": 1}
        renewed = await cache.shared("youtube", "track", key, True, make)
        assert renewed.fetched_now and renewed.body == {"n": 2}
    run(body)


def test_an_answer_asked_before_a_purge_is_not_kept_after_it():
    async def body():
        ann = Acting("person", "subject-race-" + uuid.uuid4().hex, "token")

        async def slow():
            await store.purge_subject(ann.subject)      # the purge lands while the answer is asked
            return {"of": "ann"}
        first = await cache.personal("yandex", "likes", "", ann, False, slow)
        assert first.body == {"of": "ann"} and first.fetched_now
        assert await store.get_answer("yandex", "likes", "", ann.subject) is None
    run(body)


def test_a_private_answer_is_kept_apart_for_each_mode():
    # what the service account is given is never served to an anonymous caller, nor the other way round
    async def body():
        key = uuid.uuid4().hex

        async def as_service():
            return {"for": "service"}

        async def anonymously():
            return {"for": "anyone"}
        assert (await cache.private("yandex", "radio", key, Acting("service"), False, as_service)).body == {"for": "service"}
        anonymous = await cache.private("yandex", "radio", key, Acting("anonymous"), False, anonymously)
        assert anonymous.body == {"for": "anyone"} and anonymous.fetched_now
        again = await cache.private("yandex", "radio", key, Acting("service"), False, anonymously)
        assert again.body == {"for": "service"} and not again.fetched_now
    run(body)


def test_no_id_spells_another_modes_key():
    async def body():
        key = uuid.uuid4().hex

        async def as_service():
            return {"for": "service"}

        async def anonymously():
            return {"for": "anyone"}
        await cache.private("yandex", "playlist", key, Acting("service"), False, as_service)
        imitated = await cache.private("yandex", "playlist", f"service|{key}", Acting("anonymous"), False, anonymously)
        assert imitated.body == {"for": "anyone"} and imitated.fetched_now
    run(body)
