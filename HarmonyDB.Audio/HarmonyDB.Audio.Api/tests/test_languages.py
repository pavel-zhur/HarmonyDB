import asyncio

import yandex as Y
from acting import Acting
from cache import Answer

GENRES = [{
    "urlPart": "rock", "titles": {"ru": {"title": "Рок"}, "en": {"title": "Rock"}},
    "subGenres": [{"urlPart": "punk", "titles": {"ru": {"title": "Панк"}}}, {"urlPart": "nameless", "titles": {}}],
}]


def test_genres_are_named_in_the_language_asked_and_stored_apart(monkeypatch):
    asked = []

    class Session:
        def __init__(self, lang):
            self.lang = lang

        async def __aenter__(self):
            return self

        async def __aexit__(self, *exc):
            return None

        async def get(self, path, params=None):
            asked.append((path, self.lang))
            return GENRES
    monkeypatch.setattr(Y, "_session", lambda acting, timeout=30, lang="ru": Session(lang))

    keys = []

    async def shared(service, kind, key, fresh, make):
        keys.append((kind, key))
        return Answer(await make(), None, True)
    monkeypatch.setattr(Y.cache, "shared", shared)

    def names(lang):
        genre = asyncio.run(Y.categories(Acting("anonymous"), False, lang)).body["genres"][0]
        return genre["title"], [x["title"] for x in genre["subGenres"]]

    # a name not had in the language asked is given in another; one with none, by its id
    assert names("en") == ("Rock", ["Панк", "nameless"])
    assert names("ru") == ("Рок", ["Панк", "nameless"])
    assert asked == [("genres", "en"), ("genres", "ru")]
    assert keys == [("categories", "en"), ("categories", "ru")]
