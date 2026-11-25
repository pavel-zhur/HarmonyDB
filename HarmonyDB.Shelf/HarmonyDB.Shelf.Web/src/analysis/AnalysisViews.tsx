// The views of an analysis, as tabs above its chords (AnalysisViewsBody.tsx): loaded when an analysis is opened.
//
//   <AnalysisViews chordsId={…} sheet={<the chord sheet/>} />

import { lazy, Suspense, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

const Body = lazy(() => import('./AnalysisViewsBody').then(m => ({ default: m.AnalysisViewsBody })));

export function AnalysisViews(props: { chordsId: string; sheet: ReactNode }) {
    const { t } = useTranslation();
    return (
        <Suspense fallback={<p className="loading">{t('common.loading')}</p>}>
            <Body {...props} />
        </Suspense>
    );
}
