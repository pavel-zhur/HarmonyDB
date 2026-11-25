// Every screen has its address — never under /api, /mcp, /oauth or /.well-known: those are the server's.
// A new screen: a route here (and, if it belongs in the menu, a line in nav.ts).
// Screens a person needs a profile for are wrapped in <SignedIn>: a guest gets the offer to sign in in place.

import type { ReactNode } from 'react';
import { Navigate, createBrowserRouter, useLocation } from 'react-router';
import { useTranslation } from 'react-i18next';
import { Frame } from './Frame';
import { Page } from './panes';
import { useSession } from '../data/session';
import { offerSignIn } from '../data/signInOffer';
import { errorText } from '../api/errors';
import { HomeScreen } from '../screens/home/HomeScreen';
import { LibraryScreen } from '../screens/library/LibraryScreen';
import { RecentScreen } from '../screens/recent/RecentScreen';
import { PlaylistsScreen } from '../screens/playlists/PlaylistsScreen';
import { PlaylistScreen } from '../screens/playlists/PlaylistScreen';
import { PlaylistSettingsScreen } from '../screens/playlists/PlaylistSettingsScreen';
import { PublicPlaylistsScreen } from '../screens/playlists/PublicPlaylistsScreen';
import { ImportPlaylistScreen } from '../screens/playlists/ImportPlaylistScreen';
import { InvitationScreen } from '../screens/playlists/InvitationScreen';
import { MoreScreen } from '../screens/more/MoreScreen';
import { ProfileScreen } from '../screens/profile/ProfileScreen';
import { ShareScreen } from '../screens/share/ShareScreen';
import { ChordsScreen } from '../screens/song/ChordsScreen';
import { musicRoutes } from '../screens/music/routes';
import { DeletedScreen } from '../screens/profile/DeletedScreen';
import { AiScreen } from '../screens/ai/AiScreen';
import { ConsentScreen } from '../screens/ai/ConsentScreen';
import { EditorScreen } from '../screens/editor/EditorScreen';
import { ImportScreen } from '../screens/importing/ImportScreen';
import { BookScreen } from '../screens/book/BookScreen';
import { AboutScreen } from '../screens/about/AboutScreen';
import { VersionsScreen } from '../screens/about/VersionsScreen';
import { TunerScreen } from '../screens/tuner/TunerScreen';
import { FingeringsScreen } from '../screens/fingerings/FingeringsScreen';
import { OfflineScreen } from '../screens/offline/OfflineScreen';
import { IllustrationsScreen } from '../screens/illustrations/IllustrationsScreen';

function SignedIn({ children }: { children: ReactNode }) {
    const { t } = useTranslation();
    const { known, signedIn, error } = useSession();
    const { pathname } = useLocation();
    if (!known) return error ? <Page><p className="failed">{errorText(error)}</p></Page> : null;
    if (signedIn) return <>{children}</>;
    return (
        <Page>
            <div className="locked" key={pathname}>
                <h1>{t('signIn.title')}</h1>
                <p className="muted">{t('signIn.why', { what: t('signIn.what.open') })}</p>
                <button type="button" className="btn pri" onClick={() => offerSignIn('open')}>{t('nav.signIn')}</button>
            </div>
        </Page>
    );
}

export const router = createBrowserRouter([
    {
        element: <Frame />,
        children: [
            { path: '/', element: <HomeScreen /> },

            // the library, a song or chords open beside it
            { path: '/library', element: <LibraryScreen /> },
            { path: '/library/songs/:playlistId/:songId', element: <LibraryScreen /> },
            { path: '/library/chords/:chordsId', element: <LibraryScreen /> },
            { path: '/recent', element: <SignedIn><RecentScreen /></SignedIn> },

            // playlists
            { path: '/playlists', element: <SignedIn><PlaylistsScreen /></SignedIn> },
            { path: '/playlists/public', element: <PublicPlaylistsScreen /> },
            { path: '/playlists/import', element: <SignedIn><ImportPlaylistScreen /></SignedIn> },
            { path: '/playlists/:id', element: <PlaylistScreen /> },
            { path: '/playlists/:id/songs/:songId', element: <PlaylistScreen /> },
            { path: '/playlists/:id/settings', element: <SignedIn><PlaylistSettingsScreen /></SignedIn> },
            { path: '/invitations/:token', element: <InvitationScreen /> },

            // links; chords alone
            { path: '/s/:token', element: <ShareScreen /> },
            { path: '/chords/:chordsId', element: <ChordsScreen /> },

            // the music: artists, albums, tracks, genres and moods, the chart, radio, a linked account's lists
            ...musicRoutes(page => <SignedIn>{page}</SignedIn>),

            { path: '/more', element: <MoreScreen /> },
            { path: '/profile', element: <SignedIn><ProfileScreen /></SignedIn> },
            { path: '/profile/deleted', element: <DeletedScreen /> },

            // the tools and the rest
            { path: '/import', element: <SignedIn><ImportScreen /></SignedIn> },
            { path: '/editor', element: <SignedIn><EditorScreen /></SignedIn> },
            { path: '/illustrations', element: <SignedIn><IllustrationsScreen /></SignedIn> },
            { path: '/fingerings', element: <FingeringsScreen /> },
            { path: '/tuner', element: <TunerScreen /> },
            { path: '/ai', element: <SignedIn><AiScreen /></SignedIn> },
            { path: '/offline', element: <SignedIn><OfflineScreen /></SignedIn> },
            { path: '/about', element: <AboutScreen /> },
            { path: '/versions', element: <VersionsScreen /> },
            { path: '/book', element: <SignedIn><BookScreen /></SignedIn> },
            // the consent page of the MCP's sign-in (/oauth/authorize sends the person here): what a client may do.
            // It shows, beside the client's name, the host of its redirect address and of its client id, and warns of a
            // client that is local only — a client may call itself by a well-known assistant's name; only the host
            // tells them apart.
            { path: '/connect', element: <ConsentScreen /> },

            { path: '*', element: <Navigate to="/" replace /> },
        ],
    },
]);
