// Illustrations: the chords of my library that have pictures, each by one of them, its title and artists; a tap opens
// the chords (their illustrations are under "Illustrations" there, where new ones are ordered).

import { Link } from 'react-router';
import { useTranslation } from 'react-i18next';
import { errorText } from '../../api/errors';
import { Page } from '../../app/panes';
import { useIllustratedLibrary } from '../../data/illustrations';
import './illustrations.css';

export function IllustrationsScreen() {
    const { t } = useTranslation();
    const list = useIllustratedLibrary();
    const items = list.data?.pages.flatMap(page => page.items) ?? [];
    const total = list.data?.pages[0]?.total ?? 0;
    return (
        <Page>
            <h1>{t('tools.illustrations')}</h1>
            <p className="lead">{t('ills.lead')}</p>
            {list.isError && <p className="failed">{errorText(list.error)}</p>}
            {list.isPending && <p className="loading">{t('common.loading')}</p>}
            {list.data && !total && <p className="muted">{t('ills.empty')}</p>}
            <div className="ip-grid">
                {items.map(item => (
                    <Link key={item.chords} className="ip-card" to={`/library/chords/${encodeURIComponent(item.chords)}`}>
                        <img src={item.pictureUrl} alt="" loading="lazy" />
                        <span className="ip-t">
                            <b>{item.title ?? ''}</b>
                            <span className="muted small">{item.artists.join(', ')}</span>
                        </span>
                    </Link>
                ))}
            </div>
            {list.hasNextPage && (
                <p className="ip-more">
                    <button type="button" className="btn" disabled={list.isFetchingNextPage} onClick={() => void list.fetchNextPage()}>
                        {t('ills.more', { count: total - items.length })}
                    </button>
                </p>
            )}
        </Page>
    );
}
