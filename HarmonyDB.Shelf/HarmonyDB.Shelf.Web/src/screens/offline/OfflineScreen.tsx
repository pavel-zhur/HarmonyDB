// "Chords offline": the library this device keeps — always, with no switch, as the old shelf kept it: taking it (so
// many of so many, going on where it stopped) and keeping it up to date by itself; what the copy holds against the
// library and how much it takes; what is not kept (recordings, pictures); the changes waiting for the connection;
// "Clear": the copy deleted and taken anew.

import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { errorText } from '../../api/errors';
import { Page } from '../../app/panes';
import { formatAgo } from '../../i18n';
import { useSyncStatus } from '../../local';
import { clearCopy, download, useOffline } from '../../offline';
import { Confirm } from '../../kit/controls';
import { Icon } from '../../kit/Icon';
import './offline.css';

export function OfflineScreen() {
    const { t } = useTranslation();
    const offline = useOffline();
    const { queued } = useSyncStatus();
    const [asking, setAsking] = useState(false);
    const size = offline.bytes < 1024 * 1024 ? t('offline.kb', { kb: Math.ceil(offline.bytes / 1024) }) : t('offline.mb', { mb: (offline.bytes / 1024 / 1024).toFixed(1) });
    const progress = offline.progress;
    return (
        <Page>
            <h1>{t('tools.offline')}</h1>
            <p className="lead">{t('offline.lead')}</p>
            <div className="lines of-lines">
                <div className="setrow">
                    <span>{t('offline.library')}
                        <span className="sub">
                            {progress
                                ? t(progress.stage === 'lists' ? 'offline.takingLists' : 'offline.takingChords', { done: progress.done, all: progress.all })
                                : offline.downloadedAt ? t('offline.updatedAt', { ago: formatAgo(new Date(offline.downloadedAt).toISOString()) }) : t('offline.notYet')}
                        </span>
                    </span>
                    <button type="button" className="btn" disabled={!offline.ready || offline.busy} onClick={() => void download()}>
                        <Icon name="sync" className={offline.busy ? 'spin' : undefined} />{t('offline.update')}
                    </button>
                </div>
                {progress && (
                    <div className="of-bar" role="progressbar" aria-valuemin={0} aria-valuemax={progress.all} aria-valuenow={progress.done}>
                        <i style={{ width: `${Math.round((progress.done / Math.max(1, progress.all)) * 100)}%` }} />
                    </div>
                )}
                <div className="setrow"><span>{t('offline.chords')}</span><b>{t('offline.of', { kept: offline.chords.kept, all: offline.chords.all })}</b></div>
                <div className="setrow"><span>{t('offline.songs')}<span className="sub">{t('offline.songsSub')}</span></span><b>{t('offline.of', { kept: offline.songs.kept, all: offline.songs.all })}</b></div>
                <div className="setrow"><span>{t('offline.takes')}</span><b>{size}</b></div>
                {offline.later > 0 && <p className="muted small of-note">{t('offline.later', { count: offline.later })}</p>}
                {offline.closed > 0 && <p className="muted small of-note">{t('offline.closed', { count: offline.closed })}</p>}
                <p className="muted small of-note">{t('offline.notMedia')}</p>
                <div className="setrow"><span>{t('offline.changes')}<span className="sub">{t('offline.changesSub')}</span></span><b>{queued}</b></div>
            </div>
            {offline.failed !== null && <p className="failed">{errorText(offline.failed)}{offline.chords.kept > 0 && ` ${t('offline.goesOn')}`}</p>}
            <div className="of-remove">
                {asking
                    ? <Confirm text={t('offline.clearAsk')} choices={[[t('offline.clearYes'), () => { setAsking(false); void clearCopy(); }]]} onCancel={() => setAsking(false)} />
                    : <button type="button" className="btn dan" disabled={!offline.ready || offline.busy} onClick={() => setAsking(true)}><Icon name="trash" />{t('offline.clear')}</button>}
            </div>
        </Page>
    );
}
