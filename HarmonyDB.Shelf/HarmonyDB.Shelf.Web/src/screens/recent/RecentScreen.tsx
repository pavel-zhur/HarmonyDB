// What I opened lately, how long ago, with my key on it. /recent (and the home's "Continue").

import { Link } from 'react-router';
import { useTranslation } from 'react-i18next';
import type { Schemas } from '../../api/client';
import { errorText } from '../../api/errors';
import { Page } from '../../app/panes';
import { formatAgo } from '../../i18n';
import { useRecent } from '../../data/songs';
import { useMyKey } from '../../local';

type Entry = Schemas['RecentEntry'];

export function RecentScreen() {
    const { t } = useTranslation();
    const recent = useRecent();
    return (
        <Page>
            <h1>{t('recent.title')}</h1>
            {recent.isError && <p className="failed">{errorText(recent.error)}</p>}
            {recent.data?.length === 0 && <p className="empty">{t('recent.empty')}</p>}
            <RecentList entries={recent.data ?? []} />
        </Page>
    );
}

/** The same chords opened alone and as a song (or in two places) are one line: the latest. `limit`: lines at most. */
export function RecentList({ entries, limit }: { entries: Entry[]; limit?: number }) {
    const seen = new Set<string>();
    const lines = entries.filter(entry => {
        const id = entry.chords?.id ?? entry.song?.id;
        if (!id || seen.has(id)) return false;
        seen.add(id);
        return true;
    }).slice(0, limit);
    return <div className="recent lines">{lines.map(entry => <RecentRow key={(entry.song?.id ?? '') + (entry.chords?.id ?? '')} entry={entry} />)}</div>;
}

function RecentRow({ entry }: { entry: Entry }) {
    const { t } = useTranslation();
    if (!entry.song && !entry.chords) return null;
    // the song's own name; chords alone: as the person was shown them when they opened them
    const title = entry.song?.title ?? entry.title ?? '';
    const artists = (entry.song?.artists ?? entry.artists).join(', ');
    const to = entry.song ? `/library/songs/${entry.song.playlistId}/${entry.song.id}` : `/library/chords/${entry.chords!.id}`;
    return (
        <Link to={to}>
            <span className="t">
                {title ? <b>{title}</b> : <b className="muted">{t('song.chords.notOpen')}</b>}
                <span className="muted small">{[artists, entry.source?.title].filter(Boolean).join(' · ')}</span>
            </span>
            <span className="when">
                {entry.chords && <Key chords={entry.chords.id} />}
                <br />
                <span className="muted small">{formatAgo(entry.openedAt)}</span>
            </span>
        </Link>
    );
}

function Key({ chords }: { chords: string }) {
    const key = useMyKey(chords);
    return <span className="k">{key ? (key > 0 ? `+${key}` : `${key}`) : ''}</span>;
}
