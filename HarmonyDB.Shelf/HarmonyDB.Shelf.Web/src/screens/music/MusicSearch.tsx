// The one search's music part, under the songs and the chords: artists and recordings of the music services.

import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useMusicSearch } from '../../data/music';
import { Icon } from '../../kit/Icon';
import { ArtistTile, Shelf, TrackRows } from './parts';

/** Recordings shown before "show more". */
const FIRST = 10;

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
            <div className="sec"><Icon name="phones" />{t('music.search.title')} · {artists.length + recordings.length}</div>
            {artists.length > 0 && <Shelf>{artists.map(artist => <ArtistTile key={`${artist.service}:${artist.id}`} artist={artist} />)}</Shelf>}
            {recordings.length > 0 && <TrackRows tracks={recordings} limit={all ? undefined : FIRST} />}
            {!all && recordings.length > FIRST && (
                <button type="button" className="lnk" onClick={() => setAllFor(q)}>{t('common.showMore')} · {recordings.length - FIRST}</button>
            )}
            {!artists.length && !recordings.length && <p className="empty">{t('music.nothing')}</p>}
            {!!data.musicFailures?.length && (
                <p className="muted small">
                    {t('music.search.failed')}{' '}
                    <button type="button" className="lnk" disabled={search.isFetching} onClick={() => void search.refetch()}>{t('common.retry')}</button>
                </p>
            )}
        </>
    );
}
