// The ways I sign in: each sign-in of my profile (removed while another stays), and adding another of the providers
// the server has on — Telegram, Google (the dev provider on the local stand asks for the test name); Yandex is coming.
// Adding and removing need a recent sign-in: the server says so, and the toast offers signing in again.

import { useState } from 'react';
import { useMutation } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { errorText } from '../../api/errors';
import { formatDate } from '../../i18n';
import { addSignIn, useConfiguration } from '../../data/session';
import { removeLogin, useLogins, type LoginItem } from '../../data/account';
import { Confirm, Soon } from '../../kit/controls';
import { Icon } from '../../kit/Icon';
import { i18n } from '../../i18n';

const known = ['telegram', 'google', 'yandex', 'dev'] as const;
type Provider = (typeof known)[number];
export const isKnownProvider = (name: string): name is Provider => (known as readonly string[]).includes(name);
const isKnown = isKnownProvider;

/** A provider's name as the person knows it. */
export function providerName(provider: string): string {
    return isKnown(provider) ? i18n.t(`account.provider.${provider}`) : provider;
}

export function SignIns() {
    const { t } = useTranslation();
    const logins = useLogins();
    const { data: configuration } = useConfiguration();
    const on = (configuration?.signInProviders ?? []).map(x => x.name).filter(isKnown).filter(x => x !== 'yandex');
    const list = logins.data ?? [];
    return (
        <>
            <h2>{t('account.signIns.title')}</h2>
            {logins.isError && <p className="failed">{errorText(logins.error)}</p>}
            <div className="lines">
                {list.map(login => <Login key={login.id} login={login} last={list.length === 1} />)}
                {on.map(provider => provider === 'dev' ? <AddDev key={provider} /> : (
                    <div className="setrow" key={provider}>
                        <span>{t(`account.provider.${provider}`)}<span className="sub">{t('account.signIns.addSub')}</span></span>
                        <button type="button" className={provider === 'telegram' ? 'btn tg' : 'btn'} onClick={() => void addSignIn(provider)}>
                            {provider === 'telegram' && <Icon name="tg" />}{t('account.signIns.add')}
                        </button>
                    </div>
                ))}
                <div className="setrow pf-soon" aria-disabled="true">
                    <span>{t('account.provider.yandex')} <Soon /></span>
                </div>
            </div>
            <p className="muted small pf-note">{t('account.signIns.note')}</p>
        </>
    );
}

function Login({ login, last }: { login: LoginItem; last: boolean }) {
    const { t } = useTranslation();
    const [asking, setAsking] = useState(false);
    const remove = useMutation({ mutationFn: removeLogin, onSettled: () => setAsking(false) });
    const who = [login.displayName, login.username && login.username !== login.displayName ? `@${login.username}` : null, login.email].filter(Boolean).join(' · ');
    return (
        <div className="pf-login">
            <div className="setrow">
                <span className="pf-lname">
                    {login.pictureUrl ? <img className="av" src={login.pictureUrl} alt="" /> : <span className="pf-lic"><Icon name={login.provider === 'telegram' ? 'tg' : 'user'} /></span>}
                    <span>
                        {providerName(login.provider)}
                        <span className="sub">{who ? `${who} · ` : ''}{t('account.signIns.since', { date: formatDate(login.createdAt) })}</span>
                    </span>
                </span>
                {!last && !asking && <button type="button" className="btn" disabled={remove.isPending} onClick={() => setAsking(true)}>{t('account.signIns.remove')}</button>}
            </div>
            {asking && <Confirm text={t('account.signIns.removeAsk', { provider: providerName(login.provider) })}
                choices={[[t('account.signIns.removeYes'), () => remove.mutate(login.id)]]} onCancel={() => setAsking(false)} />}
        </div>
    );
}

/** The local stand's test provider: another test name added to this profile. */
function AddDev() {
    const { t } = useTranslation();
    const [name, setName] = useState('');
    const [busy, setBusy] = useState(false);
    const go = async () => {
        setBusy(true);
        await addSignIn('dev', name.trim());
        setBusy(false);
        setName('');
    };
    return (
        <div className="setrow">
            <span>{t('account.provider.dev')}<span className="sub">{t('account.signIns.addSub')}</span></span>
            <form className="inline-form pf-dev" onSubmit={e => { e.preventDefault(); if (name.trim()) void go(); }}>
                <input className="field" value={name} onChange={e => setName(e.target.value)} placeholder={t('account.signIns.devName')} aria-label={t('account.signIns.devName')} autoComplete="off" />
                <button type="submit" className="btn" disabled={busy || !name.trim()}>{t('account.signIns.add')}</button>
            </form>
        </div>
    );
}
