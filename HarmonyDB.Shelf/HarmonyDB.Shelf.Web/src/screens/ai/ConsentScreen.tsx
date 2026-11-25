// The consent of an assistant's sign-in (/connect?request=<id>; /connect?error=<code> when the server would not send
// the browser back to the client). The server's /oauth/authorize sends the person here.
//
// A security boundary: who asks is shown by its own name — which anyone may take — and, plainly, by the hosts that tell
// it apart (where the answer goes back to; the address it is known by), with a warning for one that goes back to this
// computer only. Nothing is ticked beforehand: the person ticks what to allow (one tap ticks what the assistant asked
// for) and decides with a press; the answer sends the browser back to the assistant. The page is never framed (the
// server's headers).

import { useState } from 'react';
import { useSearchParams } from 'react-router';
import { useMutation } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { errorText } from '../../api/errors';
import { ApiError } from '../../api/client';
import { Page } from '../../app/panes';
import { approveRequest, denyRequest, useAuthorizationRequest, type AuthorizationRequestView } from '../../data/mcp';
import { useProfile, useSession } from '../../data/session';
import { SignOutButton } from '../profile/SignOut';
import { offerSignIn } from '../../data/signInOffer';
import { Icon } from '../../kit/Icon';
import { ClientIdentity } from './client';
import { PermissionPicker } from './permissions';
import './ai.css';

export function ConsentScreen() {
    const { t } = useTranslation();
    const [params] = useSearchParams();
    const { known, signedIn } = useSession();
    const error = params.get('error');
    const request = params.get('request');

    if (error || !request) {
        return (
            <Page>
                <div className="locked">
                    <span className="lockic"><Icon name="mcp" /></span>
                    <h1>{t('ai.consent.cannot')}</h1>
                    <p className="muted">{errorText(new ApiError(400, error ?? 'authorization_request_invalid'))}</p>
                    <p className="muted small">{t('ai.consent.again')}</p>
                </div>
            </Page>
        );
    }
    if (!known) return null;
    if (!signedIn) {
        return (
            <Page>
                <div className="locked">
                    <span className="lockic"><Icon name="mcp" /></span>
                    <h1>{t('ai.consent.title')}</h1>
                    <p className="muted">{t('ai.consent.signInFirst')}</p>
                    <button type="button" className="btn pri" onClick={() => offerSignIn()}>{t('nav.signIn')}</button>
                </div>
            </Page>
        );
    }
    return <Consent id={request} />;
}

function Consent({ id }: { id: string }) {
    const { t } = useTranslation();
    const view = useAuthorizationRequest(id);
    const [leaving, setLeaving] = useState<string | null>(null);
    if (leaving) return <Page><p className="loading">{t('ai.consent.goingBack', { host: leaving })}</p></Page>;
    return (
        <Page>
            {view.isError && (
                <div className="locked">
                    <span className="lockic"><Icon name="mcp" /></span>
                    <h1>{t('ai.consent.cannot')}</h1>
                    <p className="muted">{errorText(view.error)}</p>
                    <p className="muted small">{t('ai.consent.again')}</p>
                </div>
            )}
            {view.isPending && <p className="loading">{t('common.loading')}</p>}
            {view.data && <Decide id={id} view={view.data} onLeave={setLeaving} />}
        </Page>
    );
}

function Decide({ id, view, onLeave }: { id: string; view: AuthorizationRequestView; onLeave: (host: string) => void }) {
    const { t } = useTranslation();
    const { data: me } = useProfile();
    const client = view.client;
    const [chosen, setChosen] = useState<string[]>([]);
    const go = (url: string) => {
        onLeave(client.redirectHost);
        location.assign(url);
    };
    const approve = useMutation({ mutationFn: () => approveRequest(id, chosen), onSuccess: r => go(r.redirectUrl) });
    const deny = useMutation({ mutationFn: () => denyRequest(id), onSuccess: r => go(r.redirectUrl) });
    const busy = approve.isPending || deny.isPending;
    const asked = view.requested.filter(p => view.grantable.includes(p));
    const notYours = view.requested.filter(p => !view.grantable.includes(p));

    return (
        <div className="form ai-consent">
            <h1>{t('ai.consent.title')}</h1>
            <div className="ai-client">
                <Icon name="mcp" />
                <span className="ai-who">
                    <ClientIdentity idHost={client.clientIdHost ?? null} redirectHosts={[client.redirectHost]} name={client.name} />
                    <span className="muted small">{t(client.kind === 'metadataDocument' ? 'ai.consent.byDocument' : 'ai.consent.registered')}</span>
                </span>
            </div>
            <p className="muted small">{t('ai.consent.nameNote')}</p>
            {client.localOnly && <p className="ai-warn"><Icon name="info" />{t('ai.consent.localOnly')}</p>}
            {view.allowed && <p className="muted small">{t('ai.consent.connectedBefore')}</p>}

            <section>
                <span className="lab">{t('ai.consent.allow')}</span>
                {view.grantable.length === 0
                    ? <p className="muted">{t('ai.nothingGrantable')}</p>
                    : <PermissionPicker grantable={view.grantable} chosen={chosen} onChange={setChosen} asked={asked} off={notYours} />}
                {asked.length > 0 && !asked.every(p => chosen.includes(p)) && (
                    <p><button type="button" className="lnk" onClick={() => setChosen(view.grantable.filter(p => asked.includes(p) || chosen.includes(p)))}>{t('ai.consent.tickAsked')}</button></p>
                )}
            </section>

            {me && <p className="muted small">{t('ai.consent.as', { name: me.displayName })} <SignOutButton className="lnk ai-inl" label={t('ai.consent.notMe')} /></p>}
            {(approve.isError || deny.isError) && <p className="failed">{errorText(approve.error ?? deny.error)}</p>}
            <div className="chips ai-decide">
                <button type="button" className="btn pri" disabled={!chosen.length || busy} onClick={() => approve.mutate()}>{t('ai.consent.yes')}</button>
                <button type="button" className="btn" disabled={busy} onClick={() => deny.mutate()}>{t('ai.consent.no')}</button>
            </div>
            <p className="muted small">{t('ai.consent.later')}</p>
        </div>
    );
}
