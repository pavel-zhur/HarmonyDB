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

## Stage three: the Azure Functions apps as ASP.NET modules in one application

**What there was.** Ten Azure Functions apps (isolated worker, with the ASP.NET Core integration): Index, Authorization,
Billing, Collectives, Frontend API, Illustrations, Sources (Self) and the three bots' runners (the library bot, OneDog,
OneDragon). Each was its own host with its own settings and keys; they called each other over HTTP through their
clients. What the Functions host and worker gave them:

- the routes `api/{function name}`, the methods, and the authorization level of each function: anonymous, or a key
  (`?code=` or the `x-functions-key` header) refused with 401 and no body;
- an error not handled by a function, or a request that could not be bound (no body, a body that is not JSON, another
  content type), answered with 500 and no body;
- storage queues: the bots' updates (accepted by `IncomingUpdate`, processed by a queue trigger, up to 16 at a time,
  a failed one tried again after 30 seconds, 5 times in all), the library bot's rebuilds (`Regenerate` and the hourly
  timer queued one, a queue trigger ran it) and the billing usages (sent by `BillingApiClient.Add`);
- timers: the library bot's `Hourly` and `DailySelection`, and three `Warmups` that only kept the apps from sleeping;
- Application Insights, when the app had its connection string.

**What there is.**

- `OneShelf.Common.Api` holds what the host and the worker gave, in plain ASP.NET Core:
  - `ApiModule`: what a Functions app was. A module has a name; its functions are served under the path base of the
    name in lower case (`/index/api/...`, `/authorization/api/...`, `/sourcesself/api/...`), so that the functions of
    the same name in different modules (`Ping`, `IncomingUpdate`, `MigrateDb`, `V1GetSong`...) do not meet. Its
    `Configure` is what its `Program` did.
  - Each module runs as its own ASP.NET Core application inside the host (`ApiModuleHost`): its own services, its own
    hosted services, its own logging. Its settings are the host's settings with the section of its name laid over them:
    `Index:FileCacheBaseOptions:DiskPath` is the Index's setting, a setting without a section is every module's. The
    host's server passes it the requests under its path base (`ModuleServer`).
  - `[Function(name, level, methods...)]` replaces `[Function]` and `[HttpTrigger]`: the route `api/{name}` (or a route
    of its own), the methods, the level. The functions are ASP.NET Core MVC actions of the classes that have them, bound
    as the Functions bound them; a parameter of the type `HttpRequest` gets the request. The keys of a module are its
    setting `FunctionKeys` (a list); a function of the `Function` level refuses a call without one of them with 401 and
    no body, as before. An unhandled error and a request that cannot be bound are answered with 500 and no body.
  - `BackgroundQueue<T>`: items accepted at once and processed in the background of the module, a given number at a
    time, a failed one tried again after a delay, a given number of times. It lives in memory: what is in it when the
    application stops is lost.
  - `AddSchedule`: work run on a cron schedule with seconds, in UTC, as the timer triggers were, each switched on or
    off by the setting `Schedules:{name}`, which is required.
- The ten Functions projects are converted in place: each is a class library with its functions and its module
  (`IndexModule`, `AuthorizationModule`, ...), its `Program`, `host.json` and the Functions tooling files gone. The
  modules still call each other over HTTP, through their clients, with their keys, as separate applications do, so
  any of them can be run alone again.
- `OneShelf.Api` is the application that runs them all.
- `OneShelf.Common.Api.Functions` keeps what an Azure Functions app still needs from the old setup: `ConfigureApi`, the
  Functions host's setup with its error-handling middleware. It is a package for Functions apps outside these services
  that still use it; nothing in this repository depends on it.
- The admin site's PNG of an illustration takes the picture's copy from the Illustrations module through
  `IllustrationsApiClient.GetImageCopy`, at the module's endpoint in its settings, not at the address meant for
  browsers, which a server may not reach.
- The bots: `IncomingUpdate` accepts an update at once into the module's background queue, processed up to 16 at a
  time, a failed one tried again after 30 seconds, 5 times in all, as the queue's settings were. The library bot's
  rebuild is started in the background by `Regenerate` and by the `Hourly` schedule; the bot's `RegenerationQueue`
  runs the rebuilds one at a time, as it did. `DailySelection` is a schedule.
- Billing: `BillingApiClient.Add` posts the usage to the billing module's new function `Add` (of the `Function`
  level), which writes it as the queue trigger did. A failure is logged by the client, as a failure to queue was.
- Application Insights: with `APPLICATIONINSIGHTS_CONNECTION_STRING`, the application reports its requests and the
  calls it makes, and every module its logs.
- The webhook addresses the bots give Telegram (`SetWebHook`) include the module's path base.
- The API runs with the workstation garbage collector, as the Functions workers did: the server collector of an
  ASP.NET application would hold much more memory.
- `docker/` describes the stack on PostgreSQL: the database, the API, the PDF service (the in-process Functions app,
  as it is deployed, now without a storage account), the two web sites, the site, and the pollers (profile `bots`,
  not started by default). Nothing of SQL Server, Cosmos DB or Azure Storage serves it any more. The project name and
  the ports come from `.env`, so several stacks can run side by side. It is started with
  `docker compose --env-file .env up -d` in `docker/`. A new database gets its roles and schemas with
  `CreateServiceRole.sql`, and its data from the transfer, run by hand.
- The applications' images start the application's own executable, not `sh -c "exec dotnet ..."`: the shell silently
  dropped the settings whose names have dots (`Logging__LogLevel__Microsoft.EntityFrameworkCore`).

**Why.** The services no longer need the Azure Functions host, its storage account (queues, timers' leases, keys) and
its emulator, and one process uses much less memory than eleven hosts and their workers. Keeping each app a module,
with its settings, keys and HTTP clients, keeps the boundaries between the services as they were: a module can be
split off into an application of its own again, and the clients and the frontends do not change.

**What behaves differently.**

- The functions are reached under the module's path base (`http://<host>/<module>/api/...`); the clients' endpoints
  in the settings say so.
- A function called with a method it does not answer gets 405, not 404.
- The Frontend API's `Ping` answers; under the Functions worker it failed with 500 (it wrote its answer
  synchronously, which ASP.NET Core refuses).
- An update or a rebuild being processed, or waiting in the queue, when the application stops is lost; the storage
  queue kept it. A billing usage that fails to be written is not tried again.
- The warm-up timers are gone. A schedule that was due while the application was down is not run when it starts.

## Stage four: the Index's caches compact in memory

**What there was.** The Index keeps what it searches in memory, read from files built on a workstation and copied to
the server: the songs' headers, their chord progressions, the loops found in them (the structures) and the tonalities
of the songs and the loops; the full-text search over the headers is built from the headers. Each was a graph of .NET
objects: a dictionary keyed by the song's external id in every cache, so the same id string was kept four times; an
object for every header, for every song's progression and every sequence in it, for every one of the 7.3 million
links between the songs and the loops (each with two strings), two lookups over all the links, an object with a
two-dimensional array for every tonality. A file was first read whole (its decompressed bytes, or for the
progressions the whole JSON into a dictionary of byte arrays) and only then turned into the objects, so loading took
about twice the memory of the result. With all the caches loaded the API process held about 4.0 GB, and up to
6.2 GB while answering.

**What there is.**

- The same data, fully in memory, in arrays of small structs; the files and what is written to them are the same.
- `IndexIds` (in `HarmonyDB.Index.Analysis`, one instance for all the caches) keeps every song's external id and every
  loop's normalized progression once (`IdTable`) and gives each a number. The caches refer to songs and loops by
  these numbers; each cache knows the position of each of its songs or loops by number (`IdPositions`), so the
  caches meet by array lookups, not by strings.
- Headers (`IndexHeaders`): each distinct source, title, artist and tonality is kept once, as in the file; a header
  is a small struct of their indices. An `IndexHeader` is made when one is asked for.
- Full-text search (`FullTextSearch`): each word once, and the positions of the headers of each word in one array, in
  the order the lookup had them.
- Progressions (`CompactChordsProgressions`): a struct per sequence in one array; the movements of all the
  sequences (3 bytes each) in blocks of a million, each sequence a slice of a block, so the memory grows by blocks
  while the file is read and nothing is copied. `ProgressionsSearch` searches them by position through the same
  search as before, made generic over how a progression gives its sequences.
- Structures (`Structures`): the links as one array of 24-byte structs, with the numbers of their loop and song;
  the links of each loop and of each song as arrays of link indices, in the links' order; the statistics of the
  loops and of the songs computed while reading, as the LINQ of before computed them (sums and averages of the
  floats in doubles, in the links' order).
- Tonalities (`Tonalities`): the scores and the probabilities of the loops and of the songs in float arrays.
- Loading: each file is read as a stream, decompressed and buffered, straight into the final arrays; the
  progressions' JSON is read a part at a time.
- Rebuilding: the structures are rebuilt from the compact progressions, the tonalities from the compact headers and
  structures; the rebuild of the tonalities builds the EM model's objects as before, for the time of the rebuild
  only.

Measured on the production files, as the resident memory of the API process of the stack on PostgreSQL (all its
modules; 0.12 GB at start), the caches loaded one after another:

| | before | after |
|---|---|---|
| headers | +0.49 GB, 2.5 s | +0.24 GB, 1.6 s |
| progressions | +0.88 GB, 7.0 s | +0.39 GB, 2.3 s |
| structures | +1.58 GB (+1.64 GB at the highest), 14.7 s | +0.32 GB, 2.8 s |
| tonalities | +0.71 GB, 2.9 s | +0.14 GB, 1.9 s |
| full-text search | +0.20 GB, 7.9 s | +0.07 GB, 3.2 s |
| all loaded | 3.99 GB | 1.27 GB |
| after 120 requests, twice | 3.88 GB | 2.05 GB |
| the highest while answering them | 6.20 GB | 2.90 GB |

The requests got faster: the songs by chords 6.6 s on average before, 2.8 s after; the tonalities of songs 9.3 s and
6.3 s; the loops 0.42 s and 0.28 s; a loop 0.69 s and 0.39 s; the songs by title 0.95 s and 0.63 s, though two
searches with many results became slower, 0.2 s and 0.33 s, since their headers are made when asked for.

The answers of 120 requests covering every function that reads the caches are byte for byte the same as before,
apart from what was random before too (the order of the examples with equal ratings in a loop's statistics, and the
random orderings).

The rebuilds, run on copies of the production files with the code of before and of after, give the same: the
structures the same links (in another order, as each rebuild does: it runs in parallel), the tonalities a byte for
byte identical file. The full rebuild of the tonalities took the same time (5.5 minutes) and at the highest 3.3 GB
instead of 5.0 GB.

**Why.** The data itself is small: most of the memory went to the objects' headers and references, to the same
strings kept again in every cache, and to loading through whole intermediate copies. A server of 8 GB for the whole
stack must load the Index without running out of memory.

**What behaves differently.**

- A cache file that is missing is no longer logged as an error; the cache is empty, as it was.
- `HarmonyDB.Index.Analysis`: `Structures` gives its links, loops and songs through methods by number instead of
  lists, dictionaries and lookups by string, and no longer has `Reduce`; `EmModel.Deserialize` and
  `CompactChordsProgression.Deserialize` are gone (the caches read the files into the compact forms);
  `ProgressionsSearch` searches compact progressions by position instead of any `ISearchableChordsProgression`.
