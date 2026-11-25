// The installed app: its worker (a build only), and a new version told to the person — the worker takes the open page
// over as soon as it has the new version, and the page offers to reload (nothing changes under one's fingers).

import { i18n } from '../i18n';
import { toast } from './toasts';

export function installWorker(): void {
    if (!import.meta.env.PROD || !('serviceWorker' in navigator)) return;
    const hadOne = !!navigator.serviceWorker.controller;
    navigator.serviceWorker.addEventListener('controllerchange', () => {
        // the first install takes an empty page over: nothing to tell
        if (!hadOne) return;
        toast(i18n.t('pwa.updated'), { action: { label: i18n.t('pwa.reload'), run: () => location.reload() }, stays: true });
    });
    void navigator.serviceWorker.register('/service-worker.js').then(registration => {
        // a page kept open for days looks for a new version now and then
        setInterval(() => void registration.update(), 60 * 60 * 1000);
    });
}
