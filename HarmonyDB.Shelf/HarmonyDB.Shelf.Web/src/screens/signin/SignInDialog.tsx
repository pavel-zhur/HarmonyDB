// Signing in: the providers the server has on (its public configuration), each its button; the dev provider (the
// local stand only) asks for the test person's name. Opened by offerSignIn() — from the header, the tabs, or a guest's
// writing action (then it says what for, and the action is done after signing in).

import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Dialog } from '../../kit/Dialog';
import { Icon } from '../../kit/Icon';
import { closeSignIn, forgetPending, useSignInOffer } from '../../data/signInOffer';
import { signIn, useConfiguration } from '../../data/session';
import { en } from '../../i18n/en';

const named = Object.keys(en.signIn.with) as (keyof typeof en.signIn.with)[];

export function SignInDialog() {
    const offer = useSignInOffer();
    if (!offer) return null;
    return <SignInBox what={offer.what} />;
}

function SignInBox({ what }: { what: string | null }) {
    const { t } = useTranslation();
    const { data: configuration } = useConfiguration();
    const [devName, setDevName] = useState('');
    const [busy, setBusy] = useState(false);
    const close = () => {
        forgetPending();
        closeSignIn();
    };
    const go = async (provider: string, name?: string) => {
        setBusy(true);
        await signIn(provider, name);
        setBusy(false);
    };
    const providers = (configuration?.signInProviders ?? []).map(x => x.name).filter((x): x is (typeof named)[number] => named.includes(x as never));

    return (
        <Dialog label={t('signIn.title')} onClose={close}>
            <h1>{t('signIn.title')}</h1>
            <p>{what ? t('signIn.why', { what: t(`signIn.what.${what}` as 'signIn.what.mark') }) : t('signIn.default')}</p>
            {providers.length > 0 && <p className="muted small">{t('signIn.noPassword')}</p>}
            {configuration && providers.length === 0 && <p className="muted">{t('signIn.none')}</p>}
            {providers.map(provider => provider === 'dev' ? (
                <form key={provider} className="row2" aria-label={t('signIn.with.dev')} onSubmit={e => { e.preventDefault(); if (devName.trim()) void go('dev', devName.trim()); }}>
                    <input className="field" value={devName} onChange={e => setDevName(e.target.value)} placeholder={t('signIn.devName')} aria-label={t('signIn.devName')} autoComplete="off" />
                    <button type="submit" className="btn solid" disabled={busy || !devName.trim()}>{t('nav.signIn')}</button>
                </form>
            ) : (
                <button key={provider} type="button" className={provider === 'telegram' ? 'btn solid tg' : 'btn solid'} disabled={busy} onClick={() => void go(provider)}>
                    {provider === 'telegram' && <Icon name="tg" />}{t(`signIn.with.${provider}`)}
                </button>
            ))}
            <button type="button" className="btn" onClick={close}>{t('signIn.notNow')}</button>
        </Dialog>
    );
}
