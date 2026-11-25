// The views of an analysis, as tabs above its chords: text with chords (the chord sheet, the song page's), chords on
// the time line with the waveform, the grid of bars, the lead sheet, iReal Pro, the score, the notes. Each lights the
// chord and the bar that sound and plays from where it is tapped; each shows where the key, the meter or the tempo
// changes; the lit bar is kept in sight as the music goes (follow.ts). The player goes on as the tabs change. The tab chosen is kept in this browser.
// A piece without words has no text with chords (`sheet` null): its Text tab says so, and it opens on a view that has
// something to show.
//
// (Loaded when an analysis is opened, by AnalysisViews.tsx.)

import { Component, lazy, Suspense, useRef, useState, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import { errorText } from '../api/errors';
import { Icon } from '../kit/Icon';
import { useChangeWords } from './marks';
import type { Piece } from './piece';
import { useAnalysisPiece } from './useAnalysisPiece';
import { useFollowLit } from './follow';
import { analysisPlayer, useAnotherPlays } from './clock';
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

function storedTab(): ViewName | null {
    try {
        return localStorage.getItem(tabKey) as ViewName | null;
    } catch {
        return null;
    }
}

export function AnalysisViewsBody({ chordsId, sheet, share, edit = null }: { chordsId: string; sheet: ReactNode | null; share?: string | null; edit?: string | null }) {
    const { t } = useTranslation();
    const { analysis, piece } = useAnalysisPiece(chordsId, share);
    // the tab chosen on this page; until then the one kept in this browser, when it has something to show
    const [tab, setTab] = useState<ViewName | null>(null);
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
    const showing = (name: ViewName | null): name is ViewName => name !== null && has[name] && (name !== 'text' || sheet !== null);
    const kept = storedTab();
    const shown: ViewName = tab !== null && has[tab] ? tab
        : showing(kept) ? kept
        : (Object.keys(has) as ViewName[]).find(showing) ?? 'text';
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
            {piece && <AnalysisHint piece={piece} edit={edit} />}
            {piece && <AnotherPlays chordsId={chordsId} />}
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
                {shown === 'text' && (sheet ?? (analysis.isPending ? <p className="loading">{t('common.loading')}</p> : <p className="muted">{t('song.chords.noSheet')}</p>))}
                {piece && shown === 'time' && <TimeView piece={piece} />}
                {piece && shown === 'grid' && <GridView piece={piece} />}
                {piece && shown === 'lead' && <LeadView piece={piece} lyrics={content?.lyrics ?? null} />}
                {piece && shown === 'ireal' && <IRealView piece={piece} link={content?.chart?.iReal ?? null} title={analysis.data?.header?.title ?? ''} artists={analysis.data?.header?.artists ?? []} />}
                {content?.score && shown === 'score' && <ScoreView score={content.score} />}
                {content?.notes && shown === 'notes' && piece && <NotesView notes={content.notes} piece={piece} chordsId={chordsId} share={share} />}
                </Suspense>
                </ViewBoundary>
            </div>
        </div>
    );
}

/** Another recording of the song plays: the views light nothing — said, with the way back to the analysed one. */
function AnotherPlays({ chordsId }: { chordsId: string }) {
    const { t } = useTranslation();
    if (!useAnotherPlays()) return null;
    return (
        <p className="an-hint">
            <Icon name="phones" />
            <span>
                {t('analysis.another')}{' '}
                <button type="button" className="lnk" onClick={() => analysisPlayer.back(chordsId)}>{t('analysis.backToAnalysed')}</button>
            </span>
        </p>
    );
}

/**
 * What the analysis is, in one line (the mock-up's): ours, of this recording; its key (and where it goes), meter and
 * tempo; and, wrong somewhere, "fix it by hand" — its chords copied into the editor as the person's own chords, no
 * longer tied to the time. (`edit`: the editor's address; none: no such line, as on the public page.)
 */
function AnalysisHint({ piece, edit }: { piece: Piece; edit: string | null }) {
    const { t } = useTranslation();
    const changeWords = useChangeWords();
    const first = piece.changes[0];
    const keys = piece.changes.map(c => c.key).filter((k): k is string => !!k);
    const facts = first ? changeWords(first, true, piece.bars[0]?.tempo?.beatUnit) : [];
    const later = keys.length > 1 ? changeWords({ ...first!, key: keys[keys.length - 1], timeSignature: undefined, bpm: undefined }, false) : [];
    return (
        <p className="an-hint">
            <Icon name="ai" />
            <span>
                {t('analysis.hint.ours')} {[...facts, ...later].join(' · ')}.
                {edit && <> {t('analysis.hint.wrong')} <Link className="lnk" to={edit}>{t('analysis.hint.fix')}</Link>{t('analysis.hint.fixAfter')}</>}
            </span>
        </p>
    );
}
