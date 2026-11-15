# The move to one PostgreSQL

The services of this repository kept their data in three places: SQL Server (through EF Core), Cosmos DB and Azure
Storage. They are being moved to a single PostgreSQL database, in stages. This page records what there was, what there
is now and why it was decided so.

## Stage one: the EF Core databases on PostgreSQL

**What there was.** Three EF Core contexts on SQL Server: `SongsDatabase` (the library, users, likes, billing),
`DogDatabase` and `DragonDatabase` (the bots). Each service connected as its own SQL Server user, whose default schema
held its tables. Some things relied on SQL Server: a stored procedure for the next song index, two T-SQL queries
(the visited chords and searches), a filtered unique index where NULLs count as equal, the case-insensitive collation
of the database.

**What there is.**

- The provider is chosen by the `DatabaseProvider` setting (`SqlServer` when not set, or `PostgreSql`),
  through `OneShelf.Common.Database` (`UseConfiguredDatabase`).
- One PostgreSQL database, a schema per service. A service connects as its own role whose `search_path` is its schema
  (`OneShelf.Common.Database/CreateServiceRole.sql`), the counterpart of the SQL Server user and its default schema.
  No schema is written in the code. The migrations history lives in that schema too (`SearchPathHistoryRepository`).
- The models stay the same on both providers. What PostgreSQL needs is applied for it only
  (`ApplyPostgreSqlStoreTypes`): `DateTime` as `timestamp without time zone`, which keeps the local time the code
  writes, as `datetime2` did; `nvarchar(n)` column types as `character varying(n)`; a case-insensitive collation for the
  columns marked `[CaseInsensitive]`, where letter case must not matter. The stored procedure and the two queries have
  PostgreSQL versions next to the T-SQL ones.
- The PostgreSQL migrations start from a baseline of their own and live in their own projects
  (`*.PostgreSqlMigrations`), apart from the SQL Server migrations, which are untouched.
- `OneShelf.Common.Database.Transfer` fills an empty PostgreSQL from SQL Server through the EF models (the rows are read
  with the SQL Server model and written with a binary COPY as the PostgreSQL model maps them), keeps the keys and
  compares the counts and some rows. `OneShelf.Common.Database.Transfer.Tool` runs it for this repository's contexts.

**Why.** One database is simpler to run, back up and move than SQL Server, Cosmos DB and storage together. The schema per
service keeps the separation the services had, and changes nothing in their code. SQL Server stays readable so that the
data can be transferred again whenever needed.

## Stage two: what was in Cosmos DB, on PostgreSQL

**What there was.** Three stores of this repository lived in Cosmos DB, each its own client on the shared
`OneShelf.Common.Cosmos` base:

- `CollectivesCosmosDatabase` — the songs people write themselves, a document each, its versions growing inside it.
- `IllustrationsCosmosDatabase` — the prompts made for a song and the pictures made by them, each picture a document
  holding the JPEG.
- `FrontendCosmosDatabase` — the generated PDFs, kept to be given again (a cache).

The site showed the pictures by "public addresses" kept in each picture's document, pointing to an Azure storage
account. An uploader made four resized copies (1024, 512, 256 and 128 pixels) and uploaded them there, every two hours
and on demand, then wrote the addresses into the document. A new picture had no addresses, and was not shown, until it
was uploaded.

**What there is.**

- Each store is its own EF Core context, PostgreSQL only, in its own schema with its own role, as in stage one:
  `CollectivesDatabase` (`OneShelf.Collectives.Database`), `IllustrationsDatabase` (`OneShelf.Illustrations.Database`)
  and `FrontendDatabase` (`OneShelf.Frontend.Database`, formerly `OneShelf.Frontend.Database.Cosmos`). Their
  connection strings have the names of the contexts. Each has a baseline migration in its `*.PostgreSqlMigrations`
  project.
- The public methods stay as they were and the callers use them the same way. A store hands out snapshots; nothing it
  returns is tracked.
- A document is a row. What is document-shaped inside it stays JSON, in a `jsonb` column: a collective's versions,
  a prompt's lists and its trace. It is written and read as the Cosmos DB client did (Newtonsoft.Json, camelCase,
  `DocumentJson`), so the values come back as they used to.
- Writes that were upserts are upserts in one statement (`INSERT ... ON CONFLICT DO UPDATE`, `UpsertAsync`), so that
  two writers of the same key do not fail, as they did not on Cosmos DB. The patches are updates of the columns.
- The pictures live in the database. Beside each picture's JPEG, its four copies are kept, made by the store when the
  picture is added, so every picture has them from the start. The Illustrations API serves a copy by the picture's id
  and size (`images/{id}/{size}`), open to anyone as the storage addresses were, and cached by browsers for a year,
  since a copy never changes. A picture's public addresses are made when read, from the API's public address
  (`IllustrationsApiOptions:PublicEndpoint`); they are not stored, so a change of host needs no change of data.
  The uploader, its timer and the storage account settings are gone.
- The transfer tool also fills these stores: the collectives and the illustrations from an export of the Cosmos DB
  containers (a file per container, a document per line), through the stores' own methods, so every picture gets its
  copies as a new one does; the PDF cache starts empty. One command fills an empty PostgreSQL completely, and with
  `--wipe` can be run again.
- Nothing in the services uses Cosmos DB any more. `OneShelf.Common.Cosmos` stays only as a package for code
  outside this repository that still uses it.

**Why.** The stores are small and document-shaped; `jsonb` keeps their shape without a redesign, and relational columns
hold what is looked up (owner, visibility, address of the song, id). Keeping the copies of the pictures in the database
and serving them from the API removes the last tie to Azure Storage for the pictures, and makes a new picture visible
at once. Making the addresses when read keeps them right wherever the API is served from.

**What behaves differently.**

- A new picture has its copies, and is shown, as soon as it is made, not after the next upload.
- Times kept in columns (when a prompt, a picture or a PDF was made) keep microseconds, not the seven digits of
  Cosmos DB. The illustrations' hash that the site caches by changes once because of it.
- Ids are no longer limited by Cosmos DB: the PDFs' ids, which hold the artist and the title, may contain any
  characters and be of any length.
