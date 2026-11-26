# HarmonyDB Shelf

The backend of the new shelf: a chord library of people and playlists.

## Layout

| Project | What |
|---|---|
| `HarmonyDB.Shelf.Api` | ASP.NET Core (.NET 8): the browser API under `/api`, later sign-in, MCP and the internal API |
| `HarmonyDB.Shelf.Database` | The EF Core model of the database `shelf` (PostgreSQL) and its migrations |
| `HarmonyDB.Shelf.Model` | The browser API's request and response types, and `openapi.json` generated from the API |
| `HarmonyDB.Shelf.Tests` | Tests (xUnit) |
| `../HarmonyDB.Common/HarmonyDB.Common.ServiceAuth` | Internal clients with keys and scopes, for any ASP.NET Core service |
| `../HarmonyDB.Source/HarmonyDB.Source.Api.Model/V2` | The source contract, version 2 |
| `../docker` | The services, the shelf among them (`docker-compose.yml`) |

Inside `HarmonyDB.Shelf.Api`:

- `Foundation/` — what every area stands on: configuration, the database, JSON, error answers, permissions,
  internal clients, OpenAPI. `Program.cs` calls it once.
- `Areas/<Area>/<Area>Area.cs` — one area's endpoints and registration; `Areas/<Area>/Services/` — its services.

## An area

An area (`auth`, `me`, `playlists` …) is one class implementing `IShelfArea` plus a folder of services, and one line
in `Program.cs`:

```csharp
builder.AddArea<PlaylistsArea>();
```

`AddServices` registers the area's services (and its authentication schemes, if any); `MapEndpoints` maps a route
group `/api/<area>` with its tag; `UseMiddleware` (optional) adds the area's middleware, after authentication and
before authorization. See
`Areas/App` (health, version, the public configuration). The area's request and response types go to
`HarmonyDB.Shelf.Model/<Area>/`, its own error codes to a static class there. An area touches no other area's files.

- **Errors** are stable codes, never human text: throw `ShelfException` (`ShelfException.NotFound("no_such_playlist")`);
  the answer is `{ "code": "...", "details": { ... } }` (`ErrorResponse`). An unexpected exception answers 500
  `internal_error` and is logged; an answer with an error status and no body gets its code too. Shared codes are in
  `ErrorCodes`.
- **Permissions** are checked in one place. On an endpoint: `.RequirePermission(BuiltInPermissions.Pdf)` or
  `[RequirePermission(...)]`; for writing actions of signed-in people `.RequireSignIn()`. A guest without the permission
  gets 401 `sign_in_required`, a person 403 `permission_denied`. `.RequireSignIn()` is for the person's own session
  only: a client acting for the person (an MCP token, a personal key) is refused it and goes by permissions alone. Elsewhere (an MCP tool): `PermissionCatalog.Has(user,
  permission)`. Built-in permissions are `BuiltInPermissions`; custom ones come from configuration only, and the code
  never names them — `PermissionCatalog.CustomKeys(user)` is what goes to the sources as `Caller.Keys`.
- **Who is signed in**: every way of signing in makes its identity with `ShelfIdentity.Create` on every request, from
  the person's permissions as stored now; a client acting for the person passes what the person allowed it, and gets
  the intersection, and its identity is marked delegated (`user.IsDelegated()`). `user.GetAccountId()` is the account
  (null for a guest).
- **Marks**: the kinds are `MarkKinds` in `HarmonyDB.Shelf.Model/Marks` (the three hearts of the old shelf's levels of
  like are `MarkKinds.OldLikeLevels`); the database keeps the key as text.
- **Rate limits**: the foundation has the one rate limiter of the app (a refusal is 429 `too_many_requests`). An area
  adds a policy in `AddServices` and puts it on its endpoints:

  ```csharp
  services.AddShelfRateLimit("media", context => RateLimitPartition.GetFixedWindowLimiter(
      context.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
      _ => new FixedWindowRateLimiterOptions { PermitLimit = …, Window = … }));   // limits from the area's settings
  group.MapPost("/media", …).RequireRateLimiting("media");
  ```

  The client's address is the proxy's view of it only when `Shelf:Proxy:TrustedNetworks` names the proxy.
- **Internal endpoints** (other services calling with `X-Internal-Key`): `.RequireInternalScope("shelf.admin")`. They,
  and everything outside `/api` (the MCP), are left out of the browser's `openapi.json`.
- **JSON** is camel case with enums as camel-case strings. Name every endpoint (`.WithName(...)`): the name is the
  operation id of the frontend's client.

## The database

- Tables and columns are snake case; enums are kept as snake-case text.
- Rows that change in front of others (playlists, members, songs, items, preferences, marks, keys, notes, recent) are
  `IVersioned`: their `version` is written by the database (a trigger) as the id of the transaction that wrote them.
  `ShelfDbContext.GetChangeWatermarkAsync()` gives a reader the watermark to ask for changes since. A migration that
  adds a versioned table calls `migrationBuilder.AddVersionTrigger("<table>")` (a test checks it).
- Removing such a row sets `DeletedAt` (`ITombstoned`): the removal reaches the readers and can be undone. Query
  filters hide removed rows; `IgnoreQueryFilters()` reads them. One rule for every such table:
  - uniqueness holds among the rows not removed (partial unique indexes); adding the same thing again makes a new row;
  - undo brings back the removed row itself, by its id; if a live row has taken its place meanwhile, there is nothing
    to undo;
  - nothing others see is hard-deleted: a playlist is removed by marking it; a profile is deleted in two steps
    (`Account.DeletedAt`: shared playlists passed on, everything others see marked removed, personal data cleared);
    hard deletes and their cascades are the purge's alone, after the tombstones' time.
- A playlist's owner is kept in two places, `playlists.owner_id` and the owner's member row (role `owner`); one service
  method changes both, in one transaction.
- Joining a playlist is only by an invitation (`playlist_invitations`); a playlist of `link` access is only looked at
  by its link. `share_links` are for a song or chords; a link of chords may carry its sharer's custom keys
  (`carried_keys`), one per sharer and chords.
- The source contract's models are read, kept and sent with `SourceV2Json.Options` only.
- The model is changed only together with the orchestrator. A migration:

  ```
  cd HarmonyDB.Shelf/HarmonyDB.Shelf.Database
  dotnet ef migrations add <Name> --output-dir Migrations
  ```

  `shelf-api` applies the migrations when it starts (`Shelf:Database:MigrateOnStart`).

## Configuration

| Key | What |
|---|---|
| `ConnectionStrings:Shelf` | The database |
| `Shelf:Database:MigrateOnStart` | Apply the migrations at start |
| `Shelf:Secrets:Key`, `Shelf:Secrets:KeyVersion`, `Shelf:Secrets:OldKeys:<version>` | The key of what is kept encrypted (32 random bytes, base64), its version, and the keys before it while rows still use them |
| `Permissions:Custom:[{Key, Label}]` | Custom permissions |
| `Permissions:Defaults:Guest`, `Permissions:Defaults:NewAccount` | The default permissions |
| `InternalClients:[{Name, Key, Scopes[]}]` | Services that may call the internal API |
| `SignIn:Telegram`, `SignIn:Google`, `SignIn:Yandex`, `SignIn:Dev` | Which sign-in providers are on (`Dev` only in the Development environment), with their clients: `ClientId`, `ClientSecret` (Telegram's from BotFather, Login Widget), and `TokenEndpointAuthMethod` (`client_secret_basic` / `client_secret_post`; not set: the provider's default — basic for Telegram, post for Google) |
| `SignIn:PublicOrigin` | The address the app is opened at (`https://…`, no path): the providers return to `<it>/api/auth/callback/<provider>`, and it decides the cookies' rules. Plain http only for a loopback host in Development (the local stack) |
| `SignIn:Sessions:IdleTimeout`, `SignIn:Sessions:AbsoluteLifetime` | When a browser session ends: unused for (default 30 days), or after its sign-in (default 180 days) |
| `SignIn:FlowLifetime`, `SignIn:FreshSignIn` | How long a sign-in at a provider may take; how recent a sign-in must be to add or remove a sign-in or to merge accounts (10 minutes each) |
| `SignIn:RateLimitPermits`, `SignIn:RateLimitWindow` | How many sign-ins one client address may start or finish in a window (30 in 5 minutes) |
| `Shelf:Proxy:TrustedNetworks` | The reverse proxies' networks (CIDR) whose `X-Forwarded-For` gives the client's address; none by default. **Set it whenever a proxy stands in front** (in the compose project: `SHELF_WEB_SUBNET`, the network of the shelf's web servers, which reach the API there as `shelf-api.shelf-web`), or every client has the proxy's address and shares every rate limit. The web servers in turn take the visitor's address from the proxies in front of them only when these are in `SHELF_EDGE_PROXIES`, and tell the API that address alone |
| `Clients:Audio:BaseUrl`, `Clients:Audio:Key` | The music service and the shelf's key there |
| `Playlists:LibraryShowLimit` | A playlist of more songs than this, joined, starts out of the person's library (2000) |
| `Clients:OwnChords:Endpoint`, `…:Key`, `…:PreviewEndpoint`, `…:PreviewKey` | The own chords' store (`…/collectives/api/`) and their editor's preview (`…/sourcesself/api/`), with the shelf's keys |
| `Clients:Illustrations:Endpoint`, `…:Key` | The illustrations service (`…/illustrations/api/`) |
| `Clients:Pdfs:Endpoint`, `…:Key` | The PDF service (`…/pdfs/api/`) |
| `Clients:Index:Endpoint`, `…:Key`, `…:Timeout` | The index (`…/index/api/`): the source contract's relay and the analysis tools, with the shelf's key |
| `Media:SigningKey`, `Media:AddressLifetime`, `Media:RateLimitPermits`, `Media:RateLimitWindow` | Media addresses: the key they are sealed with (32 random bytes, base64), how long one works (15 minutes), how many new ones one client address may ask for (60 a minute) |
| `App:ShowcasePlaylistId` | The playlist the public page shows as its showcase (`showcasePlaylistId` of the public configuration); not set: none. Not required to exist at start |
| `PublicPage:Origin`, `…:DefaultImagePath`, `…:Keep` | The public page's address (the previews of its addresses and its sitemap are written for it), its own picture (`/icon-512.png`), how long a preview not of a link is kept (5 minutes) |
| `Illustrations:DailyImageLimit` | How many pictures a person may have drawn in a day (42) |
| `Jobs:Concurrency`, `Jobs:Lease`, `Jobs:MaxAttempts`, `Jobs:Poll`, `Jobs:RecentTime`, `Jobs:ListLimit`, `Jobs:FileLifetime`, `Jobs:Retention` | The background jobs (`Common/Jobs/JobsOptions.cs`); each has a default |

The settings are checked at start; a wrong one stops the app with the reason.

## Media

Sound and video are played from short-lived addresses (`/api/media/…`, with Range), asked for with
`POST /api/media/tracks` (a music service's track) or `POST /api/media/chords` (the recording, separate instruments and
notes file of an item of chords; a file of the item is read from its source through the index, for the person the address
was made for, and the address carries that sealed). Each answer gives the address(es) and their `expiresAt`: ask for new ones before then
(a player that seeks later than that gets 410 `media_expired`; ask again and go on from the same place).

## Links and the public page

`POST /api/share {kind, …}` makes a link; with `kind: chords, carriesAccess: true` a link that shows the chords to whoever
holds it as they are open to the sharer (by the sharer's custom keys, never the account; only the keys the sharer still
has count when it is opened). `GET /api/share/{token}` opens a link; for a link of chords `GET /api/share/{token}/chords?kinds=…`
and `POST /api/share/{token}/media` give the content and the media; `DELETE /api/share/{token}` removes a link that
carries its sharer's access.

The public page (`HarmonyDB.Shelf.Web`, `public.html`, service `public-web`) is one build; its nginx puts into its head,
for each address, the preview written by `GET /api/public/preview/<the address>` (no answer: the page keeps its own
head), and serves the sitemap from `GET /api/public/sitemap`.

## Signing in

The areas `auth` and `me`. Providers (`Areas/Auth/Services/Providers`), one class each behind `ISignInProvider`:
`telegram` and `google` (OpenID Connect on one shared class, `OidcSignInProvider`: code + PKCE, the id token
validated; Google with a nonce and `openid email profile`; Telegram with `openid profile`, its subject the numeric
Telegram user id), `yandex` (code + PKCE, the identity from its user info), `dev` (a named test account; Development
only).

The frontend's part:

1. `POST /api/auth/flows {provider, purpose: signIn | link, returnPath}` → `{state, redirectUrl}`. With a
   `redirectUrl`, go there; the browser comes back to `returnPath?auth=<outcome or error code>&auth_flow=<state>`.
   Without one (dev), post `{"answer": {"name": …}}` to `/api/auth/flows/{state}/complete`. To walk the stand as an
   imported person, post `{"answer": {"accountId": …}}` instead: the dev sign-in signs in as that account and changes
   nothing in it (no sign-in row is added). An account takes its name and picture only from the sign-in it was made with; other sign-ins never rename
   it. Starting and finishing are rate-limited
   per client address (429 `too_many_requests`).
2. The outcome `mergeNeeded` (when adding a sign-in that has its own account): show `GET /api/auth/flows/{state}`
   and on consent `POST /api/auth/flows/{state}/merge`. It needs both sign-ins made within `FreshSignIn`;
   otherwise `fresh_sign_in_required`: sign in again, then add the other sign-in again. Adding a sign-in
   (`purpose: link`) and removing one (`DELETE /api/me/logins/{id}`) need a recent sign-in too: on
   `fresh_sign_in_required`, sign in again with the account's provider and repeat.
3. `POST /api/auth/sign-out`. Who the browser is: `GET /api/auth/session`.

**Sessions**: an opaque token in the cookie `__Host-shelf-session` (HttpOnly, Secure, SameSite=Lax), kept hashed,
rotated at every sign-in; listed and revoked in `me/sessions`. On the local stack's http origin the cookies drop the
`__Host-` prefix and Secure (browsers keep neither on http); nothing else changes.

**CSRF** (signed double-submit cookie): send the value of the cookie `__Host-shelf-csrf` (`shelf-csrf` on the local
stack's http) as the header `X-CSRF-Token` with every POST, PUT, PATCH and DELETE. Every answer of `/api` gives that cookie anew when it no longer fits (after signing
in or out). A refusal is 403 `csrf_failed` with a fresh cookie: repeat the request once.

**The identity, in another area.** The browser's session is the default authentication; an endpoint takes
`ClaimsPrincipal user`:

```csharp
group.MapPost("/{id}/marks", (ClaimsPrincipal user, Guid id, MarksService marks) =>
        marks.AddAsync(user.GetAccountId()!.Value, id))   // the account; null for a guest
    .RequireSignIn();                                       // a guest gets 401 sign_in_required
group.MapGet("/{id}/book", …).RequirePermission(BuiltInPermissions.Pdf);
// elsewhere: permissions.Has(user, …), permissions.CustomKeys(user) → Caller.Keys
```

Every POST, PUT, PATCH and DELETE under `/api` says who may call it — `.RequireSignIn()`, `.RequirePermission(…)`, an
internal scope, or `.AllowGuests()` when it is open to guests on purpose; a test fails one that says none.

A guest's writing action answers 401 `sign_in_required`: the frontend offers sign-in and repeats the action. A person's
own music account, to call the music service as them: `MusicLinkStore.GetActingAsync(accountId, service)`. The music
service's client is `Clients/Audio/AudioClient` (add its calls there). A link is removed (replaced, unlinked, merged
away, deleted with its profile) after the music service has purged its subject (`AudioClient.PurgeLinksAsync`), asked
before the shelf's own change and outside its transaction: when it does not answer, the act answers 503 `unavailable`
and nothing is changed. What the link brings in between the purge and its removal stays at the music service. Deleting
a profile also asks the own chords' store first, after the music service: when the store does not answer, only the
music service's copies are gone, and deleting again goes on. What a person pastes as a music credential may be at most 48 KiB; more answers 400
`music_credentials_too_large` (they copied more than the request's headers).

**Internal**: `GET /internal/accounts/{accountId}/logins/{provider}` → `{subjects}` (scope `shelf.accounts`).

## Background jobs

Work a person starts that takes long (illustrations, a book, an import, a playlist's sync) is a job (`Common/Jobs`):
kept in `jobs`, run by a worker in the app, seen by the person at `GET /api/jobs` (queued, running, finished lately)
and `GET /api/jobs/{id}`, its file at `GET /api/jobs/{id}/file`.

- A kind of job is a class implementing `IJobHandler`, registered by the area that starts it:
  `services.AddJobHandler<MyJob>(configuration, "my_kind")`. It is made in a scope of its own for each run.
- Start it with `JobQueue.EnqueueAsync(accountId, "my_kind", subjectId, input)`; `JobQueue.FindActiveAsync(kind,
  subjectId)` finds one queued or running for a thing.
- In the handler: `run.Input<T>()`, `run.ReportAsync(stage, progress)` (a stage is a code the frontend words), and the
  end: `return new JobCompletion(result, file)`, or `throw new JobFailedException(code)`.
- A job stopped with the app goes back to the queue; one whose app died is taken again when its lease runs out, up to
  `Jobs:MaxAttempts` starts, then fails with `job_interrupted`. A run may so be repeated: what a handler does must bear
  it. The person's custom keys, when the job reads the sources, go in its input at the start.

## Working beside each other

Several developers work in this one working tree at once. Build and test in Docker from a copy of the tree, so that
nobody's `bin`/`obj` collide with another's, and copy back only what a tool generated (a migration, `openapi.json`):

```
docker run --rm -v "<the tree>:/src" -v harmonydb-nuget:/root/.nuget/packages mcr.microsoft.com/dotnet/sdk:8.0 bash -c '
  mkdir /work && cd /src && tar --exclude=./.git --exclude="*/bin" --exclude="*/obj" --exclude="*/node_modules" -cf - . | tar -xf - -C /work
  cd /work && dotnet test HarmonyDB.Shelf/HarmonyDB.Shelf.Tests'
```

## Running

The shelf runs with the other services from `docker/docker-compose.yml`, on their one PostgreSQL. Its database and the music service's, each with
its own role, are made once by hand with `OneShelf.Common/OneShelf.Common.Database/CreateServiceDatabase.sql`
(`name=shelf`, `name=audio`); `shelf-api` then starts and migrates.

```
cp docker/.env.example docker/.env    # and fill it in
docker compose -f docker/docker-compose.yml up -d --build shelf-api shelf-web public-web audio-api
curl localhost:14101/api/app/health
```

## Testing

```
dotnet test HarmonyDB.Shelf/HarmonyDB.Shelf.Tests
```

Most tests need no database. Those of sessions, sign-ins and merges need a PostgreSQL (each test class makes a
database of its own and drops it): give a server in `SHELF_TEST_DATABASE`, or they are reported skipped. In Docker:

```
docker network create shelf-tests
docker run -d --rm --name shelf-tests-pg --network shelf-tests -e POSTGRES_PASSWORD=test postgres:17
# then the command of "Working beside each other" with
#   --network shelf-tests -e "SHELF_TEST_DATABASE=Host=shelf-tests-pg;Username=postgres;Password=test"
docker rm -f shelf-tests-pg
```

After adding or changing endpoints `OpenApiDocumentTests` goes red: run the tests once with
`SHELF_WRITE_OPENAPI=1`, which writes `HarmonyDB.Shelf.Model/openapi.json` anew (in a copy of the tree, copy that file
back), and look at its diff.
