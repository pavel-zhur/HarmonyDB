"""The shapes of the answers, for the OpenAPI document. Every navigation answer comes in the
envelope Answer: the data, when it was stored, and whether it was fetched from the service now.

Fields the services do not always give are null. Yandex and YouTube have shapes of their own:
their ids and what they tell differ.
"""
from __future__ import annotations

from datetime import datetime
from typing import Any, Generic, Literal, TypeVar

from pydantic import BaseModel, ConfigDict, Field

T = TypeVar("T")


class Shape(BaseModel):
    # a field the code makes and the shape lacks is an error, found at once
    model_config = ConfigDict(extra="forbid")


class Answer(Shape, Generic[T]):
    storedAt: datetime
    fetchedNow: bool
    data: T


class Section(Shape, Generic[T]):
    total: int | None = None
    items: list[T]


class LyricWord(Shape):
    t: int
    text: str


class LyricLine(Shape):
    t: int | None = None              # the line's start, ms; null when not timed
    end: int | None = None            # YouTube: the line's end, ms
    text: str
    words: list[LyricWord] | None = None    # Yandex: by words, where it has them


# ---------------------------------------------------------------- Yandex

class YArtist(Shape):
    id: str
    name: str | None = None
    cover: str | None = None
    genres: list[Any] = []
    likes: int | None = None
    counts: dict[str, Any] = {}
    various: bool | None = None


class YLink(Shape):
    title: str | None = None
    href: str | None = None


class YCover(Shape):
    small: str | None = None
    big: str | None = None


class YArtistDetail(YArtist):
    description: str | None = None
    countries: list[Any] = []
    initDate: str | None = None
    wiki: str | None = None
    links: list[YLink] = []
    colors: dict[str, Any] = {}
    covers: list[YCover] = []


class YAlbum(Shape):
    id: str
    title: str | None = None
    version: str | None = None
    type: str | None = None
    year: int | None = None
    releaseDate: str | None = None
    genre: str | None = None
    cover: str | None = None
    trackCount: int | None = None
    likes: int | None = None
    artists: list[YArtist] = []
    labels: list[Any] = []


class YTrack(Shape):
    id: str
    realId: str
    title: str | None = None
    version: str | None = None
    durationMs: int | None = None
    available: bool | None = None
    lyricsAvailable: bool
    lyricsSynced: bool
    cover: str | None = None
    coverBig: str | None = None
    artists: list[YArtist] = []
    albums: list[YAlbum] = []
    album: YAlbum | None = None
    year: int | None = None
    genre: str | None = None
    best: bool | None = None
    label: str | None = None
    trackSource: str | None = None
    type: str | None = None
    explicit: bool | None = None
    previewDurationMs: int | None = None
    fileSize: int | None = None
    r128: dict[str, Any] | None = None
    fade: dict[str, Any] | None = None
    colors: dict[str, Any] = {}
    backgroundVideo: str | None = None
    specialAudio: list[Any] = []
    disclaimers: list[Any] = []
    availableForPremium: bool | None = None
    availableForOptions: list[Any] = []
    storageDir: str | None = None


class YPlaylist(Shape):
    id: str                       # "uid:kind"
    uid: str | None = None
    kind: int | None = None
    uuid: str | None = None
    title: str | None = None
    description: str | None = None
    trackCount: int | None = None
    durationMs: int | None = None
    likes: int | None = None
    cover: str | None = None
    owner: str | None = None
    modified: str | None = None
    visibility: str | None = None


class YClip(Shape):
    id: str
    title: str | None = None
    version: str | None = None
    duration: int | None = None
    thumb: str | None = None
    preview: str | None = None
    trackIds: list[str] = []
    artists: list[YArtist] = []
    explicit: bool | None = None


class YBest(Shape):
    type: str | None = None
    text: str | None = None
    id: str | None = None


class YSuggest(Shape):
    suggestions: list[str]
    best: YBest | None = None


class YSearch(Shape):
    text: str | None = None
    bestType: str | None = None
    tracks: Section[YTrack]
    artists: Section[YArtist]
    albums: Section[YAlbum]
    playlists: Section[YPlaylist]
    podcasts: Section[YAlbum]
    clips: Section[YClip]


class YVideo(Shape):
    title: str | None = None
    embedUrl: str | None = None
    cover: str | None = None


class YArtistPage(Shape):
    artist: YArtistDetail
    stats: dict[str, Any] = {}
    popularTracks: list[YTrack]
    albums: list[YAlbum]
    alsoAlbums: list[YAlbum]
    similarArtists: list[YArtist]
    lastReleases: list[YAlbum]
    videos: list[YVideo]


class YAlbums(Shape):
    pager: dict[str, Any] = {}
    albums: list[YAlbum]


class YPagedTracks(Shape):
    pager: dict[str, Any] = {}
    tracks: list[YTrack]


class YArtists(Shape):
    artists: list[YArtist]


class YAlbumPage(Shape):
    album: YAlbum
    bests: list[str]
    tracks: list[YTrack]


class YTracks(Shape):
    tracks: list[YTrack]


class YLyrics(Shape):
    available: bool
    format: Literal["RICH_JSON", "LRC", "TEXT"] | None = None
    writers: list[str] | None = None
    major: str | None = None
    synced: bool
    words: bool
    lines: list[LyricLine]


class YPlaylistPage(Shape):
    playlist: YPlaylist
    tracks: list[YTrack]
    similar: list[YPlaylist]
    wave: Any = None


class YChart(Shape):
    title: str | None = None
    tracks: list[YTrack]


class YSubGenre(Shape):
    id: str | None = None
    title: str | None = None


class YGenre(Shape):
    id: str | None = None
    title: str | None = None
    image: str | None = None
    color: str | None = None
    subGenres: list[YSubGenre]


class YCategories(Shape):
    genres: list[YGenre]


class YCategory(Shape):
    title: str | None = None
    pager: dict[str, Any] = {}
    albums: list[YAlbum]


class YPlaylists(Shape):
    playlists: list[YPlaylist]


class YLikes(Shape):
    total: int
    tracks: list[YTrack]


class YHistoryContext(Shape):
    type: str | None = None
    playlist: YPlaylist | None = None


class YHistoryItem(Shape):
    context: YHistoryContext
    tracks: list[YTrack]


class YHistoryTab(Shape):
    date: str | None = None
    items: list[YHistoryItem]


class YHistory(Shape):
    total: int
    tabs: list[YHistoryTab]


class TrackRef(Shape):
    id: str
    albumId: str | None = None    # looked up from the track when not given


class YRecommendFor(Shape):
    tracks: list[TrackRef] = Field(min_length=1, max_length=100)


# ---------------------------------------------------------------- YouTube

class TArtist(Shape):
    id: str | None = None         # a channel id (UC...) or a browse id
    name: str | None = None
    cover: str | None = None


class TAlbum(Shape):
    id: str | None = None         # a browse id (MPRE...)
    playlistId: str | None = None
    title: str | None = None
    year: str | None = None
    type: str | None = None
    trackCount: int | None = None
    cover: str | None = None
    artists: list[TArtist] = []
    explicit: bool | None = None


class TAlbumDetail(TAlbum):
    description: str | None = None
    duration: str | None = None


class TTrack(Shape):
    id: str | None = None         # a videoId
    title: str | None = None
    duration: str | None = None
    durationMs: int | None = None
    cover: str | None = None
    artists: list[TArtist] = []
    album: TAlbum | None = None
    explicit: bool | None = None
    views: str | None = None
    year: str | None = None
    videoType: str | None = None
    isVideo: bool | None = None
    available: bool | None = None


class TPlaylist(Shape):
    id: str | None = None
    title: str | None = None
    cover: str | None = None
    trackCount: int | str | None = None
    author: str | None = None
    description: str | None = None


class TPlaylistDetail(TPlaylist):
    duration: str | None = None
    views: int | str | None = None
    privacy: str | None = None
    owned: bool | None = None


class TSuggest(Shape):
    suggestions: list[str]


class TSearch(Shape):
    text: str
    tracks: Section[TTrack]
    videos: Section[TTrack]
    artists: Section[TArtist]
    albums: Section[TAlbum]
    playlists: Section[TPlaylist]


class TArtistDetail(Shape):
    id: str
    name: str | None = None
    cover: str | None = None
    description: str | None = None
    views: str | None = None
    subscribers: str | None = None
    monthlyListeners: str | None = None
    radioId: str | None = None
    shuffleId: str | None = None


class TArtistPage(Shape):
    artist: TArtistDetail
    songs: list[TTrack]
    albums: list[TAlbum]
    singles: list[TAlbum]
    videos: list[TTrack]
    related: list[TArtist]


class TAlbums(Shape):
    albums: list[TAlbum]


class TAlbumPage(Shape):
    album: TAlbumDetail
    tracks: list[TTrack]


class TPlaylistPage(Shape):
    playlist: TPlaylistDetail
    tracks: list[TTrack]


class TTrackPage(Shape):
    id: str
    title: str | None = None
    artists: list[TArtist] = []
    album: TAlbum | None = None
    durationMs: int | None = None
    views: str | None = None
    cover: str | None = None
    description: str | None = None
    uploadDate: str | None = None
    category: str | None = None
    unlisted: bool | None = None
    family: bool | None = None
    live: bool | None = None
    videoType: str | None = None
    isVideo: bool | None = None
    lyricsAvailable: bool


class TTracks(Shape):
    tracks: list[TTrack]


class TSimilarSection(Shape):
    title: str | None = None
    tracks: list[TTrack]
    albums: list[TAlbum]
    artists: list[TArtist]


class TSimilar(Shape):
    sections: list[TSimilarSection]


class TLyrics(Shape):
    available: bool
    source: str | None = None
    synced: bool
    words: bool
    lines: list[LyricLine]


class TChart(Shape):
    countries: list[str]
    songs: list[TTrack]
    videos: list[TTrack]
    artists: list[TArtist]
    trending: list[TTrack]


class TCategoryItem(Shape):
    id: str                       # the params that open the category
    title: str | None = None
    color: str | None = None


class TCategorySection(Shape):
    title: str
    items: list[TCategoryItem]


class TCategories(Shape):
    sections: list[TCategorySection]


class TCategoryShelf(Shape):
    title: str
    playlists: list[TPlaylist]
    albums: list[TAlbum]
    tracks: list[TTrack]
    artists: list[TArtist]


class TCategory(Shape):
    sections: list[TCategoryShelf]


class TPlaylists(Shape):
    playlists: list[TPlaylist]


class TLikes(Shape):
    total: int
    tracks: list[TTrack]


# ---------------------------------------------------------------- common

class Who(Shape):
    authed: bool
    account: str | None = None
    premium: bool | None = None


class Resolved(Shape):
    service: Literal["yandex", "youtube"]
    type: Literal["track", "album", "artist", "playlist"]
    id: str


class FileInfo(Shape):
    kept: bool
    mime: str | None = None
    bytes: int | None = None
    storedAt: datetime | None = None
    preview: bool | None = None       # Yandex: the 30-second preview, not the whole track
    codec: str | None = None
    bitrate: int | float | None = None
    vcodec: str | None = None
    height: int | None = None
    etag: str | None = None           # the validator the file is served with (ETag, If-Range)


class BlobInfo(Shape):
    key: str
    mime: str
    bytes: int
    storedAt: datetime
    etag: str                         # the validator the file is served with (ETag, If-Range)


class Purged(Shape):
    answers: int


class ServiceAccount(Shape):
    configured: bool
    authed: bool
    account: str | None = None


class Status(Shape):
    yandex: ServiceAccount
    youtube: ServiceAccount
    stored: dict[str, int]


class LinkedPlaylist(Shape):
    """A playlist opened by its link: which service and id the link names, and the playlist,
    under the service's key."""
    service: Literal["yandex", "youtube"]
    id: str
    yandex: YPlaylistPage | None = None
    youtube: TPlaylistPage | None = None
