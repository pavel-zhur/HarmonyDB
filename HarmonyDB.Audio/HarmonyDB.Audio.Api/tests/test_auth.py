import base64

import pytest
from fastapi.testclient import TestClient

from conftest import MEDIA, READER
from main import app


@pytest.fixture(scope="module")
def client():
    with TestClient(app) as c:
        yield c


def code(r):
    return r.status_code, r.json()["code"]


def test_key_is_required_and_checked(client):
    url = "/internal/resolve?url=https://youtu.be/abc"
    assert code(client.get(url)) == (401, "key_missing")
    assert code(client.get(url, headers={"X-Internal-Key": "reader-kez"})) == (401, "key_invalid")
    assert code(client.get(url, headers=MEDIA)) == (403, "scope_missing")
    r = client.get(url, headers=READER)
    assert r.status_code == 200 and r.json() == {"service": "youtube", "type": "track", "id": "abc"}


def test_media_scope(client):
    assert code(client.get("/internal/blobs/nothing-here", headers=READER)) == (403, "scope_missing")
    assert code(client.get("/internal/blobs/nothing-here", headers=MEDIA)) == (404, "not_found")


def test_every_service_call_says_its_mode(client):
    assert code(client.get("/internal/yandex/search?text=x", headers=READER)) == (400, "mode_invalid")
    person = {**READER, "X-Audio-Mode": "person"}
    assert code(client.get("/internal/yandex/me/likes", headers=person)) == (400, "subject_missing")
    assert code(client.get("/internal/yandex/me/likes", headers={**person, "X-Audio-Subject": "s"})) \
        == (400, "credentials_missing")
    assert code(client.get("/internal/yandex/me/likes", headers={**person, "X-Audio-Subject": "s",
                                                                  "X-Audio-Credentials": "not base64!"})) \
        == (400, "credentials_invalid")
    anonymous = {**READER, "X-Audio-Mode": "anonymous"}
    assert code(client.get("/internal/yandex/me/likes", headers=anonymous)) == (400, "person_required")
    yt = {**person, "X-Audio-Subject": "s", "X-Audio-Credentials": base64.b64encode(b"just text").decode()}
    assert code(client.get("/internal/youtube/me/likes", headers=yt)) == (400, "credentials_invalid")


def test_sound_is_never_fetched_as_a_person(client):
    person = {**MEDIA, "X-Audio-Mode": "person", "X-Audio-Subject": "s",
              "X-Audio-Credentials": base64.b64encode(b"token").decode()}
    assert code(client.get("/internal/yandex/tracks/1/audio", headers=person)) == (400, "mode_invalid")
