// A playlist of YouTube or Yandex Music by its link: becomes a playlist of the shelf in my account; it is updated
// later by the button at the playlist. /playlists/import

import { useState } from 'react';
import { Link, useNavigate } from 'react-router';
import { useMutation } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { ApiError } from '../../api/client';
import { Page } from '../../app/panes';
import { importPlaylist } from '../../data/playlists';
import { Icon } from '../../kit/Icon';

export function ImportPlaylistScreen() {
    const { t } = useTranslation();
    const navigate = useNavigate();
    const [url, setUrl] = useState('');
    const run = useMutation({ mutationFn: importPlaylist, onSuccess: r => navigate(`/playlists/${r.value.playlist.id}`) });
    const already = run.error instanceof ApiError && run.error.code === 'already_imported' ? run.error.details?.playlistId : null;
    return (
        <Page>
            <h1>{t('importPlaylist.title')}</h1>
            <p className="lead">{t('importPlaylist.text')}</p>
            <form className="inline-form" style={{ maxWidth: 620 }} onSubmit={e => { e.preventDefault(); if (url.trim()) run.mutate(url.trim()); }}>
                <input className="field" type="url" inputMode="url" value={url} onChange={e => setUrl(e.target.value)} placeholder={t('importPlaylist.placeholder')} aria-label={t('importPlaylist.title')} />
                <button type="submit" className="btn pri" disabled={!url.trim() || run.isPending}><Icon name="imp" />{t('importPlaylist.go')}</button>
            </form>
            {already && <p style={{ marginTop: 12 }}><Link className="lnk" to={`/playlists/${already}`}>{t('importPlaylist.already')}<Icon name="right" /></Link></p>}
        </Page>
    );
}
