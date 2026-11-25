// The person's defaults for the chords (the profile's settings): sharps or flats, degrees, small signs in the chord
// names, "Less" on a tap on a chord. On the song page the same switches change only the chords open.

import { useTranslation } from 'react-i18next';
import { updateSettings, useSettings } from '../../data/settings';
import { Seg, Switch } from '../../kit/controls';

export function ChordSettings() {
    const { t } = useTranslation();
    const settings = useSettings();
    return (
        <>
            <div className="setrow">
                <span>{t('song.settings.accidentals')}</span>
                <Seg value={settings.accidentals} options={(['asWritten', 'flats', 'sharps'] as const).map(x => [x, t(`song.settings.accidentalsChoice.${x}`)])}
                    onChange={accidentals => updateSettings({ accidentals })} />
            </div>
            <div className="setrow">
                <span>{t('song.settings.degrees')}<span className="sub">{t('song.settings.degreesSub')}</span></span>
                <Switch on={settings.showDegrees} label={t('song.settings.degrees')} onChange={showDegrees => updateSettings({ showDegrees })} />
            </div>
            <div className="setrow">
                <span>{t('song.settings.superscripts')}<span className="sub">{t('song.settings.superscriptsSub')}</span></span>
                <Switch on={settings.chordSuperscripts} label={t('song.settings.superscripts')} onChange={chordSuperscripts => updateSettings({ chordSuperscripts })} />
            </div>
            <div className="setrow">
                <span>{t('song.settings.less')}<span className="sub">{t('song.settings.lessSub')}</span></span>
                <Switch on={settings.lessMode} label={t('song.settings.less')} onChange={lessMode => updateSettings({ lessMode })} />
            </div>
        </>
    );
}
