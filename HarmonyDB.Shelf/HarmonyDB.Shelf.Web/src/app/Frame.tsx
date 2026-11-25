// The frame around every screen: the header, the side menu (wide) or the bottom tabs (phone), the toast, the sign-in
// dialog. A screen is the <Outlet/>: a page (<Page>) or a list with its song (<ListWithSong>).

import { useEffect } from 'react';
import { Link, NavLink, Outlet, useLocation, useNavigate } from 'react-router';
import { useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Icon } from '../kit/Icon';
import { Avatar, Soon } from '../kit/controls';
import { Toaster } from '../kit/Toaster';
import { SignInDialog } from '../screens/signin/SignInDialog';
import { offerSignIn } from '../data/signInOffer';
import { useProfile, useSession } from '../data/session';
import { updateSettings, useSettings } from '../data/settings';
import { attach, local, useSyncStatus } from '../local';
import { toast } from './toasts';
import { isDark, useAppearance } from './appearance';
import { useWide } from './layout';
import { useLogo } from './logo';
import { guestOtherNav, isOn, mainNav, moreAddresses, otherNav, toolsNav, type NavItem } from './nav';

export function Frame() {
    useAppearance();
    const wide = useWide();
    const { known, accountId } = useSession();

    // the local-first store follows the person signed in
    useEffect(() => {
        if (known) attach(accountId);
    }, [known, accountId]);

    return (
        <div className="app">
            {wide && <Header />}
            <div className="mid">
                {!wide && <Header />}
                {wide && <Side />}
                <main className="main"><Outlet /></main>
            </div>
            {!wide && <Tabs />}
            <Toaster />
            <SignInDialog />
        </div>
    );
}

function Header() {
    const { t } = useTranslation();
    const { signedIn, known } = useSession();
    const logo = useLogo();
    return (
        <header className="top">
            <div className="notes" />
            <Link className="brand" to="/"><span className="logo" style={logo} /><span>{t('app.name')}</span></Link>
            <span className="sp" />
            {known && signedIn && <SyncButton />}
            <ThemeButton />
            {known && (signedIn ? <ProfileButton /> : <button type="button" className="btn signin" onClick={() => offerSignIn()}>{t('nav.signIn')}</button>)}
        </header>
    );
}

/** Spins while exchanging with the server; leads to the library sorted by new chords. */
function SyncButton() {
    const { t } = useTranslation();
    const { busy } = useSyncStatus();
    const navigate = useNavigate();
    const location = useLocation();
    const queries = useQueryClient();
    const sync = async () => {
        // a song open in the library stays open; otherwise the library, the newest chords first
        if (!/^\/library\/(songs|chords)\//.test(location.pathname)) navigate('/library?sort=newChords');
        await local.exchange();
        await queries.invalidateQueries({ queryKey: ['songs'] });
        toast(t('sync.updated'));
    };
    return (
        <button type="button" className={busy ? 'tbtn syncb spin' : 'tbtn syncb'} aria-label={t('nav.sync')} title={t('nav.sync')} onClick={() => void sync()}>
            <Icon name="sync" />
        </button>
    );
}

function ThemeButton() {
    const { t } = useTranslation();
    useSettings(); // re-render when the theme changes
    return (
        <button type="button" className="tbtn" aria-label={t('nav.theme')} title={t('nav.theme')} onClick={() => updateSettings({ theme: isDark() ? 'light' : 'dark' })}>
            <Icon name="theme" />
        </button>
    );
}

function ProfileButton() {
    const { t } = useTranslation();
    const { data: profile } = useProfile();
    return (
        <Link className="tbtn" to="/profile" aria-label={t('nav.profile')} title={t('nav.profile')}>
            {profile ? <Avatar person={{ id: profile.id, name: profile.displayName, pictureUrl: profile.pictureUrl }} /> : <Icon name="user" />}
        </Link>
    );
}

export function NavEntry({ item, className }: { item: NavItem; className?: (on: boolean) => string }) {
    const { t } = useTranslation();
    const { pathname } = useLocation();
    const on = isOn(item, pathname);
    if (item.soon)
        return <span className="nav-soon" aria-disabled="true"><Icon name={item.icon} /><span>{t(item.label)}</span><Soon /></span>;
    return <NavLink to={item.to} className={className ? className(on) : on ? 'on' : ''} end={item.to === '/'}><Icon name={item.icon} /><span>{t(item.label)}</span></NavLink>;
}

function Side() {
    const { t } = useTranslation();
    const { signedIn, known } = useSession();
    if (!known) return <aside className="side" />;
    return (
        <aside className="side">
            {signedIn ? mainNav.map(item => <NavEntry key={item.to} item={item} />) : (
                <>
                    <NavEntry item={{ to: '/', icon: 'search', label: 'nav.search' }} />
                    <button type="button" onClick={() => offerSignIn()}><Icon name="user" />{t('nav.signIn')}</button>
                </>
            )}
            <h6>{t('nav.tools')}</h6>
            {toolsNav.map(item => <NavEntry key={item.to} item={item} />)}
            <h6>{t('nav.other')}</h6>
            {(signedIn ? otherNav : guestOtherNav).map(item => <NavEntry key={item.to} item={item} />)}
        </aside>
    );
}

function Tabs() {
    const { t } = useTranslation();
    const { pathname } = useLocation();
    const { signedIn, known } = useSession();
    const moreOn = moreAddresses.some(a => pathname === a || pathname.startsWith(a + '/'));
    const more = <NavEntry item={{ to: '/more', icon: 'more', label: signedIn ? 'nav.more' : 'nav.tools' }} className={() => (moreOn ? 'on' : '')} />;
    if (!known) return <nav className="tabs" />;
    return (
        <nav className="tabs">
            {signedIn ? (
                <>
                    {mainNav.map(item => <NavEntry key={item.to} item={item} />)}
                    {more}
                </>
            ) : (
                <>
                    <NavEntry item={{ to: '/', icon: 'search', label: 'nav.search' }} />
                    {more}
                    <button type="button" onClick={() => offerSignIn()}><Icon name="user" />{t('nav.signIn')}</button>
                </>
            )}
        </nav>
    );
}
