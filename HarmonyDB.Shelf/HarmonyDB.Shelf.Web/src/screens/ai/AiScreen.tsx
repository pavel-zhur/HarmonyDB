// "Your AI": connecting an assistant (Claude, ChatGPT, any with MCP) to the shelf — the MCP server's address to
// paste, what an assistant may be allowed (the MCP permissions the person has, in words), the assistants connected
// (who each is by its hosts, what it may do; changed or disconnected here), and keys for programs that cannot sign in.

import { useState } from 'react';
import { useMutation } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { errorText } from '../../api/errors';
import { Page } from '../../app/panes';
import { toast } from '../../app/toasts';
import { formatAgo, formatDate } from '../../i18n';
import {
    createPersonalKey, disconnectClient, revokePersonalKey, setClientPermissions, useMcpClients, useMcpOverview, usePersonalKeys,
    type ConnectedClient, type PersonalKey,
} from '../../data/mcp';
import { Confirm, Seg } from '../../kit/controls';
import { Icon } from '../../kit/Icon';
import { PermissionList, PermissionPicker, permissionTitle } from './permissions';
import { ClientIdentity, clientHost } from './client';
import './ai.css';

export function AiScreen() {
    const { t } = useTranslation();
    const overview = useMcpOverview();
    const grantable = overview.data?.grantable ?? [];
    return (
        <Page>
            <h1>{t('ai.title')}</h1>
            <p className="lead">{t('ai.lead')}</p>
            {overview.isError && <p className="failed">{errorText(overview.error)}</p>}
            {overview.data && (
                <div className="form">
                    <section>
                        <span className="lab"><Icon name="mcp" />{t('ai.address')}</span>
                        <Copyable text={overview.data.serverUrl} />
                        <p className="muted small ai-note">{t('ai.addressNote')}</p>
                    </section>
                    <section>
                        <span className="lab">{t('ai.mayTitle')}</span>
                        {grantable.length ? <PermissionList permissions={grantable} /> : <p className="muted">{t('ai.nothingGrantable')}</p>}
                        <p className="muted small ai-note">{t('ai.mayNote')}</p>
                    </section>
                    <Clients grantable={grantable} />
                    <Keys grantable={grantable} />
                </div>
            )}
        </Page>
    );
}

function Copyable({ text }: { text: string }) {
    const { t } = useTranslation();
    const copy = async () => {
        await navigator.clipboard.writeText(text);
        toast(t('common.copied'));
    };
    return <div className="linkbox"><code>{text}</code><button type="button" className="btn" onClick={() => void copy()}>{t('common.copy')}</button></div>;
}

function Clients({ grantable }: { grantable: string[] }) {
    const { t } = useTranslation();
    const clients = useMcpClients();
    return (
        <section>
            <span className="lab">{t('ai.clients.title')}</span>
            {clients.isError && <p className="failed">{errorText(clients.error)}</p>}
            {clients.data && !clients.data.length && <p className="muted">{t('ai.clients.none')}</p>}
            <div className="lines">{clients.data?.map(client => <Client key={client.id} client={client} grantable={grantable} />)}</div>
        </section>
    );
}

function Client({ client, grantable }: { client: ConnectedClient; grantable: string[] }) {
    const { t } = useTranslation();
    const [mode, setMode] = useState<'view' | 'edit' | 'disconnect'>('view');
    const [chosen, setChosen] = useState<string[]>(client.allowed);
    const save = useMutation({ mutationFn: setClientPermissions, onSuccess: () => setMode('view') });
    const remove = useMutation({ mutationFn: disconnectClient });
    const lost = client.allowed.filter(p => !client.inForce.includes(p));
    return (
        <div className="ai-item">
            <div className="ai-head">
                <Icon name="mcp" />
                <span className="ai-who">
                    <ClientIdentity idHost={client.clientIdHost ?? null} redirectHosts={client.redirectHosts} name={client.name} />
                    <span className="muted small">
                        {t('ai.clients.connected', { date: formatDate(client.connectedAt) })}
                        {client.lastUsedAt && ` · ${t('ai.clients.used', { ago: formatAgo(client.lastUsedAt) })}`}
                    </span>
                </span>
                {mode === 'view' && <span className="ai-acts">
                    <button type="button" className="btn sm" onClick={() => { setChosen(client.allowed); setMode('edit'); }}>{t('ai.clients.change')}</button>
                    <button type="button" className="btn sm dan" onClick={() => setMode('disconnect')}>{t('ai.clients.disconnect')}</button>
                </span>}
            </div>
            {mode !== 'edit' && <p className="ai-may">{client.inForce.map(permissionTitle).join(' · ') || t('ai.clients.nothing')}</p>}
            {mode !== 'edit' && lost.length > 0 && <p className="muted small">{t('ai.notInForce', { list: lost.map(permissionTitle).join(', ') })}</p>}
            {mode === 'edit' && (
                <div className="ai-edit">
                    <PermissionPicker grantable={grantable} chosen={chosen} onChange={setChosen} />
                    <p className="muted small">{t('ai.clients.atOnce')}</p>
                    <div className="chips">
                        <button type="button" className="btn pri" disabled={!chosen.length || save.isPending} onClick={() => save.mutate({ id: client.id, permissions: chosen })}>{t('common.save')}</button>
                        <button type="button" className="btn" onClick={() => setMode('view')}>{t('common.cancel')}</button>
                    </div>
                </div>
            )}
            {mode === 'disconnect' && (
                <Confirm text={t('ai.clients.disconnectAsk', { host: clientHost(client) })}
                    choices={[[t('ai.clients.disconnectYes'), () => remove.mutate(client.id)]]} onCancel={() => setMode('view')} />
            )}
        </div>
    );
}

const expiries: [string, number | null][] = [['never', null], ['30', 30], ['90', 90], ['365', 365]];

function Keys({ grantable }: { grantable: string[] }) {
    const { t } = useTranslation();
    const keys = usePersonalKeys();
    const [making, setMaking] = useState(false);
    const [made, setMade] = useState<string | null>(null);
    return (
        <section>
            <span className="lab">{t('ai.keys.title')}</span>
            <p className="muted small ai-note0">{t('ai.keys.why')}</p>
            {keys.isError && <p className="failed">{errorText(keys.error)}</p>}
            {made && (
                <div className="ai-made">
                    <Copyable text={made} />
                    <p className="muted small">{t('ai.keys.once')}</p>
                    <button type="button" className="btn" onClick={() => setMade(null)}>{t('ai.keys.copiedIt')}</button>
                </div>
            )}
            <div className="lines">{keys.data?.map(key => <Key key={key.id} item={key} />)}</div>
            {making
                ? <NewKey grantable={grantable} onDone={key => { setMaking(false); if (key) setMade(key); }} />
                : !made && (grantable.length
                    ? <p><button type="button" className="btn" onClick={() => setMaking(true)}>{t('ai.keys.create')}</button></p>
                    : <p className="muted small">{t('ai.keys.cannot')}</p>)}
        </section>
    );
}

function Key({ item }: { item: PersonalKey }) {
    const { t } = useTranslation();
    const [asking, setAsking] = useState(false);
    const revoke = useMutation({ mutationFn: revokePersonalKey });
    const lost = item.allowed.filter(p => !item.inForce.includes(p));
    return (
        <div className="ai-item">
            <div className="ai-head">
                <Icon name="gear" />
                <span className="ai-who">
                    <b>{item.name}</b>
                    <span className="muted small"><code>{item.prefix}…</code> · {t('ai.keys.made', { date: formatDate(item.createdAt) })}
                        {' · '}{item.expiresAt ? t('ai.keys.expires', { date: formatDate(item.expiresAt) }) : t('ai.keys.noExpiry')}
                        {item.lastUsedAt && ` · ${t('ai.clients.used', { ago: formatAgo(item.lastUsedAt) })}`}</span>
                </span>
                {!asking && <span className="ai-acts"><button type="button" className="btn sm dan" onClick={() => setAsking(true)}>{t('ai.keys.revoke')}</button></span>}
            </div>
            <p className="ai-may">{item.inForce.map(permissionTitle).join(' · ') || t('ai.clients.nothing')}</p>
            {lost.length > 0 && <p className="muted small">{t('ai.notInForce', { list: lost.map(permissionTitle).join(', ') })}</p>}
            {asking && <Confirm text={t('ai.keys.revokeAsk', { name: item.name })} choices={[[t('ai.keys.revokeYes'), () => revoke.mutate(item.id)]]} onCancel={() => setAsking(false)} />}
        </div>
    );
}

function NewKey({ grantable, onDone }: { grantable: string[]; onDone: (key: string | null) => void }) {
    const { t } = useTranslation();
    const [name, setName] = useState('');
    const [chosen, setChosen] = useState<string[]>([]);
    const [expiry, setExpiry] = useState('90');
    const create = useMutation({ mutationFn: createPersonalKey, onSuccess: r => onDone(r.key) });
    return (
        <form className="ai-edit" onSubmit={e => {
            e.preventDefault();
            if (name.trim() && chosen.length) create.mutate({ name: name.trim(), permissions: chosen, expiresInDays: expiries.find(x => x[0] === expiry)![1] });
        }}>
            <input className="field" value={name} onChange={e => setName(e.target.value)} maxLength={100} placeholder={t('ai.keys.name')} aria-label={t('ai.keys.name')} autoComplete="off" />
            <PermissionPicker grantable={grantable} chosen={chosen} onChange={setChosen} />
            <div className="setrow">
                <span>{t('ai.keys.expiry')}</span>
                <Seg value={expiry} options={expiries.map(([key]) => [key, t(`ai.keys.expiryChoice.${key}` as 'ai.keys.expiryChoice.never')])} onChange={setExpiry} />
            </div>
            <div className="chips">
                <button type="submit" className="btn pri" disabled={!name.trim() || !chosen.length || create.isPending}>{t('ai.keys.createIt')}</button>
                <button type="button" className="btn" onClick={() => onDone(null)}>{t('common.cancel')}</button>
            </div>
        </form>
    );
}
