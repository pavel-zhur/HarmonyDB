# HarmonyDB.Shelf.Import

The one-off import of the old shelf's database into the shelf's. A console tool, run by hand (on the local stack, and
again at the deploy).

## Running

On the local stack, as the compose service `shelf-import` (profile `import`, never started by `up`):

```
docker compose -f docker/docker-compose.yml --profile import run --rm --build --no-deps shelf-import dry-run
docker compose -f docker/docker-compose.yml --profile import run --rm --no-deps shelf-import run
docker compose -f docker/docker-compose.yml --profile import run --rm --no-deps shelf-import audit
```

- `dry-run` (the default): does everything in a transaction and rolls it back; the report says what would be done.
- `run`: the same, kept. Running it again updates and adds; it never duplicates, and never brings back what a person
  has since removed (or changes what they have changed) in the shelf. Nothing is removed.
- `audit`: compares, person by person, what each had in the old shelf with what they have now; writes nothing; exits
  with 1 when something differs.
- `second-pass`: **it reaches outside the stand. Run it
  only on the owner's word, at the pace he sets; never as a part of testing or "once more".** It is started by hand:
  `docker compose -f docker/docker-compose.yml --profile import run -d --no-deps --name shelf-import-second-pass shelf-import second-pass`.
  For the addresses the relay did not resolve from what is stored, it asks again one address at a time,
  with a pause between (`Relay:SecondPassPause`), with the keys under which a source may go out for an address it
  does not have (`Relay:SecondPassCallerKeys`). Every answer is kept at once: it stops cleanly (Ctrl+C, `docker stop`)
  and the next start goes on where it stopped; a site that fails `Relay:SecondPassSiteFailures` times in a row is left
  alone until the next start. Own chords are not asked there: the import resolves them as their author. A `run`
  afterwards takes what resolved; a variant resolved late goes to its place in the old order while nobody has changed
  that song's variants.

Two runs of the import (or an import and an audit) do not go at once; the second pass has its own lock.

The report ends with every item not taken or waiting, with the reason. Items waiting for their address (the relay did
not resolve it) are taken by a later run.

## Configuration

| Key | What |
|---|---|
| `ConnectionStrings:OldShelf`, `ConnectionStrings:OldOwnChords`, `ConnectionStrings:OldIllustrations` | The old database's songs, own chords and illustrations (read only, in read-only transactions) |
| `ConnectionStrings:Shelf` | The shelf's database |
| `Relay:Endpoint`, `Relay:Key` | The index's relay of the source contract V2, and the import's internal key there (scope `index`) |
| `Relay:CallerKeys` | The keys the import calls with: those under which every stored item is open and nothing is fetched from outside |
| `Relay:SecondPassCallerKeys`, `Relay:SecondPassPause`, `Relay:SecondPassSiteFailures` | The second pass: its keys, the pause between two addresses (`00:00:20`), and after how many failures in a row a site is left alone |
| `Illustrations:Endpoint`, `Illustrations:Key` | The illustrations service and the import's internal key there (scope `illustrations.aliases`) |
| `Import:*` | Who is who in the old data — see `ImportOptions` and `docker/import.example.json` |

On the local stack the `Import` and `Relay:CallerKeys` settings are in the git-ignored `docker/import.json`.

## What becomes what

See `Planning/Planner.cs` for the whole mapping. In short:

- people → accounts with a `telegram` sign-in (the old user id is the subject); the old per-tenant PDF flag and the
  per-person illustrations flag → `pdf`, `illustrations.generate`; a tenant's tags → custom keys from configuration;
- a configured shared tenant → one private playlist of its people, every song it shows;
- the template set → one public playlist of the configured person, with the recommended keys as their "here" keys;
- each person's personal tenants → their private playlist of what goes beyond the templates (every chords of their
  shown songs that are not chords of the template playlist);
- lists → playlists (shared for viewing: the tenant's people are members who do not add; shared for editing: they add);
- likes → hearts of the level's colour (0, chords kept with no level: white; 1–3: yellow, orange, red), at the like's
  time; keys → "mine" (what the person saw everywhere) and "here" (a list's); comments → notes (on a song of several
  variants each comment begins with its variant's letter); visits → recent; own chords → the author's own chords
  playlist; the main variant where it is not the first.
- the old illustrations, kept under the old addresses, are found by the chords' ids: after a run the import gives the
  illustrations service each resolved address with its id, as an alias (while both shelves run an item has two names);
- what a person already had in the shelf (a key, a note, a mark …) is theirs: the import never changes it; an account
  merged into another is followed there.

What the import made is recorded in the schema `import_old_shelf` of the shelf's database (not a part of the shelf's
model): by it a later run knows what is its own.
