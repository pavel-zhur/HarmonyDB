// The public page (public.html): the showcase — the open playlist of analyses, each viewed here with the app's views and
// player — and whatever a link leads to (chords, a song, a playlist), for one who has never seen the app. Not the app:
// no sign-in, no service worker, no install, no local store; the theme and the language are the device's.

import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { QueryClientProvider } from '@tanstack/react-query';
import { Navigate, RouterProvider, createBrowserRouter } from 'react-router';
import { I18nextProvider } from 'react-i18next';
import { i18n } from '../i18n';
import { queryClient } from '../data/queryClient';
import { PublicFrame } from './PublicFrame';
import { HomePage } from './HomePage';
import { ChordsPage, SharePage, SharedSongPage, ShowcaseSongPage } from './ChordsPage';
import '../styles/fonts.css';
import '../styles/tokens.css';
import '../styles/base.css';
import '../styles/frame.css';
import '../styles/lists.css';
import './public.css';

document.documentElement.lang = i18n.language;

const router = createBrowserRouter([
    {
        element: <PublicFrame />,
        children: [
            { path: '/', element: <HomePage /> },
            { path: '/songs/:songId', element: <ShowcaseSongPage /> },
            { path: '/s/:token', element: <SharePage /> },
            { path: '/s/:token/:songId', element: <SharedSongPage /> },
            { path: '/chords/:chordsId', element: <ChordsPage /> },
            { path: '*', element: <Navigate to="/" replace /> },
        ],
    },
]);

createRoot(document.getElementById('root')!).render(
    <StrictMode>
        <I18nextProvider i18n={i18n}>
            <QueryClientProvider client={queryClient}>
                <RouterProvider router={router} />
            </QueryClientProvider>
        </I18nextProvider>
    </StrictMode>,
);
