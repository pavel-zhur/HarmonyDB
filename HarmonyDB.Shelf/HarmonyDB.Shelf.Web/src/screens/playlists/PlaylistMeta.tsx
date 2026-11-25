import { useTranslation } from 'react-i18next';
import type { PlaylistSummary } from '../../data/playlists';
import { Icon, type IconName } from '../../kit/Icon';

/** "12 songs · 4 members · by link" — a playlist in one line. */
export function PlaylistMeta({ playlist }: { playlist: PlaylistSummary }) {
    const { t } = useTranslation();
    return (
        <>
            {t('playlists.songs', { count: playlist.songCount })}
            {' · '}
            {/* "only me" is the viewer's own playlist alone; to others, how many keep it */}
            {playlist.memberCount > 1 ? <>{t('playlists.members', { count: playlist.memberCount })}{' · '}</>
                : playlist.myRole ? <>{t('playlists.onlyMe')}{' · '}</> : null}
            {t(`playlists.access.${playlist.access}`)}
        </>
    );
}

/** The sign of a playlist's kind: system ones by what they gather, others by their access. */
export function playlistIcon(playlist: PlaylistSummary): IconName {
    switch (playlist.systemKind) {
        case 'importedChords': return 'imp';
        case 'ownChords': return 'pen';
        case 'ownAnalyses': return 'ai';
        default: return playlist.access === 'private' ? 'lock' : playlist.access === 'link' ? 'link' : 'globe';
    }
}

/** A playlist's cover: the covers of its first songs (four in a square; one fills it), or a quiet square with its sign. */
export function Cover({ playlist, size }: { playlist: PlaylistSummary; size?: 'big' | 'tile' }) {
    const name = size ? `cover ${size}` : 'cover';
    const covers = playlist.covers;
    if (covers.length > 0) {
        const four = covers.length === 1 ? covers : [0, 1, 2, 3].map(i => covers[i % covers.length]!);
        return <span className={`${name} pics${covers.length === 1 ? ' one' : ''}`} aria-hidden="true">{four.map((c, i) => <img key={i} src={c} alt="" loading="lazy" />)}</span>;
    }
    return <span className={name} aria-hidden="true">{playlist.systemKind === 'none' ? <span className="glyph">♪</span> : <Icon name={playlistIcon(playlist)} />}</span>;
}
