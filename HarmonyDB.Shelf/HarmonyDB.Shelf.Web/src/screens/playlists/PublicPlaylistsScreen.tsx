// The public playlists: open to everyone, with a guest too. /playlists/public

import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { errorText } from '../../api/errors';
import { Page } from '../../app/panes';
import { usePublicPlaylists } from '../../data/playlists';
import { SearchField } from '../../kit/controls';
import { PlaylistLine } from './PlaylistsScreen';

export function PublicPlaylistsScreen() {
    const { t } = useTranslation();
    const [q, setQ] = useState('');
    const playlists = usePublicPlaylists(q);
    return (
        <Page>
            <h1>{t('playlists.public')}</h1>
            <SearchField value={q} onChange={setQ} placeholder={t('playlists.publicSearch')} />
            {playlists.isError && <p className="failed">{errorText(playlists.error)}</p>}
            <div className="pls" style={{ marginTop: 14 }}>
                {playlists.data?.map(p => <PlaylistLine key={p.id} playlist={p} />)}
            </div>
            {playlists.data?.length === 0 && <p className="empty">{t('playlists.publicEmpty')}</p>}
        </Page>
    );
}
