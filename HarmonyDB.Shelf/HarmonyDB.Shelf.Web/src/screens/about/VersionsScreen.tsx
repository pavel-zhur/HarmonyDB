// The versions: what changed from release to release, the latest first.

import { useTranslation } from 'react-i18next';
import { Page } from '../../app/panes';
import { formatDate } from '../../i18n';
import type { en } from '../../i18n/en';
import './about.css';

type Release = keyof typeof en.versions.items;

const releases: [Release, string, string][] = [
    ['v1_2', '1.2', '2026-11-15'], ['v1_1', '1.1', '2024-10-01'], ['v1_0', '1.0', '2024-05-21'], ['v0_13', '0.13', '2024-05-01'],
    ['v0_12', '0.12', '2024-01-21'], ['v0_11', '0.11', '2024-01-06'], ['v0_10', '0.10', '2023-12-29'], ['v0_9', '0.9', '2023-12-04'],
    ['v0_8', '0.8', '2023-11-03'], ['v0_7', '0.7', '2023-10-17'], ['v0_6', '0.6', '2023-10-14'], ['v0_5', '0.5', '2023-09-10'],
    ['v0_4', '0.4', '2023-08-17'], ['v0_3', '0.3', '2023-08-10'], ['v0_2', '0.2', '2023-06-20'], ['v0_1', '0.1', '2023-05-10'],
];

export function VersionsScreen() {
    const { t } = useTranslation();
    return (
        <Page>
            <h1>{t('tools.versions')}</h1>
            <div className="lines ab-releases">
                {releases.map(([key, version, date], i) => (
                    <section key={key} className="ab-release">
                        <b>{t('versions.version', { v: version })}</b> <span className="muted small">· {formatDate(date)}</span>
                        {i === 0 && <span className="ab-latest">{t('versions.latest')}</span>}
                        <ul>{Object.values(t(`versions.items.${key}`, { returnObjects: true }) as Record<string, string>).map(item => <li key={item}>{item}</li>)}</ul>
                    </section>
                ))}
            </div>
        </Page>
    );
}
