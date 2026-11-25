// "Chords offline": keeping the whole library on this device — the switch; taking it (so many of so many, going on
// where it stopped) and bringing it up to date; what the copy holds against the library and how much it takes; what is
// not kept (recordings, pictures); the changes waiting for the connection; deleting the copy.

import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { errorText } from '../../api/errors';
import { Page } from '../../app/panes';
import { formatAgo } from '../../i18n';
import { useSyncStatus } from '../../local';
import { download, removeCopy, setKeeping, useOffline, useOnline } from '../../offline';
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
    const online = useOnline();
    // a taking that stopped (a closed tab, a lost connection) goes on by itself when the page is opened with a
    // connection; a first taking is never started unasked
    useEffect(() => {
        if (offline.ready && offline.unfinished && online && !offline.busy && offline.failed === null) void download();
    }, [offline.ready, offline.unfinished, online]);
    const size = offline.bytes < 1024 * 1024 ? t('offline.kb', { kb: Math.ceil(offline.bytes / 1024) }) : t('offline.mb', { mb: (offline.bytes / 1024 / 1024).toFixed(1) });
    const progress = offline.progress;
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
                            <span>{t('offline.library')}
                                <span className="sub">
                                    {progress
                                        ? t(progress.stage === 'lists' ? 'offline.takingLists' : 'offline.takingChords', { done: progress.done, all: progress.all })
                                        : offline.unfinished ? t('offline.partly', { kept: offline.chords.kept, all: offline.chords.all })
                                            : offline.downloadedAt ? t('offline.updatedAt', { ago: formatAgo(new Date(offline.downloadedAt).toISOString()) }) : t('offline.notYet')}
                                </span>
                            </span>
                            <button type="button" className="btn" disabled={offline.busy} onClick={() => void download()}>
                                <Icon name="sync" className={offline.busy ? 'spin' : undefined} />{t(offline.unfinished ? 'offline.goOn' : 'offline.update')}
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
                    </>
                )}
                <div className="setrow"><span>{t('offline.changes')}<span className="sub">{t('offline.changesSub')}</span></span><b>{queued}</b></div>
            </div>
            {offline.failed !== null && <p className="failed">{errorText(offline.failed)}{offline.chords.kept > 0 && ` ${t('offline.goesOn')}`}</p>}
            {offline.keeping && (
                <div className="of-remove">
                    <p><button type="button" className="btn" disabled={offline.busy} onClick={() => void download({ all: true })}>{t('offline.readAll')}</button></p>
                    {asking
                        ? <Confirm text={t('offline.removeAsk')} choices={[[t('offline.removeYes'), () => { setAsking(false); void removeCopy(); }]]} onCancel={() => setAsking(false)} />
                        : <button type="button" className="btn dan" onClick={() => setAsking(true)}><Icon name="trash" />{t('offline.remove')}</button>}
                </div>
            )}
        </Page>
    );
}
