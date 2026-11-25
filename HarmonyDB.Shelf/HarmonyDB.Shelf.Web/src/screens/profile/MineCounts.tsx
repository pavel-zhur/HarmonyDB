// "What I have": my playlists, those I am a member of, my marks, keys and notes (as I see them now), the chords I wrote.

import { useTranslation } from 'react-i18next';
import { useMyPlaylists } from '../../data/playlists';
import { useOwnChords } from '../../data/song';
import { useLocalView } from '../../local';

export function MineCounts() {
    const { t } = useTranslation();
    const playlists = useMyPlaylists();
    const own = useOwnChords();
    const view = useLocalView();
    const mine = playlists.data?.filter(p => p.myRole === 'owner' && p.systemKind === 'none').length;
    const member = playlists.data?.filter(p => p.myRole && p.myRole !== 'owner').length;
    const rows: [string, number | undefined][] = [
        [t('account.mine.playlists'), mine],
        [t('account.mine.member'), member],
        [t('account.mine.marks'), view.watermark === null ? undefined : Object.keys(view.marks).length],
        [t('account.mine.keys'), view.watermark === null ? undefined : Object.keys(view.keys).length],
        [t('account.mine.notes'), view.watermark === null ? undefined : Object.values(view.notes).filter(n => n.text).length],
        [t('account.mine.written'), own.data?.items.length],
    ];
    return (
        <>
            <h2>{t('account.mine.title')}</h2>
            <div className="lines">
                {rows.map(([label, count]) => <div className="setrow" key={label}><span>{label}</span><b>{count ?? '…'}</b></div>)}
            </div>
        </>
    );
}
