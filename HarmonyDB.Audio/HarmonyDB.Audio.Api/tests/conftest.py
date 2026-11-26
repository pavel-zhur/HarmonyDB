"""The tests run in the image (Dockerfile target `test`) against a PostgreSQL given by
AUDIO_DATABASE_URL; they reach no music service."""
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

os.environ.setdefault("MUSIC_YANDEX_SIGN_KEY", "test-sign-key")
os.environ.setdefault("MUSIC_YANDEX_MP3_SALT", "test-salt")
os.environ.setdefault("AUDIO_MAX_URL_FILE_BYTES", str(1 << 20))
os.environ.pop("AUDIO_YANDEX_TOKEN_FILE", None)
os.environ.pop("AUDIO_YOUTUBE_AUTH_FILE", None)
os.environ.update({
    "InternalClients__0__Name": "reader", "InternalClients__0__Key": "reader-key",
    "InternalClients__0__Scopes__0": "audio.read",
    "InternalClients__1__Name": "media", "InternalClients__1__Key": "media-key",
    "InternalClients__1__Scopes__0": "audio.media",
})

READER = {"X-Internal-Key": "reader-key"}
MEDIA = {"X-Internal-Key": "media-key"}
