// The views of an analysis, as tabs above its chords (AnalysisViewsBody.tsx): loaded when an analysis is opened.
//
//   <AnalysisViews chordsId={…} sheet={<the chord sheet/> | null} />   share={token}: opened by a share link, read through it
//   edit={the editor's address}: "fix it by hand" — the analysis's chords as the person's own variant of the song

import { lazy, Suspense, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

const Body = lazy(() => import('./AnalysisViewsBody').then(m => ({ default: m.AnalysisViewsBody })));

export function AnalysisViews(props: { chordsId: string; sheet: ReactNode | null; share?: string | null; edit?: string | null }) {
    const { t } = useTranslation();
    return (
        <Suspense fallback={<p className="loading">{t('common.loading')}</p>}>
            <Body {...props} />
        </Suspense>
    );
}
