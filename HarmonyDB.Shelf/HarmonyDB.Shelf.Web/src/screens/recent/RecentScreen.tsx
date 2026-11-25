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

export function RecentList({ entries }: { entries: Entry[] }) {
    return <div className="recent lines">{entries.map(entry => <RecentRow key={(entry.song?.id ?? '') + (entry.chords?.id ?? '')} entry={entry} />)}</div>;
}

function RecentRow({ entry }: { entry: Entry }) {
    const { t } = useTranslation();
    if (!entry.song && !entry.chords) return null;
    const header = entry.chords?.header;
    const title = entry.song?.title ?? header?.title ?? '';
    const artists = (entry.song?.artists ?? header?.artists ?? []).join(', ');
    const to = entry.song ? `/library/songs/${entry.song.playlistId}/${entry.song.id}` : `/library/chords/${entry.chords!.id}`;
    return (
        <Link to={to}>
            <span className="t">
                {title ? <b>{title}</b> : <b className="muted">{t('errors.chords_not_open')}</b>}
                <span className="muted small">{[artists, header?.source.title].filter(Boolean).join(' · ')}</span>
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
