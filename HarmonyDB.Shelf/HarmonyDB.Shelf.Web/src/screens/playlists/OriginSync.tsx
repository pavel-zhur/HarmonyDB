// A playlist imported from a music service, for the one who imported it: where from, and the update button. The
// update is a job on the server — the one started here, or the import's own first one (details.sync): it is followed
// until it ends, then the playlist and its list are read anew.

import { useEffect, useRef } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { errorText } from '../../api/errors';
import { ApiError } from '../../api/client';
import { formatAgo } from '../../i18n';
import { syncPlaylist, useJob, type PlaylistDetails } from '../../data/playlists';
import { toast } from '../../app/toasts';
import { Icon } from '../../kit/Icon';

export function OriginSync({ details }: { details: PlaylistDetails }) {
    const { t } = useTranslation();
    const queries = useQueryClient();
    const origin = details.playlist.origin!;
    const service = t(`playlist.serviceFrom.${origin.service}`);
    const start = useMutation({ mutationFn: syncPlaylist });
    const jobId = start.data?.sync?.id ?? details.sync?.id;
    const running = (status?: string) => status === 'queued' || status === 'running';
    const job = useJob(jobId);
    const status = job.data?.status ?? start.data?.sync?.status ?? details.sync?.status;

    // a job seen running here, and now ended: told, and what it changed read anew
    const seenRunning = useRef(false);
    useEffect(() => {
        if (running(status)) {
            seenRunning.current = true;
            return;
        }
        if (!seenRunning.current || !job.data) return;
        seenRunning.current = false;
        if (job.data.status === 'succeeded') toast(t('playlist.synced'));
        else if (job.data.errorCode) toast(errorText(new ApiError(0, job.data.errorCode)));
        void queries.invalidateQueries({ queryKey: ['songs'] });
        void queries.invalidateQueries({ queryKey: ['playlist', details.playlist.id] });
        void queries.invalidateQueries({ queryKey: ['playlists'] });
        start.reset();
    }, [status]);

    const busy = start.isPending || running(status);
    return (
        <div className="origin muted small">
            <span>{t('playlist.origin', { service })}{origin.lastSyncedAt ? ` · ${t('playlist.lastSynced', { when: formatAgo(origin.lastSyncedAt) })}` : ''}</span>
            <button type="button" className={busy ? 'btn sm spin' : 'btn sm'} disabled={busy} onClick={() => start.mutate(details.playlist.id)}>
                <Icon name="sync" />{busy ? t('playlist.syncing') : t('playlist.syncNow', { service })}
            </button>
        </div>
    );
}
