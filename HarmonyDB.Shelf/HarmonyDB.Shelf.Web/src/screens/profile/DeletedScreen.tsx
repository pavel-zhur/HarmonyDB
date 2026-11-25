// After the profile is deleted: what is gone, and that signing in again makes a new, empty profile.

import { Link } from 'react-router';
import { useTranslation } from 'react-i18next';
import { Page } from '../../app/panes';

export function DeletedScreen() {
    const { t } = useTranslation();
    return (
        <Page>
            <div className="locked">
                <h1>{t('account.deleted.title')}</h1>
                <p className="muted">{t('account.deleted.text')}</p>
                <Link className="btn pri" to="/">{t('account.deleted.home')}</Link>
            </div>
        </Page>
    );
}
