// The views of an analysis, as tabs above its chords: text with chords (the chord sheet, the song page's), chords on
// the time line with the waveform, the grid of bars, the lead sheet, iReal Pro, the score, the notes. Each lights the
// chord and the bar that sound and plays from where it is tapped; each shows where the key, the meter or the tempo
// changes; the lit bar is kept in sight as the music goes (follow.ts). The player goes on as the tabs change. The tab chosen is kept in this browser.
//
// (Loaded when an analysis is opened, by AnalysisViews.tsx.)

import { Component, lazy, Suspense, useRef, useState, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { errorText } from '../api/errors';
import { useAnalysisPiece } from './useAnalysisPiece';
import { useFollowLit } from './follow';
import { TimeView } from './views/TimeView';
import { GridView } from './views/GridView';
import { IRealView } from './views/IRealView';
import './analysis.css';

// the larger views (the score's engraver, the lead sheet with its music font, the notes) are loaded when their tab is
// opened
const ScoreView = lazy(() => import('./views/ScoreView'));
const LeadView = lazy(() => import('./views/lead/LeadView').then(m => ({ default: m.LeadView })));
const NotesView = lazy(() => import('./views/roll/NotesView').then(m => ({ default: m.NotesView })));

/** A view that fails (the score's engraver not loaded, a drawing that breaks): a line in its place, not the page gone. */
class ViewBoundary extends Component<{ failed: ReactNode; children: ReactNode }, { failed: boolean }> {
    override state = { failed: false };

    static getDerivedStateFromError() {
        return { failed: true };
    }

    override render() {
        return this.state.failed ? this.props.failed : this.props.children;
    }
}

export type ViewName = 'text' | 'time' | 'grid' | 'lead' | 'ireal' | 'score' | 'notes';

const tabKey = 'shelf.analysis.view';

function storedTab(): ViewName {
    try {
        return (localStorage.getItem(tabKey) as ViewName | null) ?? 'text';
    } catch {
        return 'text';
    }
}

export function AnalysisViewsBody({ chordsId, sheet }: { chordsId: string; sheet: ReactNode }) {
    const { t } = useTranslation();
    const { analysis, piece } = useAnalysisPiece(chordsId);
    const [tab, setTab] = useState<ViewName>(storedTab);
    const body = useRef<HTMLDivElement>(null);
    useFollowLit(body);
    const content = analysis.data?.content;
    const has: Record<ViewName, boolean> = {
        text: true,
        time: !!piece,
        grid: !!piece?.bars.length,
        lead: !!piece?.bars.length,
        ireal: !!piece?.bars.length,
        score: !!content?.score,
        notes: !!content?.notes?.parts.length,
    };
    const shown = has[tab] ? tab : 'text';
    const choose = (name: ViewName) => {
        setTab(name);
        try {
            localStorage.setItem(tabKey, name);
        } catch {
            // kept until the page closes
        }
    };
    const tabs = (Object.keys(has) as ViewName[]).filter(name => has[name]);
    return (
        <div className="an-aviews">
            <div className="an-rtabs" role="tablist">
                {tabs.map(name => (
                    <button type="button" key={name} role="tab" aria-selected={name === shown} className={name === shown ? 'on' : ''} onClick={() => choose(name)}>
                        {t(`analysis.views.${name}`)}
                    </button>
                ))}
            </div>
            <div className="an-vbody" ref={body}>
                {analysis.isError && <p className="failed">{errorText(analysis.error)}</p>}
                <ViewBoundary key={shown} failed={<p className="failed">{t('analysis.viewFailed')}</p>}>
                <Suspense fallback={<p className="loading">{t('common.loading')}</p>}>
                {shown === 'text' && sheet}
                {piece && shown === 'time' && <TimeView piece={piece} />}
                {piece && shown === 'grid' && <GridView piece={piece} />}
                {piece && shown === 'lead' && <LeadView piece={piece} lyrics={content?.lyrics ?? null} />}
                {piece && shown === 'ireal' && <IRealView piece={piece} link={content?.chart?.iReal ?? null} title={analysis.data?.header?.title ?? ''} artists={analysis.data?.header?.artists ?? []} />}
                {content?.score && shown === 'score' && <ScoreView score={content.score} />}
                {content?.notes && shown === 'notes' && piece && <NotesView notes={content.notes} piece={piece} chordsId={chordsId} />}
                </Suspense>
                </ViewBoundary>
            </div>
        </div>
    );
}
