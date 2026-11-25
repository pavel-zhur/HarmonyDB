// "More" (a phone's fifth tab; a guest's "Tools"): the tools and the rest, as plain lines.

import { Link } from 'react-router';
import { useTranslation } from 'react-i18next';
import { Page } from '../../app/panes';
import { guestOtherNav, otherNav, toolsNav, type NavItem } from '../../app/nav';
import { isDark } from '../../app/appearance';
import { useSession } from '../../data/session';
import { updateSettings, useSettings } from '../../data/settings';
import { Soon } from '../../kit/controls';
import { Icon } from '../../kit/Icon';

export function MoreScreen() {
    const { t } = useTranslation();
    const { signedIn } = useSession();
    useSettings();
    return (
        <Page>
            <h1>{signedIn ? t('nav.more') : t('nav.tools')}</h1>
            {signedIn && <h2 style={{ marginTop: 8 }}>{t('nav.tools')}</h2>}
            <div className="menu">{toolsNav.map(item => <Line key={item.to} item={item} />)}</div>
            <h2>{t('nav.other')}</h2>
            <div className="menu">
                {(signedIn ? otherNav : guestOtherNav).map(item => <Line key={item.to} item={item} />)}
                <button type="button" onClick={() => updateSettings({ theme: isDark() ? 'light' : 'dark' })}>
                    <Icon name="theme" /><span>{t('nav.theme')}<small>{t('nav.themeSub')}</small></span>
                </button>
            </div>
            {!signedIn && <p className="muted small" style={{ marginTop: 14 }}>{t('tools.guestNote')}</p>}
        </Page>
    );
}

function Line({ item }: { item: NavItem }) {
    const { t } = useTranslation();
    const body = <><Icon name={item.icon} /><span>{t(item.label)} {item.soon && <Soon />}{item.sub && <small>{t(item.sub)}</small>}</span></>;
    return item.soon ? <div className="soonline" aria-disabled="true">{body}</div> : <Link to={item.to}>{body}</Link>;
}
