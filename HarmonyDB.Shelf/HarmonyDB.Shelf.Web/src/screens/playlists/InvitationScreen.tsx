// An invitation's link: who invites, where, as whom, till when; "Join" (a guest signs in first, then joins). A member
// already (just joined — also when the joining was done after a sign-in — or long ago) is taken to the playlist.
// /invitations/:token

import { Navigate, useNavigate, useParams } from 'react-router';
import { useMutation } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { errorText } from '../../api/errors';
import { Page } from '../../app/panes';
import { formatDate } from '../../i18n';
import { acceptInvitation, playlistTitle, useInvitation } from '../../data/playlists';
import { Icon } from '../../kit/Icon';

export function InvitationScreen() {
    const { t } = useTranslation();
    const { token } = useParams() as { token: string };
    const navigate = useNavigate();
    const invitation = useInvitation(token);
    const accept = useMutation({ mutationFn: acceptInvitation, onSuccess: r => navigate(`/playlists/${r.value.playlist.id}`) });
    const v = invitation.data;
    if (v?.alreadyMember) return <Navigate to={`/playlists/${v.playlistId}`} replace />;
    return (
        <Page>
            <div className="locked">
                <span className="lockic"><Icon name="users" /></span>
                <h1>{t('invitation.title')}</h1>
                {invitation.isError && <p className="failed">{errorText(invitation.error)}</p>}
                {v && (
                    <>
                        <p>
                            {v.invitedBy && !v.invitedBy.gone ? t('invitation.text', { name: v.invitedBy.name, title: playlistTitle({ title: v.playlistTitle, systemKind: v.systemKind }) })
                                : t('invitation.textNoName', { title: playlistTitle({ title: v.playlistTitle, systemKind: v.systemKind }) })}
                            {' '}{t('invitation.as', { role: t(`playlist.role.${v.role}`).toLowerCase() })}
                        </p>
                        <p className="muted small">{t('invitation.until', { date: formatDate(v.expiresAt) })}</p>
                        <button type="button" className="btn pri" disabled={accept.isPending} onClick={() => accept.mutate(token)}>{t('invitation.accept')}</button>
                    </>
                )}
            </div>
        </Page>
    );
}
