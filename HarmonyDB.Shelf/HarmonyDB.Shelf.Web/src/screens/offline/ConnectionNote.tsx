// The frame's quiet line when there is no connection: what is seen is the copy kept on this device, and as of when
// (the oldest of what the page shows from it). Nothing when the connection is there and nothing comes from the copy.

import { useEffect, useSyncExternalStore } from 'react';
import { useLocation } from 'react-router';
import { useTranslation } from 'react-i18next';
import { formatAgo } from '../../i18n';
import { forgetServed, useServedFromCopy } from '../../offline';
import { Icon } from '../../kit/Icon';
import './offline.css';

function useOnline(): boolean {
    return useSyncExternalStore(listener => {
        addEventListener('online', listener);
        addEventListener('offline', listener);
        return () => {
            removeEventListener('online', listener);
            removeEventListener('offline', listener);
        };
    }, () => navigator.onLine);
}

export function ConnectionNote() {
    const { t } = useTranslation();
    const online = useOnline();
    const servedAt = useServedFromCopy();
    const { pathname } = useLocation();
    // each page tells anew what it shows from the copy
    useEffect(() => forgetServed(), [pathname]);
    if (online && servedAt === null) return null;
    return (
        <div className="of-conn" role="status">
            <Icon name="cloud" />
            <span>
                {t(online ? 'offline.noServer' : 'offline.noConnection')}
                {servedAt !== null && ` ${t('offline.keptAsOf', { ago: formatAgo(new Date(servedAt).toISOString()) })}`}
            </span>
        </div>
    );
}
