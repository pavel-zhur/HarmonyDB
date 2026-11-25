// The public page's frame: the app's black header (its logo, its name — to the main page; the way into the app) and
// the one pane under it — the app's own layout without its menus: on a phone one scrolling column that the header
// scrolls away with, wide the header standing over the pane.

import { Link, Outlet } from 'react-router';
import { useTranslation } from 'react-i18next';
import { Toaster } from '../kit/Toaster';
import { useWide } from '../app/layout';
import { useLogo } from '../app/logo';
import { appAddress } from './site';

export function PublicFrame() {
    const wide = useWide();
    return (
        <div className="app pub">
            {wide && <Header />}
            <div className="mid">
                {!wide && <Header />}
                <main className="main"><Outlet /></main>
            </div>
            <Toaster />
        </div>
    );
}

function Header() {
    const { t } = useTranslation();
    const logo = useLogo();
    return (
        <header className="top">
            <div className="notes" />
            <Link className="brand" to="/"><span className="logo" style={logo} /><span>{t('app.name')}</span></Link>
            <span className="sp" />
            <a className="pub-open" href={appAddress()}>{t('public.openApp')}</a>
        </header>
    );
}

/** The calm way into the app, at the end of a page. */
export function Invite({ to }: { to: string }) {
    const { t } = useTranslation();
    return (
        <div className="pub-invite">
            <div><b>{t('public.invite.title')}</b><span className="muted">{t('public.invite.text')}</span></div>
            <a className="btn pri" href={to}>{t('public.invite.button')}</a>
        </div>
    );
}
