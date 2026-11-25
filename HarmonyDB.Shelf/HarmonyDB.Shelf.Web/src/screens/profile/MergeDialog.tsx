// A sign-in being added belongs to another profile: the two profiles side by side, each as a person knows it (name,
// picture, since when, its ways to sign in; mine with what it holds), which one stays and which goes into it, and the
// irreversible step asked twice in place. Nothing is merged without both yeses.

import { useState } from 'react';
import { useMutation } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { errorText } from '../../api/errors';
import { formatDate } from '../../i18n';
import { mergeAccounts, useLogins, useMergeCandidate } from '../../data/account';
import { closeMerge, useMergeQuestion } from '../../data/mergeQuestion';
import { useMyPlaylists } from '../../data/playlists';
import { useProfile } from '../../data/session';
import { Avatar } from '../../kit/controls';
import { Dialog } from '../../kit/Dialog';
import { useLocalView } from '../../local';
import { i18n } from '../../i18n';
import { providerName } from './SignIns';

export function MergeDialog() {
    const { t } = useTranslation();
    const state = useMergeQuestion();
    const question = useMergeCandidate(state);
    const { data: me } = useProfile();
    const logins = useLogins();
    const playlists = useMyPlaylists();
    const merge = useMutation({ mutationFn: mergeAccounts });
    const [sure, setSure] = useState(false);
    if (!state) return null;
    const other = question.data?.mergeCandidate;
    const close = () => {
        setSure(false);
        closeMerge();
    };
    const myProviders = [...new Set((logins.data ?? []).map(x => providerName(x.provider)))].join(', ');
    const view = useLocalView();
    // what each profile holds, said the same way on both cards
    const mine: Holdings | null = playlists.data && view.watermark !== null ? {
        ownedPlaylists: playlists.data.filter(p => p.myRole === 'owner' && p.systemKind === 'none').length,
        joinedPlaylists: playlists.data.filter(p => p.myRole && p.myRole !== 'owner').length,
        marks: Object.keys(view.marks).length,
        keys: Object.keys(view.keys).length,
        notes: Object.values(view.notes).filter(n => n.text).length,
    } : null;
    return (
        <Dialog label={t('account.merge.title')} onClose={close}>
            <div className="pf-dlg">
                <h1>{t('account.merge.title')}</h1>
                {question.isError && <p className="failed">{errorText(question.error)}</p>}
                {question.isPending && <p className="loading">{t('common.loading')}</p>}
                {other && me && (
                    <>
                        <p>{t('account.merge.text', { provider: providerName(question.data!.provider) })}</p>
                        <div className="pf-two">
                            <div className="pf-side">
                                <span className="pf-role">{t('account.merge.stays')}</span>
                                <div className="pf-other">
                                    <Avatar person={{ id: me.id, name: me.displayName, pictureUrl: me.pictureUrl }} />
                                    <span>
                                        <b>{me.displayName}</b>
                                        <span className="muted small">{t('account.merge.since', { date: formatDate(me.createdAt) })}</span>
                                        <span className="muted small">{myProviders}</span>
                                        {mine && <span className="muted small">{holdings(mine)}</span>}
                                    </span>
                                </div>
                            </div>
                            <div className="pf-side">
                                <span className="pf-role">{t('account.merge.goes')}</span>
                                <div className="pf-other">
                                    <Avatar person={{ id: `other:${other.displayName}`, name: other.displayName, pictureUrl: other.pictureUrl }} />
                                    <span>
                                        <b>{other.displayName}</b>
                                        <span className="muted small">{t('account.merge.since', { date: formatDate(other.createdAt) })}</span>
                                        <span className="muted small">{other.providers.map(providerName).join(', ')}</span>
                                        <span className="muted small">{holdings(other)}</span>
                                    </span>
                                </div>
                            </div>
                        </div>
                        <p className="muted small">{t('account.merge.what', { stays: me.displayName, goes: other.displayName })}</p>
                        {sure
                            ? (
                                <div className="confirm">
                                    <p>{t('account.merge.sure', { stays: me.displayName, goes: other.displayName })}</p>
                                    <div className="chips">
                                        <button type="button" className="btn dan" disabled={merge.isPending} onClick={() => merge.mutate(state, { onSuccess: () => setSure(false) })}>{t('account.merge.yes')}</button>
                                        <button type="button" className="btn" onClick={() => setSure(false)}>{t('common.cancel')}</button>
                                    </div>
                                </div>
                            )
                            : <button type="button" className="btn dan" onClick={() => setSure(true)}>{t('account.merge.ask')}</button>}
                    </>
                )}
                <button type="button" className="btn" onClick={close}>{t('account.merge.no')}</button>
            </div>
        </Dialog>
    );
}

type Holdings = { ownedPlaylists: number; joinedPlaylists: number; marks: number; keys: number; notes: number };

/** What a profile holds, in one line: "2 playlists of its own · 1 joined · 15 marks · 4 keys · no notes". */
function holdings(h: Holdings): string {
    const t = i18n.t.bind(i18n);
    return [
        t('account.merge.owned', { count: h.ownedPlaylists }),
        t('account.merge.joined', { count: h.joinedPlaylists }),
        t('account.merge.marks', { count: h.marks }),
        t('account.merge.keys', { count: h.keys }),
        t('account.merge.notes', { count: h.notes }),
    ].join(' · ');
}
