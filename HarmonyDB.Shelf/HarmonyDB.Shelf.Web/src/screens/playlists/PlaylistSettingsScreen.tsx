// A playlist's settings: for me (in my library), the recordings (where to look first), access (who sees, who adds),
// the members with their contacts and roles, the link and QR code (a playlist open by link), the invitations, mine in
// it (clear), and leaving or deleting. /playlists/:id/settings. Settings only admins change are shown to all, still.

import { useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router';
import { useMutation, useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { errorText } from '../../api/errors';
import { Page } from '../../app/panes';
import { formatDate } from '../../i18n';
import {
    clearMine, createInvitation, deletePlaylist, leavePlaylist, passOwnership, playlistLink, playlistTitle, removeMember, revokeInvitation,
    setMemberRole, showInLibrary, updatePlaylist, usePlaylist, type PlaylistDetails, type PlaylistRole,
} from '../../data/playlists';
import { useSession } from '../../data/session';
import { Avatar, Confirm, Seg, Switch } from '../../kit/controls';
import { Icon } from '../../kit/Icon';
import './playlists.css';
import { MenuItem, Popover } from '../../kit/Popover';
import { LinkBox } from '../../kit/LinkBox';
import { personName } from '../../kit/person';

export function PlaylistSettingsScreen() {
    const { t } = useTranslation();
    const { id } = useParams() as { id: string };
    const playlist = usePlaylist(id, null, true);
    return (
        <Page>
            <p><Link className="btn" to={`/playlists/${id}`}><Icon name="left" />{t('playlist.toPlaylist')}</Link></p>
            {playlist.isError && <p className="failed">{errorText(playlist.error)}</p>}
            {playlist.data && <Settings details={playlist.data} />}
        </Page>
    );
}

function Settings({ details }: { details: PlaylistDetails }) {
    const { t } = useTranslation();
    const p = details.playlist;
    const admin = details.canAdminister;
    const system = p.systemKind !== 'none';
    const update = useMutation({ mutationFn: updatePlaylist });
    const toggle = useMutation({ mutationFn: showInLibrary });
    const shown = toggle.isPending ? toggle.variables.show : p.showInLibrary ?? false;

    return (
        <div className="form">
            <h1>{playlistTitle(p)}</h1>
            {admin && !system && <Rename details={details} />}

            <section>
                <span className="lab">{t('playlist.forMe')}</span>
                <div className="setrow">
                    <span>{t('playlist.showInLibrary')}<span className="sub">{t('playlist.showInLibrarySub')}</span></span>
                    <Switch on={shown} label={t('playlist.showInLibrary')} onChange={show => toggle.mutate({ id: p.id, show })} />
                </div>
            </section>

            {!system && (
                <section>
                    <span className="lab">{t('playlist.recordings')}</span>
                    <div className="setrow">
                        <span>{t('playlist.searchFirst')}<span className="sub">{t('playlist.searchFirstSub')}</span></span>
                        <Seg value={p.searchFirst ?? 'yandex'} disabled={!admin}
                            options={[['yandex', t('playlist.service.yandex')], ['youtube', t('playlist.service.youtube')]]}
                            onChange={searchFirst => update.mutate({ id: p.id, patch: { searchFirst } })} />
                    </div>
                </section>
            )}

            {!system && (
                <section>
                    <span className="lab">{t('playlist.accessTitle')}</span>
                    <div className="lines">
                        <div className="setrow">
                            <span>{t('playlist.whoSees')}</span>
                            <Seg value={p.access} disabled={!admin}
                                options={(['private', 'link', 'public'] as const).map(a => [a, t(`playlist.whoSeesChoice.${a}`)])}
                                onChange={access => update.mutate({ id: p.id, patch: { access } })} />
                        </div>
                        <div className="setrow">
                            <span>{t('playlist.whoAdds')}</span>
                            <Seg value={p.addPolicy} disabled={!admin}
                                options={(['allMembers', 'admins'] as const).map(a => [a, t(`playlist.whoAddsChoice.${a}`)])}
                                onChange={addPolicy => update.mutate({ id: p.id, patch: { addPolicy } })} />
                        </div>
                        <div className="setrow muted small">{t('playlist.accessNote')}{admin ? '' : ` ${t('playlist.adminsChange')}`}</div>
                    </div>
                </section>
            )}

            {!system && <Members details={details} />}
            {p.access !== 'private' && !system && <ViewLink details={details} />}
            {admin && !system && <Invitations details={details} />}
            {p.myRole && <Mine details={details} />}
            {p.myRole && <LeaveOrDelete details={details} />}
        </div>
    );
}

function Rename({ details }: { details: PlaylistDetails }) {
    const { t } = useTranslation();
    const p = details.playlist;
    const [title, setTitle] = useState(p.title);
    const save = useMutation({ mutationFn: updatePlaylist });
    const changed = title.trim() !== '' && title.trim() !== p.title;
    return (
        <form className="inline-form" onSubmit={e => { e.preventDefault(); if (changed) save.mutate({ id: p.id, patch: { title: title.trim() } }); }}>
            <input className="field" value={title} onChange={e => setTitle(e.target.value)} aria-label={t('playlist.title')} placeholder={t('playlist.title')} />
            <button type="submit" className="btn" disabled={!changed || save.isPending}>{t('common.save')}</button>
        </form>
    );
}

function Members({ details }: { details: PlaylistDetails }) {
    const { t } = useTranslation();
    const { accountId } = useSession();
    const owner = details.playlist.myRole === 'owner';
    return (
        <section>
            <span className="lab">{t('playlist.members')}</span>
            <div className="lines">
                {details.members.map(m => (
                    <div className="mem" key={m.person.id}>
                        <Avatar person={m.person} />
                        <span className="n">
                            {m.person.id === accountId ? t('common.me') : personName(m.person)}
                            {m.contacts.telegram.map(name => (
                                <a key={name} className="contact" href={`https://t.me/${encodeURIComponent(name)}`} target="_blank" rel="noreferrer">@{name}</a>
                            ))}
                        </span>
                        {details.canAdminister && m.role !== 'owner' && m.person.id !== accountId
                            ? <MemberMenu details={details} member={m} canPass={owner} />
                            : <span className="muted small">{t(`playlist.role.${m.role}`)}</span>}
                    </div>
                ))}
            </div>
        </section>
    );
}

function MemberMenu({ details, member, canPass }: { details: PlaylistDetails; member: PlaylistDetails['members'][number]; canPass: boolean }) {
    const { t } = useTranslation();
    const [anchor, setAnchor] = useState<HTMLElement | null>(null);
    const [ask, setAsk] = useState<'remove' | 'pass' | null>(null);
    const id = details.playlist.id, accountId = member.person.id, name = member.person.name, gone = member.person.gone;
    const close = () => setAnchor(null);
    if (ask)
        return (
            <Confirm text={ask === 'remove' ? (gone ? t('playlist.removeGoneConfirm') : t('playlist.removeConfirm', { name })) : t('playlist.passConfirm', { name, title: playlistTitle(details.playlist) })}
                choices={[[ask === 'remove' ? t('playlist.remove') : t('playlist.passOwnership'), () => {
                    setAsk(null);
                    void (ask === 'remove' ? removeMember({ id, accountId }) : passOwnership({ id, accountId })).catch(() => undefined);
                }]]}
                onCancel={() => setAsk(null)} />
        );
    return (
        <>
            <button type="button" className="chip" onClick={e => setAnchor(anchor ? null : e.currentTarget)}>{t(`playlist.role.${member.role}`)}<Icon name="down" /></button>
            <Popover anchor={anchor} onClose={close} width={260}>
                <MenuItem on={member.role === 'member'} onClick={() => { close(); void setMemberRole({ id, accountId, role: 'member' }).catch(() => undefined); }}>{t('playlist.role.member')}</MenuItem>
                <MenuItem on={member.role === 'admin'} onClick={() => { close(); void setMemberRole({ id, accountId, role: 'admin' }).catch(() => undefined); }}>{t('playlist.role.admin')}</MenuItem>
                <hr />
                {/* a profile deleted takes nothing on */}
                {canPass && !gone && <MenuItem onClick={() => { close(); setAsk('pass'); }}>{t('playlist.passOwnership')}</MenuItem>}
                <MenuItem onClick={() => { close(); setAsk('remove'); }}><span className="danger">{t('playlist.remove')}</span></MenuItem>
            </Popover>
        </>
    );
}

/** The playlist's own address: anyone opens a public one; one open by link is opened by its link. */
function ViewLink({ details }: { details: PlaylistDetails }) {
    const { t } = useTranslation();
    const p = details.playlist;
    // the link of a playlist open by link is one and the same each time it is asked (made the first time)
    const link = useQuery({
        queryKey: ['playlist-link', p.id, p.access],
        queryFn: () => (p.access === 'public' ? Promise.resolve(`${location.origin}/playlists/${p.id}`) : playlistLink(p.id)),
        staleTime: Infinity,
    });
    return (
        <section>
            <span className="lab">{t('playlist.viewLink')}</span>
            {link.data && <LinkBox link={link.data} note={t('playlist.viewLinkNote')} />}
            {link.isError && <p className="failed">{errorText(link.error)}</p>}
        </section>
    );
}

function Invitations({ details }: { details: PlaylistDetails }) {
    const { t } = useTranslation();
    const id = details.playlist.id;
    const [role, setRole] = useState<PlaylistRole>('member');
    const [days, setDays] = useState(7);
    const create = useMutation({ mutationFn: createInvitation });
    const live = details.invitations.filter(x => !x.revokedAt && new Date(x.expiresAt).getTime() > Date.now() && (x.maxUses == null || x.uses < x.maxUses));
    return (
        <section>
            <span className="lab">{t('playlist.invitations')}</span>
            <p className="muted small" style={{ margin: '0 0 10px' }}>{t('playlist.invitationsNote')}</p>
            <div className="invite">
                <span>{t('playlist.inviteAs')}</span>
                <Seg value={role} options={[['member', t('playlist.role.member')], ['admin', t('playlist.role.admin')]]} onChange={setRole} />
                <span>{t('playlist.expiresIn')}</span>
                <Seg value={String(days)} options={[1, 7, 30].map(d => [String(d), t('playlist.days', { count: d })])} onChange={d => setDays(Number(d))} />
                <button type="button" className="btn pri" disabled={create.isPending} onClick={() => create.mutate({ id, role, expiresInDays: days, maxUses: null })}>
                    <Icon name="plus" />{t('playlist.invite')}
                </button>
            </div>
            {live.map(x => {
                const link = `${location.origin}/invitations/${x.token}`;
                return (
                    <div className="invitation" key={x.token}>
                        <div className="setrow">
                            <span>
                                {t(`playlist.role.${x.role}`)} · {t('playlist.expires', { date: formatDate(x.expiresAt) })}
                                <span className="sub">{x.maxUses != null ? t('playlist.uses', { uses: x.uses, max: x.maxUses }) : t('playlist.usesUnlimited', { count: x.uses })}</span>
                            </span>
                            <button type="button" className="btn dan sm" onClick={() => void revokeInvitation({ id, token: x.token }).catch(() => undefined)}>{t('playlist.revoke')}</button>
                        </div>
                        <LinkBox link={link} />
                    </div>
                );
            })}
        </section>
    );
}

function Mine({ details }: { details: PlaylistDetails }) {
    const { t } = useTranslation();
    const [ask, setAsk] = useState(false);
    const id = details.playlist.id;
    const run = (added: boolean) => {
        setAsk(false);
        void clearMine({ id, added }).catch(() => undefined);
    };
    return (
        <section>
            <span className="lab">{t('playlist.mine')}</span>
            {ask
                ? <Confirm text={t('playlist.clearConfirm')} choices={[[t('playlist.clearKeysNotes'), () => run(false)], [t('playlist.clearAdded'), () => run(true)]]} onCancel={() => setAsk(false)} />
                : <button type="button" className="btn" onClick={() => setAsk(true)}>{t('playlist.clear')}</button>}
        </section>
    );
}

function LeaveOrDelete({ details }: { details: PlaylistDetails }) {
    const { t } = useTranslation();
    const navigate = useNavigate();
    const [ask, setAsk] = useState(false);
    const p = details.playlist;
    const title = playlistTitle(p);
    if (p.systemKind !== 'none')
        return <p className="muted small">{t(`playlists.systemNote.${p.systemKind}`)} {t('playlists.systemNote.always')}</p>;
    const owner = p.myRole === 'owner';
    const go = () => {
        setAsk(false);
        // off the playlist's pages first: what is gone is not asked again
        navigate('/playlists');
        void (owner ? deletePlaylist(p.id) : leavePlaylist(p.id)).catch(() => undefined);
    };
    return (
        <section>
            {ask
                ? <Confirm text={owner ? t('playlist.deleteConfirm', { title }) : t('playlist.leaveConfirm', { title })}
                    choices={[[owner ? t('playlist.deleteYes') : t('playlist.leaveYes'), go]]} onCancel={() => setAsk(false)} />
                : <button type="button" className="btn dan" onClick={() => setAsk(true)}>{owner && <Icon name="trash" />}{owner ? t('playlist.delete') : t('playlist.leave')}</button>}
        </section>
    );
}
