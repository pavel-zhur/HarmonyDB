// The quiet links under a song's variants: my own variant of the song (the editor, starting from the text of the
// variant open), and ordering illustrations when
// the song has none yet (its illustrations' view, where each variant's are ordered).

import { Link } from 'react-router';
import { useTranslation } from 'react-i18next';
import type { SongPage } from '../../data/song';
import { drawPermission } from '../../data/illustrations';
import { useSession } from '../../data/session';
import { Icon } from '../../kit/Icon';
import './illustrations.css';

export function SongLinks({ page, from, illustrated, onIllustrations }: { page: SongPage; from: string | null; illustrated: boolean; onIllustrations: () => void }) {
    const { t } = useTranslation();
    const { session } = useSession();
    const variant = page.canAdd;
    const order = !illustrated && !!session?.permissions.includes(drawPermission) && page.variants.some(v => v.chords.state === 'open');
    if (!variant && !order) return null;
    return (
        <div className="il-links">
            {variant && (
                <Link className="lnk" to={`/editor?playlist=${encodeURIComponent(page.song.playlistId)}&song=${encodeURIComponent(page.song.id)}${from ? `&from=${encodeURIComponent(from)}` : ''}`}>
                    <Icon name="plus" />{t('editor.myVariant')}
                </Link>
            )}
            {order && <button type="button" className="lnk" onClick={onIllustrations}><Icon name="img" />{t('ills.order')}</button>}
        </div>
    );
}
