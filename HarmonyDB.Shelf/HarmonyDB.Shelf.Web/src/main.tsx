// first of all: how the app may run here — on its own, as the guest's preview, or not at all (framed otherwise); it
// changes what the rest may touch
import { framedRefused, isGuestPreview } from './app/guestMode';
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { QueryClientProvider } from '@tanstack/react-query';
import { RouterProvider } from 'react-router';
import { I18nextProvider } from 'react-i18next';
import { i18n } from './i18n';
import { queryClient } from './data/queryClient';
import { finishSignInFromAddress } from './data/session';
import { installTriggers } from './local';
import { installZoom } from './app/zoom';
import { router } from './app/routes';
import { sendCsrf } from './api/client';
import { installWorker } from './app/pwa';
import './styles/fonts.css';
import './styles/tokens.css';
import './styles/base.css';
import './styles/frame.css';
import './styles/lists.css';

const root = createRoot(document.getElementById('root')!);

if (framedRefused) {
    // framed by anything but the app's own guest preview: the app does not run — one plain line, nothing else
    root.render(<p className="framed">{i18n.t('common.framed')}</p>);
} else {
    // a guest's preview sends no CSRF token: it has none of its own and must not use the person's
    if (!isGuestPreview) sendCsrf();
    installZoom();
    installTriggers();
    void finishSignInFromAddress();

    installWorker();

    root.render(
        <StrictMode>
            <I18nextProvider i18n={i18n}>
                <QueryClientProvider client={queryClient}>
                    <RouterProvider router={router} />
                </QueryClientProvider>
            </I18nextProvider>
        </StrictMode>,
    );
}
