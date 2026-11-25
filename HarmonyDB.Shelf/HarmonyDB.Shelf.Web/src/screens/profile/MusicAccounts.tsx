// My own accounts of the music services, on the profile's settings: linked, their playlists, likes and history are
// on the playlists page. Recordings are found without them. Linking asks for what the browser holds of the person's
// session there — said plainly, with what the shelf does with it; unlinking deletes it.

import { useState } from 'react';
import { useMutation } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import type { Schemas } from '../../api/client';
import { errorText } from '../../api/errors';
import { formatDate } from '../../i18n';
import { linkMusic, unlinkMusic } from '../../data/account';
import { useMusicLinks } from '../../data/music';
import { Confirm } from '../../kit/controls';
import { Dialog } from '../../kit/Dialog';

type Service = Schemas['MusicService'];
const services: Service[] = ['yandex', 'youtube'];

export function MusicAccounts() {
    const links = useMusicLinks();
    return <>{services.map(service => <MusicAccount key={service} service={service} link={links.data?.find(x => x.service === service) ?? null} known={!!links.data} />)}</>;
}

function MusicAccount({ service, link, known }: { service: Service; link: Schemas['MusicLinkItem'] | null; known: boolean }) {
    const { t } = useTranslation();
    const [linking, setLinking] = useState(false);
    const [asking, setAsking] = useState(false);
    const unlink = useMutation({ mutationFn: unlinkMusic, onSettled: () => setAsking(false) });
    return (
        <div>
            <div className="setrow">
                <span>
                    {t(`account.music.name.${service}`)}
                    <span className="sub">
                        {link
                            ? t('account.music.linkedAs', { name: link.externalName ?? t('account.music.anAccount'), date: formatDate(link.createdAt) })
                            : t('account.music.invite')}
                    </span>
                </span>
                {known && (link
                    ? !asking && <button type="button" className="btn" onClick={() => setAsking(true)}>{t('account.music.unlink')}</button>
                    : <button type="button" className="btn" onClick={() => setLinking(true)}>{t('account.music.link')}</button>)}
            </div>
            {asking && <Confirm text={t('account.music.unlinkAsk', { service: t(`account.music.name.${service}`) })}
                choices={[[t('account.music.unlinkYes'), () => unlink.mutate(service)]]} onCancel={() => setAsking(false)} />}
            {linking && <LinkDialog service={service} onClose={() => setLinking(false)} />}
        </div>
    );
}

/** What to paste and why, the field, and the music service's answer (a refusal said here). */
function LinkDialog({ service, onClose }: { service: Service; onClose: () => void }) {
    const { t } = useTranslation();
    const [credentials, setCredentials] = useState('');
    const link = useMutation({ mutationFn: () => linkMusic(service, credentials), onSuccess: onClose });
    return (
        <Dialog label={t('account.music.linkTitle', { service: t(`account.music.name.${service}`) })} onClose={onClose}>
            <div className="pf-dlg">
                <h1>{t('account.music.linkTitle', { service: t(`account.music.name.${service}`) })}</h1>
                <p>{t(`account.music.what.${service}`)}</p>
                <p className="muted small">{t(`account.music.how.${service}`)}</p>
                <textarea className="field pf-cred" value={credentials} onChange={e => setCredentials(e.target.value)} spellCheck={false} autoComplete="off"
                    placeholder={t(`account.music.placeholder.${service}`)} aria-label={t(`account.music.placeholder.${service}`)} />
                <p className="muted small">{t('account.music.honest')}</p>
                {link.isError && <p className="failed">{errorText(link.error)}</p>}
                <button type="button" className="btn solid" disabled={!credentials.trim() || link.isPending} onClick={() => link.mutate()}>
                    {t(link.isPending ? 'account.music.checking' : 'account.music.link')}
                </button>
                <button type="button" className="btn" onClick={onClose}>{t('common.cancel')}</button>
            </div>
        </Dialog>
    );
}
