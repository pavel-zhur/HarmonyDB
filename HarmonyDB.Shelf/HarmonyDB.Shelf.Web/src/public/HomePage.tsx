// The main page — the showcase: a word on what this is, the open playlist of analyses (each opens here, /songs/:songId,
// with its views and player), the way into the app.

import { useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { errorText } from '../api/errors';
import { Invite } from './PublicFrame';
import { PlaylistSongs } from './Songs';
import { appAddress, useShowcase } from './site';

export function HomePage() {
    const { t } = useTranslation();
    const showcase = useShowcase();
    useEffect(() => {
        document.title = t('app.name');
    }, [t]);
    return (
        <div className="pane">
            <div className="pad">
                <div className="pub-welcome">
                    <b>{t('public.home.title')}</b>
                    <span>{t('public.home.lead')}</span>
                </div>
                {showcase.isError && <p className="failed">{errorText(showcase.error)}</p>}
                {showcase.data && <PlaylistSongs playlistId={showcase.data} to={songId => `/songs/${songId}`} />}
                {showcase.data === null && <p className="muted pub-none">{t('public.home.nothing')}</p>}
                <Invite to={appAddress()} />
            </div>
        </div>
    );
}
