// "Chords offline": keeping the library on this device — the switch, what the copy holds against what the library
// holds and how much it takes, its lists taken anew, the changes waiting for the connection, and deleting the copy.

import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { errorText } from '../../api/errors';
import { Page } from '../../app/panes';
import { formatAgo } from '../../i18n';
import { useSyncStatus } from '../../local';
import { downloadLists, removeCopy, setKeeping, useOffline } from '../../offline';
import { Confirm, Switch } from '../../kit/controls';
import { Icon } from '../../kit/Icon';
import './offline.css';


export function OfflineScreen() {
    const { t } = useTranslation();
    const offline = useOffline();
    const { queued } = useSyncStatus();
    const [asking, setAsking] = useState(false);
    const [switching, setSwitching] = useState(false);
    const toggle = async (on: boolean) => {
        setSwitching(true);
        await setKeeping(on);
        setSwitching(false);
    };
    return (
        <Page>
            <h1>{t('tools.offline')}</h1>
            <p className="lead">{t('offline.lead')}</p>
            <div className="lines of-lines">
                <div className="setrow">
                    <span>{t('offline.keep')}<span className="sub">{t('offline.keepSub')}</span></span>
                    <Switch on={offline.keeping} disabled={!offline.ready || switching || offline.busy} label={t('offline.keep')} onChange={on => void toggle(on)} />
                </div>
                {offline.keeping && (
                    <>
                        <div className="setrow">
                            <span>{t('offline.lists')}
                                <span className="sub">
                                    {offline.busy
                                        ? t('offline.taking', offline.progress ?? { done: 0, all: 0 })
                                        : offline.downloadedAt ? t('offline.listsAt', { ago: formatAgo(new Date(offline.downloadedAt).toISOString()) }) : t('offline.listsNone')}
                                </span>
                            </span>
                            <button type="button" className="btn" disabled={offline.busy} onClick={() => void downloadLists()}>
                                <Icon name="sync" className={offline.busy ? 'spin' : undefined} />{t('offline.update')}
                            </button>
                        </div>
                        {offline.busy && offline.progress && (
                            <div className="of-bar" role="progressbar" aria-valuemin={0} aria-valuemax={offline.progress.all} aria-valuenow={offline.progress.done}>
                                <i style={{ width: `${Math.round((offline.progress.done / Math.max(1, offline.progress.all)) * 100)}%` }} />
                            </div>
                        )}
                        <div className="setrow"><span>{t('offline.songs')}</span><b>{t('offline.of', { kept: offline.songs.kept, all: offline.songs.all })}</b></div>
                        <div className="setrow"><span>{t('offline.chords')}</span><b>{t('offline.of', { kept: offline.chords.kept, all: offline.chords.all })}</b></div>
                        <div className="setrow"><span>{t('offline.takes')}</span><b>{offline.bytes < 1024 * 1024 ? t('offline.kb', { kb: Math.ceil(offline.bytes / 1024) }) : t('offline.mb', { mb: (offline.bytes / 1024 / 1024).toFixed(1) })}</b></div>
                        <p className="muted small of-note">{t('offline.asOpened')}</p>
                    </>
                )}
                <div className="setrow"><span>{t('offline.changes')}<span className="sub">{t('offline.changesSub')}</span></span><b>{queued}</b></div>
            </div>
            {offline.failed !== null && <p className="failed">{errorText(offline.failed)}</p>}
            {offline.keeping && (
                <div className="of-remove">
                    {asking
                        ? <Confirm text={t('offline.removeAsk')} choices={[[t('offline.removeYes'), () => { setAsking(false); void removeCopy(); }]]} onCancel={() => setAsking(false)} />
                        : <button type="button" className="btn dan" onClick={() => setAsking(true)}><Icon name="trash" />{t('offline.remove')}</button>}
                </div>
            )}
        </Page>
    );
}
