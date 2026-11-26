# HarmonyDB Shelf — the web app

The new shelf's frontend: TypeScript, React 18, Vite, React Router, TanStack Query, i18next; an installable PWA.
The mock-up `OneShelf.Frontend/OneShelf.Frontend.WebTs.v3` (`src.html` and its `docs/`) gives the direction and the
look (its first style, light and dark), not a law: it was drawn by a designer who knew the application only roughly,
and has its own mistakes. In its `docs`, the tables of the owner's own corrections are his words and hold; the rest is
weighed by common sense against how the application really works.

## Layout

```
src/
  main.tsx            start: i18n, queries, router, the two-finger zoom, the service worker (app/pwa.ts)
  api/                the browser API's client: schema.d.ts (generated), client.ts (fetch, CSRF, call()), errors.ts
  data/               reading and changing through the API: one file per subject (session, settings, playlists, songs)
                      actions.ts — every writing call, once; signInOffer.ts — a guest's action → sign in → done
  local/              local-first small changes (marks, keys, notes, opened): index.ts is its whole surface
  app/                the frame: Frame.tsx (header, side menu / tabs), routes.tsx (every address), nav.ts (menus),
                      panes.tsx (<Page>, <ListWithSong>), appearance.ts + zoom.ts (theme, sizes), toasts.ts
  kit/                the design's pieces: Icon, controls (Switch, Seg, Stepper, Chip, Soon, SearchField, Avatar,
                      Confirm), Popover (+MenuItem), Dialog, Toaster, SongRow, Qr, LinkBox
  screens/<area>/     the screens, each area with its own <area>.css: home, library, lists (the list of songs,
                      shared), playlists, recent, more, profile, signin, song (the song pane: unit 14's), share
  i18n/               en/<area>.ts, ru/<area>.ts — every text, one file per area, joined in en/index.ts, ru/index.ts;
                      index.ts — the language, dates
  styles/             fonts.css (kept with the app), tokens.css (colours, fonts), base.css (the kit), frame.css,
                      lists.css (lists of songs and playlists) — shared; an area's own styles are in its folder.
                      Class names of these areas carry a prefix, so that a short name of one does not style another:
                      an- (analysis), ls- (the lead sheet), rl- (the notes), pl- (player), mu- (screens/music),
                      rec- (screens/recordings)
  analysis/           the views of an analysis (tabs above its chords): time line, grid, lead sheet (views/lead),
                      iReal Pro, score, notes (views/roll); loaded when an analysis is opened
  player/             the player of a song's page and of a track (controller.ts, PlayerBar.tsx) and its sound
                      (player/sound: the transport, the tempo's stretcher, the mixer); the sound loads on first use
  testing/            what the tests need of a browser under node
  lib/                (to come, unit 14) the old frontend's src/lib copied as it is: chord sheet, transposition, analysis…
build/                the service worker (copied from the old frontend) and its build step
docker/               the image: the build, served by nginx (security headers, CSP); /api, /mcp, /oauth and /.well-known
                      go to shelf-api — nothing of the app lives under them
```

## Running it and looking

On the stand (compose project `harmonydb-wave2`, file `docker/wave2/docker-compose.yml`), the app is the service
`shelf-web` at **http://localhost:14100** (its API too: the backend's cookies are for that origin):

```
docker compose -f docker/wave2/docker-compose.yml up -d --build --no-deps shelf-web
```

While working, the dev server with hot reload, the API through the stand:

```
npm ci
npm run dev -- --port 14191        # http://localhost:14191
npm run build                      # type check + build
npm test                           # node's test runner: i18n keys, the local-first store, the client
```

Sign in with the **dev** provider (on the local stand only): "Sign in", type a test person's name — a new one makes a
new empty account. To walk the stand as an imported person (real playlists and marks), type that account's id (a uuid:
the admin site's people page, port 14104, or the `accounts` table) in place of a name: the dev provider signs in as
that account and changes nothing in it. Never link a dev name to an account by hand. The template playlist
(~600 songs) and "HarmonyDB AI" are public: open them from the guest's home or /playlists/public.

**Searching may reach outside the stand.** Test the search as a guest or as a new test person; never script searches,
and keep any look that goes out to one.
The same for opening chords never fetched and resolving new links: use what is stored.

Look at every screen at a phone's width (390) and wide (1440), light and dark, beside the same screen of the mock-up.

## A new screen

1. A component in `screens/<area>/`, wrapped in `<Page>` (one column) or `<ListWithSong>` (a list with its song).
2. Its address in `app/routes.tsx` (wrap it in `<SignedIn>` if it needs a profile).
3. If it belongs in a menu: a line in `app/nav.ts`.
4. Its texts in its area's files `i18n/en/<area>.ts` and `i18n/ru/<area>.ts` (a new area: a file in each, and a line in
   each `index.ts`); the test fails if one language has a key the other has not.
5. Its styles: the kit's classes first; its own in `screens/<area>/<area>.css`, imported by its screen, with the
   tokens only — no colour of its own.
6. Its address is never under `/api`, `/mcp`, `/oauth` or `/.well-known` (the server's; nginx and the worker pass
   them on).

```tsx
export function TunerScreen() {
    const { t } = useTranslation();
    return <Page><h1>{t('tools.tuner')}</h1>…</Page>;
}
```

## The kit, in short

- Quiet everywhere: `.btn` (grey), `.btn.pri` (accent text on a soft accent); the one solid `.btn.solid` lives in
  dialogs. `.btn.dan` for dangerous steps, asked again with `<Confirm>` in place.
- Lists are lines with thin dividers (`.lines`, `.menu`, `.setrow`), no boxes. Section labels are `<h2>` (small caps);
  no big headings (`h1` is 22px).
- `<Chip>` + `<Popover>` + `<MenuItem>` for filters and choices; `<Seg>` for two or three choices; `<Switch>` for on/off;
  `<Stepper>` for a size.
- `<Soon/>` marks what the README calls "soon": shown, does nothing.
- `toast(text, { undo })` after a change — "Undo" for a few seconds.
- `<SongRow>`: my marks · artist — title · sources' icons · others' marks with how many. Sources are shown as the server
  gives them for this person (`source.title`, `source.iconUrl`): the app knows no source by name.

## The data layer

- `api/schema.d.ts` is generated from `HarmonyDB.Shelf.Model/openapi.json`. When the backend adds or changes endpoints:
  regenerate `openapi.json` (backend README, "Testing": the tests with `SHELF_WRITE_OPENAPI=1`, in Docker from a copy
  of the tree, copy the file back), then `npm run api`, and commit both.
- Read: `call(api.GET('/api/…', { params }))` in a `useQuery`, in `data/<subject>.ts`. `call()` gives the data or
  throws `ApiError` (`status`, `code`, `details`); `errorText(error)` words the code (`i18n errors.<code>`) — the server
  never sends text. A new error code of the backend gets its words in both languages.
- Lists of songs are read in pages (`useSongList`, 200 a page, more as it is scrolled) under the key `['songs', …]`.
- Write: an `action(name, what, run, { done, undo, invalidate })` in `data/<subject>.ts` (see `data/actions.ts`): the
  CSRF header is put on by the client; a guest gets the sign-in offer (`what` says what for: `signIn.what.<what>`) and
  the action is done after signing in — also after a provider's round trip (it is remembered by `name`); errors are
  said in a toast; `done` + `undo` give the toast with "Undo" (the API's undo token).

```ts
export const leavePlaylist = action('leavePlaylist', 'change',
    (id: string) => call(api.POST('/api/playlists/{id}/leave', { params: { path: { id } } })),
    { done: () => t('playlist.left'), undo: r => r.undo, invalidate: (_, id) => [['playlists'], ['playlist', id]] });

const leave = useMutation({ mutationFn: leavePlaylist });   // in the component, for the busy state
```

## Local-first small changes (`local/`)

Marks, keys ("mine" and "here"), notes and "opened" are seen at once, kept in a queue in the browser (per account, it
survives a closed tab and a lost connection), sent to `/api/sync` in a batch a moment later, and the answer is
merged: the server's state of my marks/keys/notes/recent since the last watermark, the results one by one. A change
answered `stale` (made offline before a newer change elsewhere) just leaves the queue: the newer state is what is
seen. Every change but "opened" shows "Undo": before sending it leaves the queue; after, the server's undo token
undoes it. A guest's change offers signing in and is made after it (an action waits for its sign-in a quarter of an
hour at most; signing in from the header forgets it).

One person, one state: the store follows the session (signing in and out attach it; the frame attaches it at
start); every batch names its person, and the server refuses a batch of another than the session's (409
`wrong_account`: signed out and in as someone else in another tab) — then the store follows who the browser is now and
says so. Signing out, and any attaching, keeps only the unsent changes of everyone else this browser held, not their
marks, keys, notes or recent. Two tabs of one person share the state
through localStorage: a change is merged into what the other tab wrote (the queues joined), and a tab takes in the
other's changes as they come.

```ts
import { local, useMyMarks, useMyKey, useMyNote, useSyncStatus } from '../../local';

local.mark(chordsId, 'heart_red', true);        // put (false: take off)
local.key(chordsId, 2);                         // mine; null forgets
local.key(chordsId, 5, playlistId);             // here
local.note({ chords: chordsId }, 'text');       // personal; { songId } — in the song's playlist; null removes
local.opened({ chords: chordsId, songId });     // for the recent

const marks = useMyMarks(chordsId);             // the server's word + what is not sent yet
```

The pure part (the queue on top of the base, the merges) is `local/state.ts`, tested in `local/state.test.ts`; the
store against a server the test answers in `local/store.test.ts`. The
kinds of marks and their signs come from the server's public configuration: `data/marks.ts`.

## i18n

- Every text a person sees is a key: `t('library.whole')`. No text in components, no text from the server (codes).
- Plurals: the key with `_one`/`_other` (Russian also `_few`/`_many`), called with `{ count }`.
- The language: the person's setting, else the browser's (a guest), else English. Dates and "how long ago" by `Intl`
  (`formatDate`, `formatAgo`).
- The voice: plain and short; never "we" — impersonal, or about the person.
- System playlists are named by the app (`playlistTitle()`), not by their stored title.

## Sizes, theme, the frame

- Two sizes, both in the profile's settings: two fingers on the chords (an element marked `data-chords`) change the
  chords' size (`--chords-scale`, for the chord sheet to use), anywhere else the whole interface (`--ui-scale`, CSS
  zoom of the app). The layout re-flows: nothing ever scrolls sideways. Positions inside the app (a popover) are in
  its zoomed pixels: see `kit/Popover.tsx`.
- Phone: the header scrolls away with the page; the bottom tabs. Wide (≥ 960px): the header stays, the side menu, a
  list and its song side by side; the border is dragged (remembered in this browser; a double click restores it).
- `<ListWithSong>`: on a phone the song covers the list, and closing it brings the list back where it was; the row one
  came back from is lit (`app/lastOpened.ts`). A list's search, sort and filters live in its address.
- The sync button spins while the local-first store exchanges with the server, and leads to the library, newest
  chords first.
- A new build of the app: the worker keeps the current build only and takes the open page over; a page of the build
  before that asks for a part no longer there loads the new build, once (`app/pwa.ts`, `build/service-worker.js`).
  The fonts are kept with the app (`styles/fonts.css`), so it is whole offline.

## Libraries and fonts kept with the app

Besides the frameworks in `package.json`:

- `@soundtouchjs/core` (MPL-2.0) — the time-stretch under the tempo (`player/sound/stretchPipe.ts`). That file
  changes the library's overlap search, so it is under the MPL-2.0 itself, as its first lines say.
- `opensheetmusicdisplay` (BSD-3-Clause) — engraves the score (`analysis/views/ScoreView.tsx`), as it is.
- Bravura (SIL Open Font License 1.1) — the music font of the lead sheet, with its licence beside it
  (`analysis/views/lead/OFL.txt`).

## Security

nginx (`docker/security.conf`) gives every answer `X-Frame-Options: DENY`, a Content-Security-Policy (only the app's own
scripts, styles, fonts and connections; pictures also from https; `frame-ancestors 'none'`), `nosniff` and a
referrer policy. No inline script and no style element in the page; inline style attributes are allowed
(`style-src-attr 'unsafe-inline'`: they cannot run script; the sheet and the score write them). A new need (a picture
from elsewhere, a player) is a change of the policy there, said in the review.

## The consent page of the MCP's sign-in (a later unit)

`/connect` (its place is in `app/routes.tsx`): the server's `/oauth/authorize` sends the person here to choose what a
client may do. Beside the client's name it shows the host of the redirect address and the host of the client id, and
warns of a client that is local only: a client may call itself by a well-known assistant's name, and only the host
tells them apart.

## The public page

A second entry of this build (`public.html`, `src/public/`), served by its own container (`public-web`, the image's
target `public`, **http://localhost:14109** on the stand): the showcase — the open playlist of analyses, each viewed
there with the app's views and player — and whatever a link leads to, for one who has never seen the app. It shares
`src/` (the views, the player, the chord sheet, the kit, the client, the i18n) but not the app's frame, worker,
manifest, sign-in or local store; it reads the API always as a guest through its own nginx (`docker/public.nginx.conf`:
`/api/` goes on to shelf-api without the browser's cookies and credentials, and no cookie comes back — what a guest may
do is the API's to decide; tested when the image is built, `docker/public-web.test.sh`; the same content policy).

- Addresses: `/` the showcase, `/songs/:songId` a song of it, `/chords/:chordsId` chords by id, `/s/:token` a share
  link (chords, a song, a playlist), `/s/:token/:songId` a song of a shared playlist.
- Search engines see the showcase only: `robots.txt` (`docker/public-robots.txt`) and `X-Robots-Tag: noindex` on every
  other address.
- What it is told comes from its container (`docker/public-site.sh` writes it into the page when it starts):
  `SHELF_SITE_ORIGIN` (its own address: the messengers' preview picture), `SHELF_APP_ORIGIN` (the app, where it
  invites). Read in `src/public/site.ts`.

```
docker compose -f docker/wave2/docker-compose.yml up -d --build --no-deps public-web
```
