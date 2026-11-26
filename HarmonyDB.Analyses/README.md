# HarmonyDB.Analyses

The analyses of recordings as a source of the index (the source contract, version 2): an analysis with all it has —
the sheet, the timeline, the words, the notes, the score, the charts, the recording and its separate instruments — is
one item, open to all. Its ids begin with `ha-`. It keeps finished content and gives it as it was put; it builds
nothing and calls no one.

A module of the common application (`OneShelf.Api`, path base `/analyses/api/`), kept apart from the others: its own
schema in the common database, reached by its own role; its own folder of files; its own settings. Nothing references
it but the host (`OneShelf.Api.Tests` checks the modules' references).

## Its API

For the index (internal client with the scope `source`): the operations of the source contract
(`HarmonyDB.Source.Api.Model/V2`), `V2GetFile` among them — the files of an item, with ranges, from the folder.

For whoever fills it (internal client with the scope `analyses.write`; the client's name is part of the ids it gets,
so a client keeps its name):

| | |
|---|---|
| `PUT analyses` | `{ key, title, artists, orderedBy, madeFrom, content }` → `{ id, files }`: the content in the contract's kinds; the same key put again by the same client updates the same item. `orderedBy`, an opaque string, is kept and never given out; `madeFrom`, the music service's track the recording is (a plain service name and the track's id), when it is one, is given in the item's header |
| `GET analyses/{id}/files` | the files kept so far, with their sizes and SHA-256s |
| `PUT analyses/{id}/files/{name}` | a file the content names, the body being the file, in the content type the content gives it; larger than the application takes by default, up to `MaxFileBytes` |
| `DELETE analyses/{id}` | the item and its files |

A kind that names files (the recording, a separate instrument, the notes' MIDI file) is had, and its files given, only
once every file it names is kept. A file the content no longer names is deleted.

## Settings

| Setting | |
|---|---|
| `ConnectionStrings:AnalysesDatabase` | PostgreSQL, as the module's own role, whose search_path is its own schema. The role and the schema are made by hand, once, with `OneShelf.Common/OneShelf.Common.Database/CreateServiceRole.sql`; the module's migrations make its tables when it starts |
| `Analyses:AnalysesOptions:SourceKey`, `…:SourceTitle` | how the source labels its items |
| `Analyses:AnalysesOptions:IconName`, `…:IconFile` | its picture: the name (beginning with `ha-`) and the file (PNG, SVG, WebP); optional, together |
| `Analyses:AnalysesOptions:PublicUriTemplate` | an item's public page, with `{id}` where its id goes: the item's address, which also resolves to it |
| `Analyses:AnalysesOptions:FilesFolder` | the folder of the files: a volume of the application |
| `Analyses:AnalysesOptions:MaxFileBytes` | the largest file taken in (300 MB) |
| `Analyses:InternalClients` | the index (`source`) and whoever fills it (`analyses.write`) |

All but the picture are required: without them the application does not start.

## Tests

`HarmonyDB.Analyses.Tests` hosts the module beside the index, as the application does, on a real port and a real
PostgreSQL given in `ANALYSES_TEST_DATABASE` (a superuser's connection string: each test makes a role and a schema and
drops them); without it they are reported skipped. In Docker, as the shelf's tests are run (`HarmonyDB.Shelf/README.md`),
with `-e "ANALYSES_TEST_DATABASE=Host=shelf-tests-pg;Database=postgres;Username=postgres;Password=test"`.
