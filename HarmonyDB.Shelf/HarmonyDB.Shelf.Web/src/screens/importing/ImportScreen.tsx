// Import of chords: everything imported goes to the playlist "Imported chords". Links work now (a job: the queue below
// stays while one is away from the page); files, a photo, a message to the bot are "soon"; chords from a recording are
// the analysis's ("soon"); writing by hand is the editor.

import { useState } from 'react';
import { Link } from 'react-router';
import { useMutation } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { ApiError } from '../../api/client';
import { errorText } from '../../api/errors';
import { Page } from '../../app/panes';
import { formatAgo } from '../../i18n';
import { importLinks, resultOf, unfinished, useJobs, type JobInfo } from '../../data/jobs';
import { playlistTitle, useMyPlaylists } from '../../data/playlists';
import { Soon } from '../../kit/controls';
import { Icon, type IconName } from '../../kit/Icon';
import './importing.css';

/** How many links one import takes. */
const maxLinks = 100;

export function ImportScreen() {
    const { t } = useTranslation();
    const playlists = useMyPlaylists();
    const imported = playlists.data?.find(p => p.systemKind === 'importedChords' && p.myRole === 'owner') ?? null;
    return (
        <Page>
            <h1>{t('importing.title')}</h1>
            <p className="lead">
                {t('importing.lead')}{' '}
                {imported && <Link className="lnk im-pl" to={`/playlists/${imported.id}`}>{t('importing.playlist', { title: playlistTitle(imported), count: imported.songCount })}</Link>}
            </p>
            <div className="im-grid">
                <Links />
                <SoonCard icon="file" title={t('importing.files')} text={t('importing.filesText')} />
                <SoonCard icon="cam" title={t('importing.photo')} text={t('importing.photoText')} />
                <SoonCard icon="tg" title={t('importing.telegram')} text={t('importing.telegramText')} />
                <SoonCard icon="ai" title={t('importing.audio')} text={t('importing.audioText')} />
                <div className="im-card">
                    <b><Icon name="pen" />{t('importing.byHand')}</b>
                    <p className="muted small">{t('importing.byHandText')}</p>
                    <div><Link className="btn" to="/editor"><Icon name="pen" />{t('importing.byHandGo')}</Link></div>
                </div>
            </div>
            <Queue />
        </Page>
    );
}

function Links() {
    const { t } = useTranslation();
    const [text, setText] = useState('');
    // one link a line
    const links = text.split('\n').map(x => x.trim()).filter(Boolean);
    const run = useMutation({ mutationFn: importLinks, onSuccess: () => setText('') });
    return (
        <div className="im-card">
            <b><Icon name="link" />{t('importing.links')}</b>
            <p className="muted small">{t('importing.linksText', { max: maxLinks })}</p>
            <textarea className="field im-links" value={text} onChange={e => setText(e.target.value)} spellCheck={false} placeholder={'https://…\nhttps://…'} aria-label={t('importing.links')} />
            {links.length > maxLinks && <p className="failed">{t('importing.tooMany', { max: maxLinks })}</p>}
            <div><button type="button" className="btn pri" disabled={!links.length || links.length > maxLinks || run.isPending} onClick={() => run.mutate(links)}>
                <Icon name="imp" />{t('importing.go')}
            </button></div>
        </div>
    );
}

function SoonCard({ icon, title, text }: { icon: IconName; title: string; text: string }) {
    return (
        <div className="im-card im-soon" aria-disabled="true">
            <b><Icon name={icon} />{title} <Soon /></b>
            <p className="muted small">{text}</p>
        </div>
    );
}

/** The imports: each a job of links; done, each link with what became of it. */
function Queue() {
    const { t } = useTranslation();
    const jobs = useJobs('import');
    const list = jobs.data ?? [];
    const busy = list.filter(unfinished).length;
    return (
        <>
            <h2>{t('importing.queue')}{busy > 0 && ` · ${t('importing.busy', { count: busy })}`}</h2>
            {jobs.isError && <p className="failed">{errorText(jobs.error)}</p>}
            {jobs.data && !list.length && <p className="muted">{t('importing.empty')}</p>}
            <div className="lines im-queue">{list.map(job => <Job key={job.id} job={job} />)}</div>
        </>
    );
}

const stateIcon: Record<string, IconName> = { imported: 'check', notFound: 'x', unavailable: 'clock', notALink: 'x' };

function Job({ job }: { job: JobInfo }) {
    const { t } = useTranslation();
    const result = resultOf(job, 'import');
    const imported = result?.links.filter(x => x.state === 'imported').length ?? 0;
    // done: all in, some in, none in
    const icon: IconName = !result ? (job.status === 'failed' || job.status === 'cancelled' ? 'x' : 'loop') : imported === result.links.length ? 'check' : imported ? 'info' : 'x';
    const tone = !result ? job.status : imported === result.links.length ? 'succeeded' : imported ? 'partly' : 'failed';
    return (
        <div className="im-job">
            <div className="im-row">
                <span className={`im-i ${tone}`}><Icon name={icon} className={unfinished(job) ? 'spin' : undefined} /></span>
                <span className="im-t">
                    <span>{result ? t('importing.done', { n: imported, total: result.links.length }) : t(`importing.status.${job.status}`)}</span>
                    <span className="muted small">
                        {unfinished(job) && job.stage ? `${t(`importing.stage.${job.stage}` as 'importing.stage.resolving')}${job.progress != null ? ` · ${Math.round(job.progress * 100)}%` : ''} · ` : ''}
                        {job.status === 'failed' && job.errorCode ? `${errorText(new ApiError(0, job.errorCode))} · ` : ''}
                        {formatAgo(job.createdAt)}
                    </span>
                </span>
            </div>
            {result && result.links.map(link => (
                <div className="im-row im-link" key={link.link}>
                    <span className={`im-i ${link.state}`}><Icon name={stateIcon[link.state] ?? 'x'} /></span>
                    <span className="im-t">
                        <span className="im-url">{link.link}</span>
                        <span className="muted small">{t(`importing.linkState.${link.state}`)}</span>
                    </span>
                    {link.state === 'imported' && link.songId && <Link className="btn sm" to={`/playlists/${result.playlistId}/songs/${link.songId}`}>{t('common.open')}</Link>}
                </div>
            ))}
        </div>
    );
}
