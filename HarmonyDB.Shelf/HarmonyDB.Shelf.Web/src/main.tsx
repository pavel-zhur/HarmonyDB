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
import { installWorker } from './app/pwa';
import './styles/fonts.css';
import './styles/tokens.css';
import './styles/base.css';
import './styles/frame.css';
import './styles/lists.css';

installZoom();
installTriggers();
void finishSignInFromAddress();

installWorker();

createRoot(document.getElementById('root')!).render(
    <StrictMode>
        <I18nextProvider i18n={i18n}>
            <QueryClientProvider client={queryClient}>
                <RouterProvider router={router} />
            </QueryClientProvider>
        </I18nextProvider>
    </StrictMode>,
);
