// The one search's music part, after the songs of my playlists and before the sources' chords (the mock-up's order):
// artists, and recordings of the music services — the first six, the rest a tap away.

import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useMusicSearch } from '../../data/music';
import { Icon } from '../../kit/Icon';
import { ArtistTile, Shelf, TrackRows } from './parts';

/** Recordings shown before "show more". */
const FIRST = 6;

export function MusicSearch({ q }: { q: string }) {
    const { t } = useTranslation();
    const search = useMusicSearch(q);
    const [allFor, setAllFor] = useState<string | null>(null);
    const all = allFor === q;      // (a new search shows the first ones again)
    const data = search.data;
    if (q.trim().length < 3 || !data) return null;
    const artists = data.artists ?? [], recordings = data.recordings ?? [];
    return (
        <>
            {artists.length > 0 && (
                <>
                    <div className="sec">{t('music.artists')}</div>
                    <Shelf>{artists.map(artist => <ArtistTile key={`${artist.service}:${artist.id}`} artist={artist} />)}</Shelf>
                </>
            )}
            <div className="sec"><Icon name="phones" />{t('recordings.title')} · {recordings.length}</div>
            {recordings.length > 0 && <TrackRows tracks={recordings} limit={all ? undefined : FIRST} />}
            {!all && recordings.length > FIRST && (
                <button type="button" className="lnk" onClick={() => setAllFor(q)}>{t('common.showMore')} · {recordings.length - FIRST}</button>
            )}
            {!recordings.length && <p className="empty">{t('music.nothing')}</p>}
            {!!data.musicFailures?.length && (
                <p className="muted small">
                    {t('music.search.failed')}{' '}
                    <button type="button" className="lnk" disabled={search.isFetching} onClick={() => void search.refetch()}>{t('common.retry')}</button>
                </p>
            )}
        </>
    );
}
