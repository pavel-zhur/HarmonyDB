// The guest's home: it invites, it does not begin with "you are not signed in". Under the search that really searches:
// a welcome, songs to try right now (the most liked of the biggest public playlist), the public playlists; then (the
// home's) a few things worth trying and a quiet offer to sign in. Also the cards' small pictures, for those and for
// "What the shelf can do".

import { Fragment } from 'react';
import { useTranslation } from 'react-i18next';
import { offerSignIn } from '../../data/signInOffer';
import { usePublicPlaylists } from '../../data/playlists';
import { rowId, useSongList } from '../../data/songs';
import { Icon, type IconName } from '../../kit/Icon';
import { SongRow } from '../../kit/SongRow';
import { PlaylistLine } from '../playlists/PlaylistsScreen';
import type { en } from '../../i18n/en';
import './home.css';

export type Card = keyof typeof en.guest.cards;

export function GuestHome() {
    const { t } = useTranslation();
    const playlists = usePublicPlaylists('', 20);
    const biggest = playlists.data?.reduce<typeof playlists.data[number] | null>((a, b) => (!a || b.songCount > a.songCount ? b : a), null) ?? null;
    return (
        <>
            <div className="welcome">
                <b>{t('guest.welcomeTitle')}</b>
                <span>{t('guest.welcomeText')}</span>
            </div>
            {biggest && <TryNow playlistId={biggest.id} />}
            {!!playlists.data?.length && (
                <>
                    <h2>{t('guest.publicPlaylists')}</h2>
                    <div className="pls">{playlists.data.map(p => <PlaylistLine key={p.id} playlist={p} />)}</div>
                </>
            )}
        </>
    );
}

/** The quiet offer to sign in: the guest's home, and where a guest closed the chords. */
export function SignInOffer() {
    const { t } = useTranslation();
    return (
        <>
            <div className="softcta">
                <div><b>{t('guest.ctaTitle')}</b><span className="muted">{t('guest.ctaText')}</span></div>
                <button type="button" className="btn pri" onClick={() => offerSignIn()}>{t('nav.signIn')}</button>
            </div>
        </>
    );
}

function TryNow({ playlistId }: { playlistId: string }) {
    const { t } = useTranslation();
    const songs = useSongList({ kind: 'playlist', id: playlistId }, { q: '', sort: 'popular', who: null, mark: null });
    const rows = songs.data?.pages[0]?.rows.slice(0, 5) ?? [];
    if (!rows.length) return null;
    return (
        <>
            <h2>{t('guest.tryNow')}</h2>
            <div className="rows">
                {rows.map(row => (
                    <SongRow noMarks key={rowId(row)} to={`/playlists/${playlistId}/songs/${row.songId}`} lit={false} q="" title={row.title} artists={row.artists}
                        sources={row.sources} otherMarks={row.otherMarks} otherPeople={row.otherPeople} />
                ))}
            </div>
        </>
    );
}

const icons = (names: IconName[]) => <span className="ficons">{names.map(n => <Icon key={n} name={n} />)}</span>;
const pills = (names: string[], current: number) => <span className="ftl">{names.map((n, i) => <i key={n + i} className={i === current ? 'cur' : ''}>{n}</i>)}</span>;

/** Each card's small picture, drawn of the design's own pieces. */
export function Visual({ card }: { card: Card }) {
    const { t } = useTranslation();
    switch (card) {
        case 'analysis': return <><Icon name="ai" />{pills(['Am', 'F', 'C', 'G', 'Am'], 2)}</>;
        case 'playAlong': return <><span className="fplay"><Icon name="play" /></span><span className="fbar"><i /></span><Icon name="mixer" /></>;
        case 'music': return icons(['vol', 'yt', 'phones']);
        case 'import': return icons(['link', 'file', 'cam', 'tg']);
        case 'importPlaylist': return <>{icons(['yt', 'vol'])}<Icon name="right" />{icons(['pls'])}</>;
        case 'fingerings': return <>{pills(['Am', 'E7'], 0)}<Icon name="vol" /></>;
        case 'wrap': return <WrapDemo />;
        case 'keys': return <><span className="step fs"><i>−</i><span>+2</span><i>+</i></span><span className="kchip">{t('guest.cards.keys.mine')} <b>+2</b></span><span className="kchip h">{t('guest.cards.keys.here')} <b>+5</b></span></>;
        case 'ai': return <><Icon name="mcp" /><span className="fed">{t('guest.cards.ai.example')}</span></>;
        case 'book': return <span className="spreads">{['', '1', '2', '3'].map((n, i) => <span className="spr" key={i}><i className="pg">{n}</i><i className="pg">{n ? Number(n) + 1 : ''}</i></span>)}</span>;
        case 'illustrations': return icons(['img', 'img', 'img']);
        case 'playlists': return icons(['pls', 'users', 'link']);
        case 'noAds': return <span className="fnoad">{t('guest.cards.noAds.sign')}</span>;
    }
}

/** The same line at two widths: the chord stays over its word when the line wraps. */
function WrapDemo() {
    const { t } = useTranslation();
    const words = t('guest.cards.wrap.line').split(' ');
    const chords = ['Am', '', '', 'F', '', 'C'];
    const line = (
        <span className="ln">
            {words.map((word, i) => (
                // the word and its chord never part; the line breaks between words
                <Fragment key={i}><span className="nw">{chords[i] ? <span className="ca"><b className="chord">{chords[i]}</b></span> : null}{word}</span>{i < words.length - 1 ? ' ' : ''}</Fragment>
            ))}
        </span>
    );
    return (
        <>
            <span className="fwrap"><small>{t('guest.cards.wrap.wide')}</small><span className="chords demo" style={{ width: 230 }}>{line}</span></span>
            <span className="fwrap"><small>{t('guest.cards.wrap.narrow')}</small><span className="chords demo" style={{ width: 110 }}>{line}</span></span>
        </>
    );
}
