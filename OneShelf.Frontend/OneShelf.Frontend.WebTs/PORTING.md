# OneShelf.Frontend.WebTs

The frontend site. It is a one-to-one port to TypeScript of the Blazor WebAssembly site `OneShelf.Frontend.Web`,
"the original" below, which it replaced. The original is no longer in the repository (it is in its history, up to
the commit that removed it); this file keeps what the port had to keep and where it departs.

The reason: the .NET runtime in the browser is some ten megabytes to download and seconds to start, and on some
phones it is painful. This project is meant to load at once and to be, for the person using it, the very same site.

## What "the same" means

- **Nothing changes for the user.** The same pages at the same addresses, the same look to the pixel, the same texts,
  the same behaviour — including the odd and the imperfect. What does not work in the original because of where it runs
  (the Telegram login on localhost, for example) does not work here in the same way, and starts to work the same way
  when the place is right.
- **The same data on the device.** A person who used the old site opens the new one and stays logged in, with the
  offline library, the unsent actions and the settings in place — and the other way round. So `localStorage` keys and
  values, the IndexedDB database with its stores and item shapes, are exactly the original's.
- **The same API.** The requests and responses are the original's, byte for byte in meaning: the same addresses,
  property names, enum values, null handling.
- **The original is the specification.** When in doubt, read the original and do what it does. Do not improve, tidy or
  fix behaviour. If something cannot be done the same way in a browser without .NET, write it down in "Departures" at
  the end of this file, with the reason, and bring it to Pavel.

The C# libraries the original used stay in .NET as they were. What the original alone knew about chords was moved
into them when it was removed: the guitar fingerings into a library of their own, the circle-of-fifths marking and
the showing of a loop into `HarmonyDB.Index.Analysis`, the rule of the "simpler" modes into
`HarmonyDB.Common.Representations.OneShelf`.

## How it is built

TypeScript (strict), React, Vite, React Router, Vitest. No other framework or state library.

The structure follows the original file by file, so that either could be read against the other:

| Original | Here |
|---|---|
| `OneShelf.Frontend.Web/Pages/X.razor` | `src/pages/X.tsx` |
| `OneShelf.Frontend.Web/Shared/X.razor` | `src/shared/X.tsx` |
| `OneShelf.Frontend.Web/Components/X.cs` | `src/components/X.tsx` |
| `OneShelf.Frontend.Web/Services/X.cs` | `src/services/X.ts` |
| `OneShelf.Frontend.Web/Models`, `IndexedDb`, `Interop` | `src/models`, `src/indexedDb`, `src/interop` |
| `X.razor.scss`, `X.razor.less` | beside the component, unchanged: `X.scoped.scss`, `X.scoped.less` |
| `wwwroot` (the theme, fonts, icons, images, `manifest.json`, …) | `public`, unchanged |
| `HarmonyDB.Index.Analysis` | `src/lib/analysis` |
| `HarmonyDB.Common.Representations.OneShelf`, `HarmonyDB.Common`, `OneShelf.Common` | `src/lib/representations`, `src/lib/common` |
| the API models and clients (`OneShelf.Frontend.Api.Model`, `…Api.Client`, `OneShelf.Collectives.Api.*`, `HarmonyDB.Source.Api.Model`, `OneShelf.Authorization.Api.Model`, …) | `src/lib/api` |
| the tests of the above (`HarmonyDB.Index.Analysis.Tests`, `OneShelf.Frontend.Web.Tests`) | `*.test.ts` beside the code |
| `Program.cs`, `App.razor` | `src/main.tsx`, `src/App.tsx` |
| what Blazor itself does (navigation, `PageTitle`, `FocusOnNavigate`, the error bar) | `src/framework` |
| the settings (`IConfiguration`, `IWebAssemblyHostEnvironment`) | `src/configuration.ts` |

Inside a library the folders and file names follow the C# namespaces and type names. A C# type keeps its name; a
member keeps its name in camelCase.

**Services.** A service of the original (registered in `Program.cs`) is a class here with the same members. The
singletons live in `src/services/registry.ts`, created in the order the original needs them; the scoped ones are
created the way the original scopes them. Where the original raises an event for components to re-render, the class
has the same event, and a component subscribes with a hook (`src/hooks`).

**Components.** A Razor component becomes a function component giving the same DOM: the same elements, classes,
attributes and order, because the theme's CSS and scripts depend on them. Blazor's lifecycle maps as: `OnInitialized(Async)`
→ an effect on mount; `OnParametersSet(Async)` → an effect on those props; `OnAfterRender(Async)` → an effect after
render (with the `firstRender` distinction kept); `StateHasChanged` → a state update; `Dispose` → the effect's cleanup.

**Scoped styles.** Blazor gives every element a component renders a marker attribute and rewrites the component's
`.razor.css` so that each rule applies to marked elements only (`::deep` reaches below). Here the same is done at build
time by a Vite plugin (`build/scopedStyles.ts`): a component imports its `X.scoped.scss`, and every element it renders
gets the marker. The style files are the original's, unchanged.

**The global styles** (`wwwroot/css/app.scss`) are compiled with the same sass as the original uses (1.56.1).

**JSON.** The original talks JSON through `System.Text.Json` and stores JSON on the device. The names, the casing, the
enum representation and what happens to nulls and unknown members are taken from the C# classes and the serializer
options in use at that place — check, do not assume.

**Numbers, strings, collections.** C# and JavaScript differ: integer division and overflow, `decimal`, string
comparison and ordering (ordinal vs culture), `Dictionary` ordering, `Distinct`/`GroupBy`/`OrderBy` stability, regular
expressions, `DateTime`. Where the original's result depends on these, the port gives the same result.

**Tests.** The C# tests are ported one for one and pass.

## Conventions that have settled

- **Scoped styles.** A component's own file has `import './X.scoped.scss';` (or `.less`) — one import, no bindings.
  Every lowercase JSX element written in that file then carries the component's marker; elements of child components,
  elements made with `createElement` and markup set through `dangerouslySetInnerHTML` do not (as with Razor and
  `MarkupString`).
- **Configuration.** `import { configuration, hostEnvironment } from './configuration'`: `configuration.get(key)`
  (`GetValue<string>`), `configuration.getBoolean(key)`, `configuration.getSection(name)`;
  `hostEnvironment.environment`.
- **Navigation and titles.** Plain `<a href>` as in the Razor markup; `navigationManager` from
  `src/framework/navigation`; `<PageTitle>` from `src/framework/PageTitle`. A page stays mounted when only the query
  changes, so what the original does in `OnParametersSet` reacts to the location.
- **Fire-and-forget.** Where C# writes `_ = SomethingAsync()`, the promise is caught: an unhandled rejection would
  be reported by the browser here, while the same failure in .NET is not seen.
- **C# types.** `List<T>` → array; `Dictionary` → `Map` in insertion order; a value tuple → an object with the C#
  element names (`{ x, i }`); `DateTime`, `Guid`, `Uri` and the other .NET behaviour the code relies on → `src/lib/common/dotnet`;
  `out` parameters → a holder object; C# overloads that cannot be told apart in TypeScript get distinct names, noted
  at the top of the file.
- **Models.** A model is a class whose fields match the wire, constructed from an object of initial values (required
  members required, C# defaults otherwise), with a static `json` contract. The API speaks camelCase
  (`ApiClientBase`); what the original stores on the device with the default serializer keeps the C# property names.
  `src/lib/common/dotnet/json` writes and reads either.

## Porting a page or a shared component

- **A component's parameters** are its props, with the Razor `[Parameter]` names in camelCase and the same types
  (`EventCallback<T>` → a function; `RenderFragment` → `ReactNode`; `ChildContent` → `children`). A component that
  others call through `@ref` exposes the same methods through a ref.
- **`@inject X`** → `import { services } from '../services/registry'` and `const x = services.x;` in the component
  body. Services are created when first asked for, as in the original, and some start working on creation — ask for
  them where and when the original injects them.
- **`X.Event += StateHasChanged`** → `useRenderOnEvent(services.x.event)`; **`X.Event += Handler`** →
  `useEvent(services.x.event, handler)`, or `useAsyncEvent` for `Func<…, Task>` events; **`StateHasChanged()`** →
  the function `useStateHasChanged()` gives. (`src/hooks`.)
- **`@inherits CollectionComponentBase`** → `useCollectionComponent(async () => { /* OnNewCollectionIndexReceived */ })`,
  which gives `collectionIndex`, `collectionIndexFailed`, `retryCollectionIndex`.
- **JS modules beside components** are served unchanged and imported at run time as in the original:
  `const m = await importJsModule<…>('./Shared/MainLayout.razor.js'); await invokeJs(m.MainLayoutRendered);`
  (`src/interop/jsRuntime`). Functions on `window` → `invokeWindowFunction(…)`. Where Razor passes `Receiver.Reference`,
  pass `services.receiver.reference`.
- **`NavLink`, `CollectionNavLink`, `VisitedNavLink`** are in `src/components`; any attribute they do not know goes to
  the `<a>`.
- **`SecondarySidebar`**: `MainLayout` provides `MainLayoutContext` (in `src/components/SecondarySidebar.tsx`) around
  the page.
- **Query parameters**: `getUriWithQueryParameter(s)` in `src/framework/navigationManagerExtensions`.
- **`[HumanTitle]` enums**: `getHumanTitle(…)` (`src/models/HumanTitle`).
- **`MarkupString`** → `dangerouslySetInnerHTML` with the same string.
- **Events.** The application's handlers run where and when Blazor runs them — from `document`, after the theme's
  own listeners, with the DOM updated before the listener returns (`src/framework/eventDelegator.ts`, set up with the
  root). `@onclick`/`@oninput` → `onClick`/`onInput`; `@onchange` on a `<select>` → `onChange`, on an `<input>` →
  `nativeChange` (React's `onChange` there is not the browser's change event).
- **Tasks the renderer awaits** — of an event handler, of `OnInitializedAsync`/`OnParametersSetAsync`/
  `OnAfterRenderAsync` — go through `errorHandledTask` (or `blazorEventHandler`): a failure is logged as the renderer
  logs it, a cancellation is not.
- **Text and whitespace.** Razor keeps the whitespace between elements as it is written; where it shows on the page
  (inline elements, `pre`), the JSX keeps it too.

## Offline and updates (the service worker)

The published site installs and works offline as the original does, and moves to a new version the same way.

- **The worker** is the original's `service-worker.published.js` (the Blazor template's), in `public/`, without the
  choice between the normal and the compatible build: it reads the one manifest `service-worker-assets.js`, caches every
  file listed in it at install (checked against the listed hash), activates at once, deletes the other
  `offline-cache-*` caches, answers from the cache and every navigation with `index.html`. `public/service-worker.js`
  is the original's do-nothing development worker, served by the development server.
- **The build** (`build/serviceWorker.ts`) writes, as the .NET build does, `service-worker-assets.js` (every published
  file with `sha256-` and the Base64 of its SHA-256, in the same text format, so that the scripts which change the
  hashes per deployment work on it as on the original's) and `service-worker.js` — the published worker with
  `/* Manifest version: … */` on its first line. The version is the start of the hash of all the files' hashes: any
  changed file makes the worker itself different, the browser installs it as an update on the minute check of
  `sw-registrator.js` (unchanged), and `UpdateAvailableDetector` shows "Есть обновление!" — the same moment and words.
- **`{#CACHE_VERSION#}`** is replaced before the build, as the release workflow does (in `index.html`, where
  `getWasmReleaseVersion` gives it to the error bar and the main page, and in the worker, where there is none), so it
  is part of the hashes and of the version.
- **A device with the original installed** has the original's worker at the same address. Its next minute check finds
  `/service-worker.js` changed and installs the new worker; it caches the new site, activates at once and deletes the
  original's `offline-cache-*` — as each update of the original did; the open page of the original shows "Есть
  обновление!", and the reload opens the new site, offline as well. It also deletes the cache the .NET runtime keeps
  for its own files (`dotnet-resources-/`, `blazor-resources-/` in .NET 7 and earlier), tens of megabytes nothing
  uses any more. `localStorage` and IndexedDB are not touched: the person stays logged in, with everything.
  The original's `localStorage.forceCompatMode`, if set, stays and means nothing.

## Building and deploying

- **Docker**: `docker/frontend-web` and the service `frontend-web` in `docker/docker-compose.yml` (port
  `FRONTEND_WEB_PORT`): `npm ci`, `{#CACHE_VERSION#}` from the build
  argument `CACHE_VERSION`, `npm run build` (which type-checks; the tests are not run in the image); at start
  `configure.sh` puts the given settings in place of `appsettings.json` and `appsettings.Development.json` and updates
  their hashes in the manifest, and with `ALLOW_BAD_HASH=true` adds `BadHash` and `/enter.html`. nginx serves the
  site's files compressed with gzip: the text files compressed at build time (`gzip_static`), the files changed at
  start compressed when sent; fonts and pictures as they are. The browser's calls to the API pass through the same
  address (`/frontend-api/`, `/collectives-api/`).
- **A release**: `npm ci`, the `{#CACHE_VERSION#}` replacement, `npm run build`; the site is `dist`. The per-deployment
  replacement of `appsettings.json` is done with the scripts in `post-publish` — the original's, run in the same way
  in the published folder (`substitite1.ps1` before the settings file is replaced, `substitite2.ps1` after), without
  the compatible build's manifest, which this site has not.

## Web workers

Later, and only where it changes nothing for the user: the heavy searches over the offline library.

## Departures

- **The loading screen.** There is no .NET runtime to download, so nothing drives the loading circle: it stays empty
  with "Загрузка" until the application replaces it (as the original's does before the runtime starts loading). The
  choice between the normal and the compatible build (SIMD detection, `_frameworkCompat`, `?forceCompatMode=1`,
  `localStorage.forceCompatMode`, `?verboseStart=1`) is gone: there is one build. With it go what offered the
  choice: the "Совместимость" section of the profile page and the error bar's "попробовать совместимость" button.
- **The service worker** reads one manifest (there is no `service-worker-assets-compat.js`, no `_frameworkCompat`, no
  `wasm-feature-detect`), and when it activates it also deletes the .NET runtime's own cache, which only the original
  used.
- **An exception while rendering or in an effect.** It is logged as Blazor's renderer logs it (a `crit:` line, an
  Application Insights exception, the error bar), but Blazor leaves the page as it was, while React has already
  removed what is below the nearest boundary (`ComponentErrorBoundary`): the page's body, or for the layout the whole
  page, until the next navigation.
- **An exception in an `async void` method.** The output is the original's (the runtime's "Unhandled Exception" lines
  and the error bar), but the original's .NET runtime then exits and the page stops responding; here the
  application goes on.
- **Blazor's debugger shortcut** (its `keydown` listener on `document` for Shift+Alt+D) is left out.
- **A render asked for after an await.** When a component calls `StateHasChanged()` after an await (or its task
  completes), Blazor writes the DOM at once; here React writes it in its next scheduler task, a moment later. During
  the dispatch of an event and in `Virtualize` the port does write at once, as those are where the order showed.
  Doing it everywhere needs to know whether React is rendering or running effects at that moment, which React does
  not tell.
- **How many times a component is rendered.** Blazor renders a component once for each `StateHasChanged()` of its
  owner; React joins renders asked for together into one. So `OnParametersSetAsync` runs fewer times here. It shows
  in one place: `ChordsView` records a visit each time it runs, and the original sends three or four visits of one
  song where the port sends one or two. The visited list on the page is the same (the server joins the same visits
  made within ten minutes); the rows the server keeps are fewer.
- **Exception texts in the console.** An exception is printed with its short name ("HttpRequestException: …", the
  original prints "System.Net.Http.HttpRequestException: …"), and a failed request's message is .NET's full text
  ("Response status code does not indicate success: 500 …") where the original, whose runtime has its message
  resources trimmed, prints the resource's key ("net_http_message_not_success_statuscode_reason, 500, …").
- **Attributes set after rendering.** Attributes the port sets through a ref (`stringAttributes`, `booleanAttribute`,
  inline `on*` handlers, `style` set by React) come after the element's other attributes and its scope marker;
  Blazor writes them in the markup's order, the scope marker last. The order is not seen and no CSS depends on it.
- **Numbers and characters JavaScript holds differently** (found by the comparison with .NET, `tools/dotnet-reference`):
  a `long` beyond ±2^53 read from JSON loses its last digits (the ids and dates in use are far below it);
  `char.IsUpper` follows the browser's Unicode version (it only camelCases the C# property names, which are ASCII);
  local times before the time zones existed (local mean time, before about 1893) get an offset rounded differently
  from .NET's, by up to a minute.
- **`Uri.Equals`** in .NET returns true at once when both Uris hold the very same string object; JavaScript cannot
  tell string objects apart, so here the comparison goes on. It ends the same for two Uris made alike from one string;
  only one string made into an absolute and a relative Uri would differ, which the site never does.
