// The addresses of the music pages, /music/<service>/…: joined to the app's routes (app/routes.tsx). A linked account's
// own lists need a profile.

import { lazy, Suspense, type ReactElement } from 'react';
import type { RouteObject } from 'react-router';
import { MusicPage, type MusicPageProps } from './parts';
import { ArtistScreen } from './ArtistScreen';
import { AlbumScreen } from './AlbumScreen';
import { CategoriesScreen, CategoryScreen } from './CategoryScreen';
import {
    ArtistRadioScreen, ChartScreen, MyHistoryScreen, MyLikesScreen, MyServicePlaylistScreen, RadioScreen, ServicePlaylistScreen, SimilarScreen,
} from './TrackListScreen';

// a track's page carries the player: loaded when it is opened
const LazyTrack = lazy(() => import('./TrackScreen').then(m => ({ default: m.TrackScreen })));

function TrackScreen(props: MusicPageProps) {
    return (
        <Suspense fallback={null}>
            <LazyTrack {...props} />
        </Suspense>
    );
}

export function musicRoutes(signedIn: (page: ReactElement) => ReactElement): RouteObject[] {
    return [
        { path: '/music/:service/artists/:id', element: <MusicPage page={ArtistScreen} /> },
        { path: '/music/:service/artists/:id/radio', element: <MusicPage page={ArtistRadioScreen} /> },
        { path: '/music/:service/albums/:id', element: <MusicPage page={AlbumScreen} /> },
        { path: '/music/:service/tracks/:id', element: <MusicPage page={TrackScreen} /> },
        { path: '/music/:service/tracks/:id/similar', element: <MusicPage page={SimilarScreen} /> },
        { path: '/music/:service/tracks/:id/radio', element: <MusicPage page={RadioScreen} /> },
        { path: '/music/:service/playlists/:id', element: <MusicPage page={ServicePlaylistScreen} /> },
        { path: '/music/:service/categories', element: <MusicPage page={CategoriesScreen} /> },
        { path: '/music/:service/categories/:id', element: <MusicPage page={CategoryScreen} /> },
        { path: '/music/:service/chart', element: <MusicPage page={ChartScreen} /> },
        { path: '/music/:service/me/playlists/:id', element: signedIn(<MusicPage page={MyServicePlaylistScreen} />) },
        { path: '/music/:service/me/likes', element: signedIn(<MusicPage page={MyLikesScreen} />) },
        { path: '/music/:service/me/history', element: signedIn(<MusicPage page={MyHistoryScreen} />) },
    ];
}
