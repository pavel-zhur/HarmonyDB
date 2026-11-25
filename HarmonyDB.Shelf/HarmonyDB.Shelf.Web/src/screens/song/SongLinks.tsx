// The quiet links under a song's variants (the mock-up's): my own variant of the song (the editor, starting from the
// text of the variant open); with a recording of the song, similar ones (the music's pages), without one, more of the
// artist in this playlist; the song looked
// for everywhere (the one search, the sources too); ordering illustrations when the song has none yet (its
// illustrations' view, where each variant's are ordered).

import { Link } from 'react-router';
import { useTranslation } from 'react-i18next';
import type { SongPage } from '../../data/song';
import { drawPermission } from '../../data/illustrations';
import { useSongRecordings } from '../../data/recordings';
import { useSession } from '../../data/session';
import { Icon } from '../../kit/Icon';
import './illustrations.css';

export function SongLinks({ page, from, share, illustrated, onIllustrations }: {
    page: SongPage; from: string | null; share: string | null; illustrated: boolean; onIllustrations: () => void;
}) {
    const { t } = useTranslation();
    const { session, signedIn } = useSession();
    const song = page.song;
    const recordings = useSongRecordings({ playlistId: song.playlistId, songId: song.id, share });
    const chosen = recordings.data?.recordings.find(x => x.itemId === recordings.data?.myChoice) ?? recordings.data?.recordings[0] ?? null;
    const variant = page.canAdd;
    const order = !illustrated && !!session?.permissions.includes(drawPermission) && page.variants.some(v => v.chords.state === 'open');
    const artist = song.artists[0] ?? null;
    const everywhere = [song.artists.join(' '), song.title].filter(Boolean).join(' ');
    return (
        <div className="il-links">
            {variant && (
                <Link className="lnk" to={`/editor?playlist=${encodeURIComponent(song.playlistId)}&song=${encodeURIComponent(song.id)}${from ? `&from=${encodeURIComponent(from)}` : ''}`}>
                    <Icon name="plus" />{t('editor.myVariant')}
                </Link>
            )}
            {chosen && signedIn ? (
                <Link className="lnk" to={`/music/${chosen.track.service}/tracks/${encodeURIComponent(chosen.track.id)}/similar`}>{t('song.links.similar')}</Link>
            ) : artist && !share && (
                <Link className="lnk" to={`/playlists/${encodeURIComponent(song.playlistId)}?q=${encodeURIComponent(artist)}`}>{t('song.links.moreOf', { artist })}</Link>
            )}
            <Link className="lnk" to={`/library?q=${encodeURIComponent(everywhere)}`}><Icon name="globe" />{t('song.links.everywhere')}</Link>
            {order && <button type="button" className="lnk" onClick={onIllustrations}><Icon name="img" />{t('ills.order')}</button>}
        </div>
    );
}
