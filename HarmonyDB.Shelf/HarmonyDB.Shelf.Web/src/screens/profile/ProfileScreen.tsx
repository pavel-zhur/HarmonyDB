// The profile: who I am (name and picture from the sign-in the profile was made with) and signing out; the ways I
// sign in (adding another, merging when it is another profile's, removing one); the settings — the music accounts,
// the chords' defaults, the bot (soon), the sizes, the theme, the language; what I have; clearing and deleting.

import { useState } from 'react';
import { useNavigate } from 'react-router';
import { useMutation } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Page } from '../../app/panes';
import { clampScale } from '../../app/appearance';
import { useProfile } from '../../data/session';
import { SignOutButton } from './SignOut';
import { defaultSettings, updateSettings, useSettings } from '../../data/settings';
import { deleteProfile, useLogins, wipeProfile } from '../../data/account';
import { Avatar, Confirm, Seg, Soon, Stepper, Switch } from '../../kit/controls';
import { locales } from '../../i18n';
import { errorText } from '../../api/errors';
import { ChordSettings } from '../song/ChordSettings';
import { SignIns, isKnownProvider } from './SignIns';
import { MusicAccounts } from './MusicAccounts';
import { MineCounts } from './MineCounts';
import { MergeDialog } from './MergeDialog';
import './profile.css';

export function ProfileScreen() {
    const { t } = useTranslation();
    const { data: profile, error } = useProfile();
    const logins = useLogins();
    const settings = useSettings();
    const percent = (value: number) => `${Math.round(value * 100)}%`;
    // the profile takes its name and picture from the sign-in it was made with: the first one
    const first = logins.data?.reduce<typeof logins.data[number] | null>((a, b) => (!a || b.createdAt < a.createdAt ? b : a), null) ?? null;
    return (
        <Page>
            <h1>{t('profile.title')}</h1>
            {error && <p className="failed">{errorText(error)}</p>}
            {profile && (
                <div className="me">
                    <Avatar person={{ id: profile.id, name: profile.displayName, pictureUrl: profile.pictureUrl }} size="l" />
                    <span className="pf-who">
                        <b>{profile.displayName}</b>
                        {first && isKnownProvider(first.provider) && <span className="muted small">{t(`account.nameFrom.${first.provider}`)}</span>}
                    </span>
                    <SignOutButton />
                </div>
            )}

            <SignIns />

            <h2>{t('profile.settings')}</h2>
            <div className="lines">
                <MusicAccounts />
                <ChordSettings />
                <div className="setrow">
                    <span>{t('account.bot')} <Soon /><span className="sub">{t('account.botSub')}</span></span>
                    <Switch on={false} disabled label={t('account.bot')} onChange={() => undefined} />
                </div>
                <div className="setrow">
                    <span>{t('profile.interfaceSize')}<span className="sub">{t('profile.interfaceSizeSub')}</span></span>
                    <Stepper value={percent(settings.interfaceScale)} lessLabel={t('profile.smaller')} moreLabel={t('profile.bigger')} resetLabel={t('profile.reset')}
                        onStep={d => updateSettings({ interfaceScale: clampScale('interface', settings.interfaceScale + d * 0.1) })}
                        onReset={() => updateSettings({ interfaceScale: defaultSettings.interfaceScale })} />
                </div>
                <div className="setrow">
                    <span>{t('profile.chordsSize')}<span className="sub">{t('profile.chordsSizeSub')}</span></span>
                    <Stepper value={percent(settings.chordsScale)} lessLabel={t('profile.smaller')} moreLabel={t('profile.bigger')} resetLabel={t('profile.reset')}
                        onStep={d => updateSettings({ chordsScale: clampScale('chords', settings.chordsScale + d * 0.1) })}
                        onReset={() => updateSettings({ chordsScale: defaultSettings.chordsScale })} />
                </div>
                <div className="setrow">
                    <span>{t('profile.theme')}</span>
                    <Seg value={settings.theme} options={(['system', 'light', 'dark'] as const).map(x => [x, t(`profile.themeChoice.${x}`)])}
                        onChange={theme => updateSettings({ theme })} />
                </div>
                <div className="setrow">
                    <span>{t('profile.language')}</span>
                    <Seg value={settings.locale ?? 'system'} options={[['system', t('profile.languageChoice.system')], ...locales.map(l => [l, t(`profile.languageChoice.${l}`)] as [string, string])]}
                        onChange={locale => updateSettings({ locale: locale === 'system' ? null : locale })} />
                </div>
            </div>

            <MineCounts />
            <Danger />
            <MergeDialog />
        </Page>
    );
}

/** Clearing the profile (it stays, empty) and deleting it: each asked again in place. */
function Danger() {
    const { t } = useTranslation();
    const navigate = useNavigate();
    const [asking, setAsking] = useState<'wipe' | 'delete' | null>(null);
    const wipe = useMutation({ mutationFn: wipeProfile, onSettled: () => setAsking(null) });
    const remove = useMutation({ mutationFn: deleteProfile, onSuccess: () => navigate('/profile/deleted', { replace: true }) });
    return (
        <>
            <h2>{t('account.danger.title')}</h2>
            <div className="pf-danger">
                <div>
                    {asking === 'wipe'
                        ? <Confirm text={t('account.danger.wipeAsk')} choices={[[t('account.danger.wipeYes'), () => wipe.mutate()]]} onCancel={() => setAsking(null)} />
                        : <>
                            <button type="button" className="btn dan" disabled={wipe.isPending} onClick={() => setAsking('wipe')}>{t('account.danger.wipe')}</button>
                            <p className="muted small">{t('account.danger.wipeSub')}</p>
                        </>}
                </div>
                <div>
                    {asking === 'delete'
                        ? <Confirm text={t('account.danger.deleteAsk')} choices={[[t('account.danger.deleteYes'), () => remove.mutate()]]} onCancel={() => setAsking(null)} />
                        : <>
                            <button type="button" className="btn dan" disabled={remove.isPending} onClick={() => setAsking('delete')}>{t('account.danger.delete')}</button>
                            <p className="muted small">{t('account.danger.deleteSub')}</p>
                        </>}
                </div>
            </div>
        </>
    );
}
