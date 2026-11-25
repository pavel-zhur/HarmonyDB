// "What the shelf can do": a showcase of everything it does, in groups, each card leading to where it is done ("soon"
// where it is not there yet); the guest's home shows the most interesting of the same cards. Then the versions.

import { useState } from 'react';
import { Link, useNavigate } from 'react-router';
import { useTranslation } from 'react-i18next';
import { Page } from '../../app/panes';
import { SearchField, Soon } from '../../kit/controls';
import { Icon, type IconName } from '../../kit/Icon';
import { Visual, type Card } from '../home/GuestHome';
import type { en } from '../../i18n/en';
import '../home/home.css';
import './about.css';

type Own = keyof typeof en.about.cards;
type Item = { guest: Card; to?: string; soon?: boolean; wide?: boolean } | { own: Own; to?: string };

const groups: { key: keyof typeof en.about.groups; items: Item[] }[] = [
    { key: 'best', items: [{ guest: 'playAlong', to: '/library' }, { guest: 'importPlaylist', to: '/playlists/import' }, { guest: 'music', to: '/' }, { guest: 'import', to: '/import' }, { guest: 'ai', to: '/ai' }, { guest: 'illustrations', to: '/illustrations' }, { guest: 'book', to: '/playlists' }, { guest: 'analysis', soon: true }] },
    { key: 'play', items: [{ guest: 'fingerings', to: '/library' }, { guest: 'keys', to: '/library' }, { guest: 'wrap', to: '/library', wide: true }] },
    { key: 'own', items: [{ guest: 'playlists', to: '/playlists' }, { own: 'linking', to: '/playlists' }, { own: 'marks', to: '/library' }, { own: 'together', to: '/playlists' }, { own: 'own', to: '/editor' }] },
    { key: 'more', items: [{ guest: 'noAds' }, { own: 'links' }, { own: 'yourMusic', to: '/profile' }, { own: 'look', to: '/profile' }] },
];

const icons = (names: IconName[]) => <span className="ficons">{names.map((n, i) => <Icon key={n + i} name={n} />)}</span>;

function OwnVisual({ card }: { card: Own }) {
    switch (card) {
        case 'linking': return <><span className="ftl"><i className="cur">A</i><i>B</i><i>C</i></span>{icons(['link'])}</>;
        case 'marks': return <span className="ab-emo">❤️ 🔥 🏕️ ⭐ 🎸 💛</span>;
        case 'together': return icons(['users', 'link', 'share']);
        case 'own': return <span className="fed">&gt;Am{' '.repeat(5)}F</span>;
        case 'links': return icons(['link', 'lock']);
        case 'yourMusic': return icons(['vol', 'smile']);
        case 'look': return <><Icon name="theme" /><span className="step fs"><i>−</i><span>120%</span><i>+</i></span></>;
    }
}

export function AboutScreen() {
    const { t } = useTranslation();
    return (
        <Page>
            <h1>{t('tools.about')}</h1>
            {groups.map((group, k) => (
                <section key={group.key}>
                    <h2 className={k ? undefined : 'ab-first'}>{t(`about.groups.${group.key}`)}</h2>
                    <div className="fgridc">
                        {k === 0 && <SearchCard />}
                        {group.items.map(item => <FeatureCard key={'guest' in item ? item.guest : item.own} item={item} />)}
                    </div>
                </section>
            ))}
            <p className="ab-versions"><Link className="btn" to="/versions"><Icon name="clock" />{t('about.versions')}</Link></p>
        </Page>
    );
}

/** The first card: the one search, tried right here — the words go to the home's search, where the results come as one types. */
function SearchCard() {
    const { t } = useTranslation();
    const navigate = useNavigate();
    const [q, setQ] = useState('');
    return (
        <div className="fcard tall">
            <form className="fvis ab-search" onSubmit={e => { e.preventDefault(); if (q.trim()) navigate(`/?q=${encodeURIComponent(q.trim())}`); }}>
                <SearchField value={q} onChange={setQ} placeholder={t('home.searchPlaceholder')} />
                <button type="submit" className="btn pri" disabled={!q.trim()}>{t('about.find')}</button>
            </form>
            <span className="fbody"><b>{t('about.cards.search.title')}</b><small>{t('about.cards.search.text')}</small></span>
        </div>
    );
}

function FeatureCard({ item }: { item: Item }) {
    const { t } = useTranslation();
    const guest = 'guest' in item;
    const title = guest ? t(`guest.cards.${item.guest}.title`) : t(`about.cards.${item.own}.title`);
    const text = guest ? t(`guest.cards.${item.guest}.text`) : t(`about.cards.${item.own}.text`);
    const soon = guest && item.soon;
    const body = (
        <>
            <span className="fvis">{guest ? <Visual card={item.guest} /> : <OwnVisual card={item.own} />}</span>
            <span className="fbody"><b>{title} {soon && <Soon />}</b><small>{text}</small></span>
        </>
    );
    const className = guest && item.wide ? 'fcard tall' : 'fcard';
    return item.to && !soon ? <Link className={`${className} ab-go`} to={item.to}>{body}</Link> : <div className={className}>{body}</div>;
}
