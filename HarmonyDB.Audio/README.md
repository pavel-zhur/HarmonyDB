# HarmonyDB.Audio

An internal music service: the navigation of Yandex Music and YouTube Music, their sound and video, and a store of
files, all kept in PostgreSQL. It is called by other HarmonyDB services, never from the internet.

`HarmonyDB.Audio.Api` — the service (Python, FastAPI). Its OpenAPI document is served at `/openapi.json`; a copy is
`HarmonyDB.Audio.Api/openapi.json`.

## Every request

**An internal client's key.** Header `X-Internal-Key`: the key of a configured client. The client may use only its
scopes: `audio.read` — navigation, links, the status, purging a subject; `audio.media` — sound, video, files by
address. Only `/health` and `/openapi.json` need no key.

**How to act**, on every call to a music service (`/internal/yandex/...`, `/internal/youtube/...`,
`/internal/playlists/by-link`):

| Header | |
|---|---|
| `X-Audio-Mode` | `anonymous`, `person` or `service` |
| `X-Audio-Subject` | person mode: whose personal answers these are — an opaque id the caller keeps (1–200 characters) |
| `X-Audio-Credentials` | person mode: base64 of the person's credentials as UTF-8 text. Yandex: the OAuth token. YouTube: the request headers of a signed-in YouTube Music page, as copied from the browser's developer tools |

- `anonymous` — no account.
- `person` — the person's credentials, given in this call; never stored, never logged.
- `service` — the service account from configuration. Without one configured, the call acts anonymously.

YouTube's general navigation is asked anonymously in every mode (it is the same, and YouTube refuses part of it
under an account). On YouTube the mode decides the person's own lists and playlists.

Playlists (by id, by link, a playlist's recommendations) take `anonymous` or `person` only: asked as the service
account, its own playlists, private ones too, would be given to whoever asks (`service_not_allowed`).

Sound and video (`/tracks/{id}/audio`, `/video` and their `/info`) take `anonymous` or `service` only: they are never
fetched with a person's credentials. In service mode YouTube's files are fetched anonymously, and with the service
account's cookies only when YouTube refuses that.

## The cache

Answers are kept for good; `?fresh=true` asks the service anew and keeps the new answer. Every navigation answer
comes in an envelope: `{ storedAt, fetchedNow, data }`.

- **Shared**: songs, albums, artists, charts, lyrics … — kept once for every caller, whoever's credentials fetched
  them.
- **Personal**: the person's own lists (`/me/...`) — person mode only; kept under `X-Audio-Subject` and given to that
  subject only. Answers that may be private, or are shaped by who asks — a playlist, a playlist's recommendations,
  a Yandex radio — are personal when asked in person mode and shared otherwise, kept apart for each mode (what the
  service account is given is never given to an anonymous caller, nor the other way round). A playlist not open to
  the caller answers `not_found`, as one that is not there.
- `DELETE /internal/subjects/{subject}` forgets every personal answer of a subject; an answer that was being asked
  while the purge happened is not kept.
- **Files** are shared, in every mode. A Yandex track's sound fetched without a subscription is the 30-second preview
  (`preview: true`); it is replaced as soon as a call that may have the whole file asks for it, and a preview never
  replaces a whole file.
- A file is replaced only when its new content is whole: a download or an upload that fails or is cut short leaves
  the kept file as it was. Each file is served with an `ETag`, which changes when it is replaced; a Range request
  with an `If-Range` of another `ETag` gets the whole current file.

## Endpoints

`{s}` is `yandex` or `youtube`; ids are the service's own (Yandex: numbers, a playlist `uid:kind` or its uuid;
YouTube: videoId, channel id `UC…`, album browse id `MPRE…`, playlist id).

| | |
|---|---|
| `GET /internal/{s}/me` | who the call's account is (asked now) |
| `GET /internal/{s}/search?text=`, `/suggest?part=` | search; suggestions while typing |
| `GET /internal/{s}/artists/{id}` | an artist's page |
| `GET /internal/{s}/artists/{id}/albums` | discography (Yandex: `page`, `sort=year\|rating`) |
| `GET /internal/yandex/artists/{id}/tracks?page=`, `/artists/{id}/similar` | all tracks; similar artists |
| `GET /internal/{s}/albums/{id}`, `/tracks/{id}` | an album with its tracks; a track |
| `GET /internal/{s}/tracks/{id}/similar`, `/radio`, `/lyrics` | similar (YouTube: by sections); a radio from the track; lyrics, timed where the service has times |
| `GET /internal/{s}/playlists/{id}` | a playlist with its tracks |
| `GET /internal/playlists/by-link?url=` | a playlist of either service by its link |
| `GET /internal/yandex/playlists/{id}/recommendations` | recommendations for a playlist (an account's own) |
| `POST /internal/yandex/recommendations` | recommendations for a set of tracks `{tracks: [{id, albumId?}]}` — service mode (a temporary playlist is made in the service account and deleted) |
| `GET /internal/{s}/chart` | the chart (YouTube: `country`, `ZZ` the world) |
| `GET /internal/{s}/categories`, `/categories/{id}` | genres / moods; one of them (Yandex: `offset`, `limit`) |
| `GET /internal/{s}/me/playlists`, `/me/likes`, `/me/history`; `/internal/youtube/me/songs` | the person's own lists — person mode |
| `GET /internal/resolve?url=` | what a link names: `{service, type, id}` |
| `GET /internal/status` | the service accounts (asked now) and what is stored |
| `DELETE /internal/subjects/{subject}` | forget a subject's personal answers |
| `GET\|HEAD /internal/{s}/tracks/{id}/audio` | the sound (Yandex mp3, YouTube m4a), with Range |
| `GET\|HEAD /internal/{s}/tracks/{id}/video` | YouTube: the video (mp4, up to 720p); Yandex: the track's background loop |
| `GET /internal/{s}/tracks/{id}/audio/info`, `/internal/youtube/tracks/{id}/video/info` | the file fetched and kept (`fetch=false`: only told whether it is kept), described |
| `GET\|HEAD /internal/files?url=` | a file the services name by address (a cover, a background video), kept, with Range; https on the services' image and video hosts only, every redirect checked, at most `AUDIO_MAX_URL_FILE_BYTES` |

A file is fetched from the service on the first request and kept; later requests are served from the store.

## Errors

Every refusal is `{ code, message }`; the code is stable, the message is for developers.

| Status | Code |
|---|---|
| 400 | `bad_request`, `mode_invalid`, `subject_missing`, `credentials_missing`, `credentials_invalid`, `person_required`, `service_required`, `service_not_allowed`, `link_unknown`, `link_not_playlist`, `host_not_allowed` |
| 401 | `key_missing`, `key_invalid` |
| 403 | `scope_missing`, `credentials_rejected` (the service refused the person's credentials), `account_required` (the service gives this to an account only) |
| 404 | `not_found`, `no_file` |
| 413 | `too_large` |
| 416 | `range_not_satisfiable` |
| 500 | `internal_error` |
| 502 | `upstream_failed` |
| 503 | `service_account_missing`, `service_account_rejected` |

## Configuration

| Environment | |
|---|---|
| `AUDIO_DATABASE_URL` | `postgresql://user:password@host:port/audio`; the tables are created at start |
| `MUSIC_YANDEX_SIGN_KEY`, `MUSIC_YANDEX_MP3_SALT` | the two constants of Yandex's client (lyrics signing, file addresses) |
| `AUDIO_YANDEX_TOKEN_FILE` | optional: a file with the service account's Yandex token |
| `AUDIO_YOUTUBE_AUTH_FILE` | optional: a file with the service account's YouTube request headers (JSON, as ytmusicapi keeps them) |
| `AUDIO_MAX_URL_FILE_BYTES` | the largest file kept from an address |
| `InternalClients__N__Name`, `InternalClients__N__Key`, `InternalClients__N__Scopes__M` | the internal clients, in the .NET configuration shape |

The service listens on port 8080 and publishes no port.

## Running and testing

```
docker build -t audio-api HarmonyDB.Audio.Api
docker build --target test -t audio-api-test HarmonyDB.Audio.Api
docker run --rm -e AUDIO_DATABASE_URL=postgresql://... audio-api-test
```

The tests (the key check, the personal and shared cache with purges, the store's keeping of a file whole or not at all)
need a PostgreSQL and reach no music service. `ytmusicapi` and `yt-dlp` follow the services' changes: they are pinned, and raised by hand when a service
changes.
