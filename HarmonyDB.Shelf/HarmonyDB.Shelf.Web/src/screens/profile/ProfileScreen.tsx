// The profile: who I am and signing out; the settings — the interface's and the chords' size, the theme, the
// language. (Sign-ins, music accounts, clearing and deleting the profile: unit 16, below these.)

import { useTranslation } from 'react-i18next';
import { Page } from '../../app/panes';
import { clampScale } from '../../app/appearance';
import { signOut, useProfile } from '../../data/session';
import { defaultSettings, updateSettings, useSettings } from '../../data/settings';
import { Avatar, Seg, Stepper } from '../../kit/controls';
import { locales } from '../../i18n';
import { errorText } from '../../api/errors';
import { ChordSettings } from '../song/ChordSettings';
import './profile.css';

export function ProfileScreen() {
    const { t } = useTranslation();
    const { data: profile, error } = useProfile();
    const settings = useSettings();
    const percent = (value: number) => `${Math.round(value * 100)}%`;
    return (
        <Page>
            <h1>{t('profile.title')}</h1>
            {error && <p className="failed">{errorText(error)}</p>}
            {profile && (
                <div className="me">
                    <Avatar person={{ id: profile.id, name: profile.displayName, pictureUrl: profile.pictureUrl }} size="l" />
                    <b>{profile.displayName}</b>
                    <button type="button" className="btn" onClick={() => void signOut()}>{t('profile.signOut')}</button>
                </div>
            )}
            <h2>{t('profile.settings')}</h2>
            <div className="lines">
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
                <ChordSettings />
            </div>
        </Page>
    );
}
