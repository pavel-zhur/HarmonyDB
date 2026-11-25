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
| `../docker/wave2` | The compose project `harmonydb-wave2` |

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
  by its link. `share_links` are for a song or chords.
- The source contract's models are stored (`analysis_contents`) and sent with `SourceV2Json.Options` only.
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
| `SignIn:Telegram`, `SignIn:Google`, `SignIn:Yandex`, `SignIn:Dev` | Which sign-in providers are on (`Dev` only in the Development environment) |

The settings are checked at start; a wrong one stops the app with the reason.

## Working beside each other

Several developers work in this one working tree at once. Build and test in Docker from a copy of the tree, so that
nobody's `bin`/`obj` collide with another's, and copy back only what a tool generated (a migration, `openapi.json`):

```
docker run --rm -v "<the tree>:/src" -v harmonydb-wave2-nuget:/root/.nuget/packages mcr.microsoft.com/dotnet/sdk:8.0 bash -c '
  mkdir /work && cd /src && tar --exclude=./.git --exclude="*/bin" --exclude="*/obj" --exclude="*/node_modules" -cf - . | tar -xf - -C /work
  cd /work && dotnet test HarmonyDB.Shelf/HarmonyDB.Shelf.Tests'
```

## Running

The compose project runs beside the shared stack and uses its PostgreSQL:

```
cp docker/wave2/.env.example docker/wave2/.env    # and fill it in
docker compose -f docker/wave2/docker-compose.yml up -d --build
curl localhost:14101/api/app/health
```

`db-init` creates the databases `shelf` and `audio` with their roles; `shelf-api` then starts and migrates.

## Testing

```
dotnet test HarmonyDB.Shelf/HarmonyDB.Shelf.Tests
```

The tests need no database. After adding or changing endpoints `OpenApiDocumentTests` goes red: run the tests once with
`SHELF_WRITE_OPENAPI=1`, which writes `HarmonyDB.Shelf.Model/openapi.json` anew (in a copy of the tree, copy that file
back), and look at its diff.
