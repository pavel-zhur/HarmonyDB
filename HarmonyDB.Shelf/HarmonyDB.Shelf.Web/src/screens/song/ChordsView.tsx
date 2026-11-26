// The chords of a variant (or chords alone): their header (the letter, the source as a link, "to a playlist", share,
// close), the line about the source (its key, the detected keys, the tempo), the calm grid above the sheet — Key,
// Chords, Marks, Notes, From the author — the tools (loops, circle of fifths, similar songs), and the sheet.
// No arrows: a swipe over the whole area goes to the next or the previous variant (or search result).

import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { Link } from 'react-router';
import { useTranslation } from 'react-i18next';
import { errorText } from '../../api/errors';
import { playlistTitle } from '../../data/playlists';
import { useProfile, useSession } from '../../data/session';
import { useSettings } from '../../data/settings';
import { useChordsAnalysis, useChordsContent, useChordsPlaces, useOwnChords, useSimilarChords, type ChordsVariant, type PersonRef, type SongPage } from '../../data/song';
import { useMarkKinds, useMarkSign } from '../../data/marks';
import { local, useLocalView, useMyKey, useMyMarks, useMyNote } from '../../local';
import { Avatar } from '../../kit/controls';
import { Icon } from '../../kit/Icon';
import { MenuItem, MenuTitle, Popover } from '../../kit/Popover';
import { SourceIcon } from '../../kit/SongRow';
import { NoteAlteration } from '../../lib/common/HarmonyDB.Common/NoteAlteration';
import { RepresentationSettings, SheetKey, SimplificationMode } from '../../lib/representations';
import { ChordSheet } from '../../chords/ChordSheet';
import { ChordsViewProvider } from '../../chords/view';
import { AnalysisViews } from '../../analysis/AnalysisViews';
import { isAnalysis } from '../../data/analysis';
import { CircleOfFifths, type Tonic } from '../../chords/CircleOfFifths';
import { FingeringWindow } from '../../chords/FingeringWindow';
import { chordNames, firstAttribute, loopAttributes, loopsOf, progressionBetween, progressionOf, type Loop } from '../../chords/analysis';
import { sheetFromApi } from '../../chords/sheet';
import { AddPop, SharePop } from './Actions';
import { keyText, stepKey } from './keys';
import { personName } from '../../kit/person';

export interface ChordsPlace {
    /** the song in its playlist, when the chords are its variant */
    page?: SongPage;
    variant?: ChordsVariant;
    /** the link the song was opened by */
    share?: string | null;
    letter?: string | null;
    /** chords alone from a search: the pager over the results */
    pager?: { index: number; count: number; prev: string | null; next: string | null };
}

type Simplification = 'none' | 'sevenths' | 'all';
const simplifications: Record<Simplification, SimplificationMode> = {
    none: SimplificationMode.None,
    sevenths: SimplificationMode.RemoveBass | SimplificationMode.Remove6 | SimplificationMode.RemoveSus,
    all: SimplificationMode.All,
};

export function ChordsView({ chordsId, place, shown, onShow, onClose, onSwipe }: {
    chordsId: string;
    place: ChordsPlace;
    /** the key shown now and not remembered (another's key applied, a step of − +); null: the remembered one */
    shown: number | null;
    onShow: (semitones: number | null) => void;
    onClose: () => void;
    onSwipe: (direction: -1 | 1) => void;
}) {
    const { t } = useTranslation();
    const content = useChordsContent(chordsId);
    const answer = content.data;
    const header = answer?.header ?? place.variant?.chords.header ?? null;
    const state = answer?.state ?? place.variant?.chords.state ?? null;
    // an analysis (it has a time line) states its own keys; the index's detection is for the other chords
    const timed = isAnalysis(header?.kinds);
    const analysis = useChordsAnalysis(chordsId, state === 'open' && header !== null && !timed);
    const settings = useSettings();
    const playlistId = place.page?.song.playlistId;

    // the view's own choices; the person's defaults from the profile
    const [alteration, setAlteration] = useState<NoteAlteration | null>(settings.accidentals === 'flats' ? NoteAlteration.Flat : settings.accidentals === 'sharps' ? NoteAlteration.Sharp : null);
    const [simplification, setSimplification] = useState<Simplification>('none');
    const [degrees, setDegrees] = useState(settings.showDegrees);
    const [superscripts, setSuperscripts] = useState(settings.chordSuperscripts);
    const [tool, setTool] = useState<'circle' | 'loops' | 'similar' | null>(null);
    const [tonic, setTonic] = useState<{ candidate: Tonic; on: boolean } | null>(null);
    const [loop, setLoop] = useState<Loop | null>(null);
    const [from, setFrom] = useState<number | null>(null);
    const [found, setFound] = useState<{ names: string; attributes: ReadonlyMap<number, string> } | null>(null);

    const mine = useMyKey(chordsId);
    const here = useMyKey(chordsId, playlistId);
    const remembered = (playlistId ? here : null) ?? mine ?? 0;
    const transpose = shown ?? remembered;

    const sheet = useMemo(() => (answer?.content?.sheet ? sheetFromApi(answer.content.sheet.root) : null), [answer]);
    const base = useMemo(() => new RepresentationSettings({
        transpose, alteration, simplification: simplifications[simplification], isVariableWidth: sheet?.isVariableWidth ?? false,
    }, { showDegrees: degrees, chordSuperscripts: superscripts, showNotesAsWritten: alteration === null && settings.accidentals === 'asWritten' }),
    [transpose, alteration, simplification, sheet, degrees, superscripts, settings.accidentals]);
    const chords = useMemo(() => (sheet ? sheet.asChords(base) : []), [sheet, base]);
    const progression = useMemo(() => (sheet && (tool === 'loops' || tool === 'similar') ? progressionOf(chords) : null), [sheet, tool, chords]);
    const loops = useMemo(() => (progression && tool === 'loops' ? loopsOf(progression, chordNames(chords, base)) : []), [progression, tool, chords, base]);
    // without a text with chords (a piece without words) an analysis's views still show the degrees
    const hasDegrees = useMemo(() => (sheet ? sheet.traverseAllChords().some(x => x.degree !== null) : timed), [sheet, timed]);

    const attributes = tool === 'loops' && loop && progression ? loopAttributes(progression, loop)
        : tool === 'similar' ? (found?.attributes ?? (from !== null ? firstAttribute(from) : null)) : null;
    const representation = useMemo(() => {
        const relative = tool === 'circle' && tonic?.on ? { relativeTo: { note: tonic.candidate.note, major: tonic.candidate.major } } : {};
        return attributes || relative.relativeTo ? base.with({ ...(attributes ? { customAttributes: attributes } : {}), ...relative }) : base;
    }, [base, attributes, tool, tonic]);

    const pickChord = (index: number) => {
        if (tool !== 'similar' || !progression) return;
        if (from !== null && from < index) {
            setFound(progressionBetween(progression, from, index));
            setFrom(null);
        } else {
            setFound(null);
            setFrom(index);
        }
    };
    const toggleTool = (next: 'circle' | 'loops' | 'similar') => {
        setTool(tool === next ? null : next);
        setLoop(null);
        setFrom(null);
        setFound(null);
    };

    // a swipe across the whole area (not a scroll, not two fingers)
    const touch = useRef<{ x: number; y: number } | null>(null);
    const start = (e: React.TouchEvent) => {
        touch.current = e.touches.length === 1 ? { x: e.touches[0]!.clientX, y: e.touches[0]!.clientY } : null;
    };
    const end = (e: React.TouchEvent) => {
        const s = touch.current;
        touch.current = null;
        if (!s || e.changedTouches.length !== 1) return;
        const dx = e.changedTouches[0]!.clientX - s.x, dy = e.changedTouches[0]!.clientY - s.y;
        if (Math.abs(dx) > 70 && Math.abs(dx) > 2 * Math.abs(dy)) onSwipe(dx < 0 ? 1 : -1);
    };

    const [addAnchor, setAddAnchor] = useState<HTMLElement | null>(null);
    const [shareAnchor, setShareAnchor] = useState<HTMLElement | null>(null);
    const places = useChordsPlaces(state === 'open' ? chordsId : null);
    const inMine = new Set((places.data?.places ?? []).filter(x => x.chords === chordsId).map(x => x.playlistId)).size;
    const authorComment = answer?.content?.sheet?.authorComment;
    // chords I wrote, editable here: edited from their header
    const own = useOwnChords();
    const mineToEdit = own.data?.items.find(x => x.chordsId === chordsId && x.editable) ?? null;

    return (
        <section className="vview" onTouchStart={start} onTouchEnd={end} onTouchCancel={() => { touch.current = null; }}>
            <div className="vhead">
                <span className="t">
                    {place.letter && <b>{place.letter}</b>}
                    {header?.source.iconUrl && <SourceIcon source={header.source} />}
                    {header?.address
                        ? <a className="el srcl" href={header.address} target="_blank" rel="noreferrer" title={t('song.chords.openSource', { source: header.source.title })}>{header.source.title}<Icon name="ext" /></a>
                        : <span className="el">{header?.source.title}</span>}
                </span>
                {place.pager && place.pager.count > 1 && (
                    <span className="pips" aria-label={t('song.chords.results')}>
                        {place.pager.prev ? <Link to={place.pager.prev} aria-label={t('song.chords.prev')} title={t('song.chords.prev')}><Icon name="left" /></Link> : <span className="off"><Icon name="left" /></span>}
                        <span className="on">{place.pager.index + 1}/{place.pager.count}</span>
                        {place.pager.next ? <Link to={place.pager.next} aria-label={t('song.chords.next')} title={t('song.chords.next')}><Icon name="right" /></Link> : <span className="off"><Icon name="right" /></span>}
                    </span>
                )}
                {mineToEdit && (
                    <Link className="xbtn" to={`/editor?own=${encodeURIComponent(mineToEdit.id)}`} aria-label={t('song.menu.edit')} title={t('song.menu.edit')}>
                        <Icon name="pen" />
                    </Link>
                )}
                {state === 'open' && (
                    <>
                        <button type="button" className={inMine ? 'xbtn cnt on' : 'xbtn cnt'} aria-label={t('song.add.button')} title={inMine ? t('song.add.inMine', { count: inMine }) : t('song.add.button')}
                            onClick={e => setAddAnchor(addAnchor ? null : e.currentTarget)}>
                            <Icon name="addpl" />{inMine > 0 && <b>{inMine}</b>}
                        </button>
                        <button type="button" className="xbtn" aria-label={t('song.share.title')} title={t('song.share.title')} onClick={e => setShareAnchor(shareAnchor ? null : e.currentTarget)}>
                            <Icon name="share" />
                        </button>
                    </>
                )}
                <button type="button" className="xbtn" aria-label={t('song.chords.close')} title={t('song.chords.close')} onClick={onClose}><Icon name="x" /></button>
            </div>
            <AddPop anchor={addAnchor} onClose={() => setAddAnchor(null)} chordsId={chordsId} />
            <SharePop anchor={shareAnchor} onClose={() => setShareAnchor(null)} title={header?.title ?? place.page?.song.title ?? ''}
                chords={{ id: chordsId, address: header?.address ?? null, isPublic: header?.isPublic ?? false }}
                target={place.page ? { kind: 'song', playlistId: place.page.song.playlistId, songId: place.page.song.id, share: place.share ?? null } : { kind: 'chords', chords: chordsId }} />

            {state === 'notOpen' && <p className="vstate">{t('song.chords.notOpen')}</p>}
            {state === 'unavailable' && <p className="vstate">{t('song.chords.unavailable')}</p>}
            {content.isError && <p className="vstate failed">{errorText(content.error)}</p>}
            {state === 'open' && !answer && <p className="vstate loading">{t('common.loading')}</p>}

            {state === 'open' && answer && (
                <div className="tools">
                    <Meta settings={base} stated={statedKeys(sheet?.keys ?? null, header?.attributes.key ?? null)} detected={timed ? [] : analysis.data?.keys ?? []} bpm={header?.attributes.bpm ?? null} />
                    <div className="panel">
                        <KeyRow chordsId={chordsId} playlistId={playlistId} member={!!place.page?.playlist?.myRole} transpose={transpose} remembered={remembered} shown={shown} onShow={onShow} />

                        <div className="plb">{t('song.grid.chords')}</div>
                        <div className="pc">
                            <div className="quiet eq">
                                <ToolButton on={alteration === NoteAlteration.Flat} label={t('song.tools.flat')} onClick={() => setAlteration(alteration === NoteAlteration.Flat ? null : NoteAlteration.Flat)}><Icon name="flat" /></ToolButton>
                                <ToolButton on={alteration === NoteAlteration.Sharp} label={t('song.tools.sharp')} onClick={() => setAlteration(alteration === NoteAlteration.Sharp ? null : NoteAlteration.Sharp)}><Icon name="sharp" /></ToolButton>
                                <Simpler value={simplification} onChange={setSimplification} />
                                {/* these act on the text with chords: a piece without one has nothing for them */}
                                {sheet && (
                                    <>
                                        <ToolButton on={tool === 'circle'} label={t('song.tools.circle')} onClick={() => toggleTool('circle')}><Icon name="circ" /></ToolButton>
                                        <ToolButton on={tool === 'loops'} label={t('song.tools.loops')} onClick={() => toggleTool('loops')}><Icon name="loop" /></ToolButton>
                                        <ToolButton on={tool === 'similar'} label={t('song.tools.similar')} onClick={() => toggleTool('similar')}><Icon name="simil" /></ToolButton>
                                    </>
                                )}
                                {hasDegrees && <ToolButton on={degrees} label={t('song.tools.degrees')} onClick={() => setDegrees(!degrees)}><Glyph kind="degrees" /></ToolButton>}
                                <ToolButton on={superscripts} label={t('song.tools.superscripts')} onClick={() => setSuperscripts(!superscripts)}><Glyph kind="superscripts" /></ToolButton>
                            </div>
                        </div>

                        <MarksRow chordsId={chordsId} page={place.page} />
                        <NotesRow chordsId={chordsId} page={place.page} />

                        {authorComment && (
                            <>
                                <div className="plb">{t('song.grid.author')}</div>
                                <div className="pc pn"><div className="nl au"><span>{authorComment}</span></div></div>
                            </>
                        )}
                    </div>

                    {tool === 'circle' && (
                        <>
                            <CircleOfFifths chords={chords} onPlayed={candidate => setTonic({ candidate, on: tonic?.on === true && tonic.candidate.note === candidate.note && tonic.candidate.major === candidate.major })} />
                            {tonic && (
                                <p className="chips cofbar">
                                    <button type="button" className={tonic.on ? 'chip on' : 'chip'} aria-pressed={tonic.on} title={t('song.tools.tonicSub')} onClick={() => setTonic({ ...tonic, on: !tonic.on })}>
                                        {t('song.tools.tonic', { name: tonic.candidate.name })}
                                    </button>
                                </p>
                            )}
                        </>
                    )}
                    {tool === 'loops' && (
                        <div className="loops">
                            {loops.length === 0 && <p className="hint">{t('song.tools.noLoops')}</p>}
                            {loops.map((x, i) => (
                                <button type="button" key={i} className={loop === x ? 'on' : ''} aria-pressed={loop === x} onClick={() => setLoop(loop === x ? null : x)}>
                                    <b>{x.chords}</b> <span className="muted">{t('song.tools.loopStats', { times: Math.round(x.link.occurrences), share: Math.round(x.link.coverage * 100) })}</span>
                                </button>
                            ))}
                        </div>
                    )}
                    {tool === 'similar' && <Similar from={from} found={found?.names ?? null} onClear={() => { setFrom(null); setFound(null); }} />}
                </div>
            )}

            {state === 'open' && answer && (timed || sheet) && (
                // the other views of an analysis name the chords as the sheet does (chords/view.tsx); an analysis
                // shows its views whether or not it has a text with chords (a piece without words has none)
                <ChordsViewProvider value={representation}>
                    {timed
                        ? <AnalysisViews chordsId={chordsId} sheet={sheet && <div className="sheetbox"><ChordSheet sheet={sheet} settings={representation} onChord={pickChord} /></div>}
                            edit={place.page?.canAdd ? `/editor?playlist=${place.page.song.playlistId}&song=${place.page.song.id}&from=${chordsId}` : `/editor?from=${encodeURIComponent(chordsId)}`} />
                        : sheet && <div className="sheetbox"><ChordSheet sheet={sheet} settings={representation} onChord={pickChord} /></div>}
                </ChordsViewProvider>
            )}
            {state === 'open' && answer && !timed && !sheet && <p className="vstate muted">{t('song.chords.noSheet')}</p>}
            <FingeringWindow />
        </section>
    );
}

function ToolButton({ on, label, onClick, children }: { on: boolean; label: string; onClick: (e: React.MouseEvent<HTMLButtonElement>) => void; children: ReactNode }) {
    return <button type="button" className={on ? 'on' : ''} aria-pressed={on} aria-label={label} title={label} onClick={onClick}>{children}</button>;
}

/** The icons of the two switches the old set has not: degrees ("IV") and superscripts ("C⁷"). */
function Glyph({ kind }: { kind: 'degrees' | 'superscripts' }) {
    return (
        <svg className="ic" viewBox="0 0 24 24" aria-hidden="true">
            {kind === 'degrees'
                ? <text x="12" y="17" textAnchor="middle" fontSize="13" fontWeight="700" stroke="none" fill="currentColor" fontFamily="Inter Variable, sans-serif">IV</text>
                : <><text x="2" y="19" fontSize="15" fontWeight="700" stroke="none" fill="currentColor" fontFamily="Inter Variable, sans-serif">C</text>
                    <text x="14" y="11" fontSize="10" fontWeight="700" stroke="none" fill="currentColor" fontFamily="Inter Variable, sans-serif">7</text></>}
        </svg>
    );
}

function Simpler({ value, onChange }: { value: Simplification; onChange: (value: Simplification) => void }) {
    const { t } = useTranslation();
    const [anchor, setAnchor] = useState<HTMLElement | null>(null);
    const choose = (next: Simplification) => {
        onChange(next);
        setAnchor(null);
    };
    return (
        <>
            <ToolButton on={value !== 'none'} label={t('song.tools.simpler')} onClick={e => setAnchor(anchor ? null : e.currentTarget)}><Icon name="simp" /></ToolButton>
            <Popover anchor={anchor} onClose={() => setAnchor(null)} width={280} label={t('song.tools.simpler')}>
                <MenuItem on={value === 'none'} sub="Am7/G, Fmaj7" onClick={() => choose('none')}>{t('song.tools.simple.none')}</MenuItem>
                <MenuItem on={value === 'sevenths'} sub="Am7, G7" onClick={() => choose('sevenths')}>{t('song.tools.simple.sevenths')}</MenuItem>
                <MenuItem on={value === 'all'} sub="Am, G" onClick={() => choose('all')}>{t('song.tools.simple.all')}</MenuItem>
            </Popover>
        </>
    );
}

/**
 * The keys the item states: the sheet's own keys when it has them (an analysis: the keys of its time line, in the order
 * they come), else the key its source gives (a name; one not read as a key is shown as it is written).
 */
function statedKeys(keys: readonly SheetKey[] | null, attribute: string | null): (SheetKey | string)[] {
    if (keys?.length) return [...keys];
    if (!attribute) return [];
    try {
        return [SheetKey.parse(attribute)];
    } catch {
        return [attribute];
    }
}

/**
 * The key(s) the item states, the detected ones with their confidence (the second hidden on a phone), the tempo: facts
 * about the chords as they are written, not moved by the transposition. When the chords are shown in another key, where
 * they are now is said apart ("now E").
 */
function Meta({ settings, stated, detected, bpm }: { settings: RepresentationSettings; stated: (SheetKey | string)[]; detected: { key: { tonic: string; isMinor: boolean }; confidence: number }[]; bpm: number | null }) {
    const { t } = useTranslation();
    const written = settings.with({ transpose: 0, relativeTo: null, alteration: null, showNotesAsWritten: true });
    const name = (key: SheetKey | string, view = written) => (typeof key === 'string' ? key : key.shown(view).name);
    const first = stated.find((x): x is SheetKey => typeof x !== 'string') ?? (detected[0] ? new SheetKey(detected[0].key.tonic, detected[0].key.isMinor) : null);
    const now = settings.transpose !== 0 && first ? name(first, settings.with({ relativeTo: null })) : null;
    if (!stated.length && !detected.length && !bpm) return null;
    return (
        <div className="meta">
            {stated.length > 0 && <span className="mi"><b>{stated.map(x => name(x)).join(' → ')}</b>{t('song.meta.stated')}</span>}
            {detected.length > 0 && (
                <span className="mi" title={t('song.meta.detectedSub')}>
                    <i>{t('song.meta.detected')}</i>
                    {detected.map((x, i) => (
                        <span key={i} className={i ? 'alt' : ''}>{i > 0 && <i>· </i>}<b>{name(new SheetKey(x.key.tonic, x.key.isMinor))}</b> {Math.round(x.confidence * 100)}%</span>
                    ))}
                </span>
            )}
            {now && <span className="mi"><i>{t('song.meta.now')}</i><b>{now}</b></span>}
            {!!bpm && <span className="mi"><b>{Math.round(bpm)}</b>BPM</span>}
        </div>
    );
}

/** Key: "− +3 +", and the words only when there is something to say. */
function KeyRow({ chordsId, playlistId, member, transpose, remembered, shown, onShow }: {
    chordsId: string; playlistId?: string; member: boolean; transpose: number; remembered: number; shown: number | null; onShow: (semitones: number | null) => void;
}) {
    const { t } = useTranslation();
    const { signedIn } = useSession();
    const mine = useMyKey(chordsId);
    const here = useMyKey(chordsId, playlistId);
    const view = useLocalView();
    const { data: places } = useChordsPlaces(chordsId);
    const changed = shown !== null && shown !== remembered;
    // my keys for this chords in other playlists (outside a playlist: each its own button)
    // my "here" keys for these chords in my other playlists: each its own button (as I see them now, else the server's)
    const others = new Map<string, number>();
    for (const k of places?.keys ?? []) if (k.chords === chordsId && k.playlistId !== playlistId) others.set(k.playlistId, k.semitones);
    for (const k of Object.values(view.keys)) {
        if (k.chords !== chordsId || !k.playlistId || k.playlistId === playlistId) continue;
        if (k.semitones === null || k.semitones === undefined) others.delete(k.playlistId);
        else others.set(k.playlistId, k.semitones);
    }
    const elsewhere = [...others].map(([id, semitones]) => ({ playlistId: id, semitones }));
    const titleOf = (id: string) => {
        const p = places?.playlists.find(x => x.id === id);
        return p ? playlistTitle(p) : '';
    };
    const remember = () => {
        if (playlistId && here !== null) local.key(chordsId, transpose, playlistId);
        else local.key(chordsId, transpose);
        // a guest is asked to sign in first: the key stays shown meanwhile, and is the remembered one after
        if (signedIn) onShow(null);
    };
    return (
        <>
            <div className="plb">{t('song.grid.key')}</div>
            <div className="pc">
                <div className="prow keys">
                    <span className="step">
                        <button type="button" aria-label={t('song.key.down')} title={t('song.key.down')} onClick={() => onShow(stepKey(transpose, -1))}>−</button>
                        <span className="v">{keyText(transpose)}</span>
                        <button type="button" aria-label={t('song.key.up')} title={t('song.key.up')} onClick={() => onShow(stepKey(transpose, 1))}>+</button>
                    </span>
                    {changed && <button type="button" className="kchip ask" title={t('song.key.rememberSub')} onClick={remember}>{t('song.key.remember')}</button>}
                    {here !== null && (
                        <button type="button" className="kchip h set" aria-pressed="true"
                            title={here === transpose ? t('song.key.hereRemove') : t('song.key.hereReplace', { key: keyText(transpose) })}
                            onClick={() => { local.key(chordsId, here === transpose ? null : transpose, playlistId); onShow(null); }}>
                            {t('song.key.here', { key: keyText(here) })}
                        </button>
                    )}
                    {elsewhere.map(k => (
                        <button type="button" key={k.playlistId} className="kchip h set" title={t('song.key.inPlaylistSub')} onClick={() => onShow(k.semitones)}>
                            {titleOf(k.playlistId)} {keyText(k.semitones)}
                        </button>
                    ))}
                    {/* mine and here are both seen when both are kept, even when equal */}
                    {mine !== null && (mine !== transpose || here !== null) && (
                        <button type="button" className="kchip set" title={t('song.key.mineSub')} onClick={() => onShow(mine)}>{t('song.key.mine', { key: keyText(mine) })}</button>
                    )}
                    {signedIn && member && playlistId && here === null && (
                        <button type="button" className="kchip add" aria-label={t('song.key.hereAdd')} title={t('song.key.hereAdd')}
                            onClick={() => { local.key(chordsId, transpose, playlistId); onShow(null); }}>
                            <Icon name="plus" />{changed ? t('song.key.onlyHere') : t('song.key.forHere')}
                        </button>
                    )}
                    {changed && <button type="button" className="kchip back" title={t('song.key.backSub')} onClick={() => onShow(null)}>{t('song.key.back', { key: keyText(remembered) })}</button>}
                </div>
            </div>
        </>
    );
}

/** A person as a face: from the song's people, or me. */
function usePerson(page?: SongPage) {
    const { accountId } = useSession();
    const { data: profile } = useProfile();
    const { t } = useTranslation();
    return (id: string): PersonRef => {
        if (id === accountId) return { id, name: profile?.displayName ?? t('common.me'), pictureUrl: profile?.pictureUrl ?? null, gone: false };
        return page?.people.find(x => x.id === id) ?? { id, name: '?', pictureUrl: null, gone: false };
    };
}

/** Marks: a tap puts or takes off mine; the faces of the first three who marked, me first; the sign at the end opens who marked and the choice of a new mark. */
function MarksRow({ chordsId, page }: { chordsId: string; page?: SongPage }) {
    const { t } = useTranslation();
    const { accountId } = useSession();
    const mineMarks = useMyMarks(chordsId);
    const markSign = useMarkSign();
    const kinds = useMarkKinds();
    const person = usePerson(page);
    const [anchor, setAnchor] = useState<HTMLElement | null>(null);

    const groups = new Map<string, string[]>();
    for (const kind of mineMarks) groups.set(kind, [accountId ?? 'me']);
    for (const m of page?.marks ?? []) {
        if (m.chords !== chordsId || m.accountId === accountId) continue;
        groups.set(m.kind, [...(groups.get(m.kind) ?? []), m.accountId]);
    }
    const toggle = (kind: string) => local.mark(chordsId, kind, !mineMarks.includes(kind));

    return (
        <>
            <div className="plb">{t('song.grid.marks')}</div>
            <div className="pc">
                <div className="prow marks">
                    {[...groups].map(([kind, who]) => {
                        const on = mineMarks.includes(kind);
                        return (
                            <button type="button" key={kind} className={on ? 'rx on' : 'rx'} aria-pressed={on}
                                title={`${who.map(id => personName(person(id))).join(', ')} — ${t(on ? 'song.marks.takeOff' : 'song.marks.put')}`} onClick={() => toggle(kind)}>
                                {markSign(kind)}
                                <span className="fc">{who.slice(0, 3).map(id => <Avatar key={id} person={person(id)} size="s" />)}{who.length > 3 && <i className="ct">+{who.length - 3}</i>}</span>
                            </button>
                        );
                    })}
                    <button type="button" className="rx add" aria-label={t('song.marks.who')} title={t('song.marks.who')} onClick={e => setAnchor(anchor ? null : e.currentTarget)}>
                        {groups.size ? <Icon name="smile" /> : <>🤍 <span>{t('song.marks.mark')}</span></>}
                    </button>
                </div>
            </div>
            <Popover anchor={anchor} onClose={() => setAnchor(null)} width={320} label={t('song.marks.who')}>
                {groups.size > 0 && (
                    <>
                        <MenuTitle>{t('song.marks.whoTitle')}</MenuTitle>
                        <div className="wholist">
                            {[...groups].map(([kind, who]) => (
                                <div key={kind}><span className="e">{markSign(kind)}</span>
                                    <span>{who.map(id => <span key={id} className="who"><Avatar person={person(id)} size="s" />{id === accountId ? t('common.me') : personName(person(id))}</span>)}</span>
                                </div>
                            ))}
                        </div>
                        <hr />
                    </>
                )}
                <MenuTitle>{t('song.marks.mine')}</MenuTitle>
                <div className="emo">
                    {kinds.map(({ key, sign }) => (
                        <button type="button" key={key} className={mineMarks.includes(key) ? 'on' : ''} aria-pressed={mineMarks.includes(key)} onClick={() => toggle(key)}>{sign}</button>
                    ))}
                </div>
                <p className="note">{t('song.marks.note')}</p>
            </Popover>
        </>
    );
}

/** Notes: mine (only I see it) and in the playlist (its members see it); a tap on the word opens and closes the editor. */
function NotesRow({ chordsId, page }: { chordsId: string; page?: SongPage }) {
    const { t } = useTranslation();
    const { accountId } = useSession();
    const person = usePerson(page);
    // a note for the playlist's members is written by a member
    const songId = page?.playlist?.myRole ? page.song.id : undefined;
    const personal = useMyNote({ chords: chordsId });
    const inPlaylist = useMyNote({ songId: songId ?? '' });
    const [open, setOpen] = useState(false);
    const others = (page?.notes ?? []).filter(n => n.accountId !== accountId && n.text);
    const toggle = () => setOpen(!open);

    const lines = [
        ...(personal && !open ? [{ key: 'me', me: true, face: null, text: personal }] : []),
        ...(songId && inPlaylist && !open ? [{ key: 'here', me: false, face: accountId, text: inPlaylist }] : []),
        ...others.map(n => ({ key: n.accountId, me: false, face: n.accountId, text: n.text })),
    ];

    return (
        <>
            <button type="button" className={open ? 'plb on' : 'plb'} aria-expanded={open} title={t(open ? 'song.notes.collapse' : 'song.notes.open')} onClick={toggle}>{t('song.grid.notes')}</button>
            <div className="pc pn">
                {open && (
                    <div className="noteed">
                        <div className="nehead"><b>{t('song.grid.notes')}</b><button type="button" className="lnk" onClick={toggle}>{t('song.notes.collapse')}<Icon name="up" /></button></div>
                        <label>{t('song.notes.personal')}
                            <NoteField stored={personal} placeholder={t('song.notes.personalHint')} onSave={text => local.noteTyped({ chords: chordsId }, text)} />
                        </label>
                        {songId
                            ? <label>{t('song.notes.playlist')}
                                <NoteField stored={inPlaylist} placeholder={t('song.notes.playlistHint')} onSave={text => local.noteTyped({ songId }, text)} />
                            </label>
                            : <div className="hint">{t('song.notes.noPlaylist')}</div>}
                    </div>
                )}
                {lines.length > 0 && (
                    <button type="button" className="notesc" aria-label={t('song.notes.open')} onClick={() => !open && toggle()}>
                        {lines.map(n => (
                            <span key={n.key} className={n.me ? 'nl me' : 'nl'}>
                                {n.me ? <Icon name="lock" /> : <Avatar person={person(n.face!)} size="s" />}
                                <span>{n.text}</span>
                            </span>
                        ))}
                    </button>
                )}
                {!open && lines.length === 0 && (
                    <button type="button" className="rx add" onClick={toggle}><Icon name="plus" /><span>{t('song.notes.write')}</span></button>
                )}
            </div>
        </>
    );
}

/**
 * A note's field: what is typed is kept, quietly (a small "saved" by it), a moment after the typing stops (the
 * local-first store: seen at once, sent soon), and at once on every way out — the editor collapsed, the chords or the song closed, another variant, the page
 * left. A note is never lost.
 */
function NoteField({ stored, placeholder, onSave }: { stored: string | null; placeholder: string; onSave: (text: string | null) => void }) {
    const { t } = useTranslation();
    const [draft, setDraft] = useState<string | null>(null);
    const [saved, setSaved] = useState(false);
    const pending = useRef<{ text: string; timer: ReturnType<typeof setTimeout> } | null>(null);
    const save = useRef(onSave);
    save.current = onSave;
    const kept = useRef(stored);
    kept.current = stored;

    const flush = useCallback(() => {
        const now = pending.current;
        if (!now) return;
        clearTimeout(now.timer);
        pending.current = null;
        const text = now.text.trim() || null;
        if (text !== (kept.current ?? null)) {
            save.current(text);
            setSaved(true);
        }
    }, []);

    useEffect(() => {
        addEventListener('pagehide', flush);
        return () => {
            removeEventListener('pagehide', flush);
            flush();
        };
    }, [flush]);

    const type = (text: string) => {
        setDraft(text);
        setSaved(false);
        if (pending.current) clearTimeout(pending.current.timer);
        pending.current = { text, timer: setTimeout(flush, 1200) };
    };

    return (
        <>
            <textarea className="field" value={draft ?? stored ?? ''} placeholder={placeholder} onChange={e => type(e.target.value)} onBlur={flush} />
            <span className={saved ? 'nsaved on' : 'nsaved'} aria-live="polite">{saved ? t('song.notes.saved') : ''}</span>
        </>
    );
}

/** The search of songs with the same progression: two taps on the sheet, then what was found, page by page. */
function Similar({ from, found, onClear }: { from: number | null; found: string | null; onClear: () => void }) {
    const { t } = useTranslation();
    const [pages, setPages] = useState(1);
    const first = useSimilarChords(found, 1);
    if (!found) return <p className="hint">{from === null ? t('song.similar.first') : t('song.similar.last')}</p>;
    const total = first.data?.totalPages ?? 1;
    return (
        <div className="hint similar">
            <div>
                {first.isPending && <span className="muted">{t('song.similar.looking', { progression: found })}</span>}
                {first.isError && errorText(first.error)}
                {first.data && (first.data.items.length ? t('song.similar.found', { progression: found }) : t('song.similar.none', { progression: found }))}
                {' '}<button type="button" className="chip" onClick={() => { setPages(1); onClear(); }}>{t('common.clear')}</button>
            </div>
            <div className="simrows">
                {Array.from({ length: pages }, (_, i) => <SimilarPage key={i} progression={found} page={i + 1} />)}
            </div>
            {pages < total && <button type="button" className="lnk" onClick={() => setPages(pages + 1)}>{t('common.showMore')}</button>}
        </div>
    );
}

function SimilarPage({ progression, page }: { progression: string; page: number }) {
    const { t } = useTranslation();
    const similar = useSimilarChords(progression, page);
    // the first page's waiting and failure are said above, with the progression
    if (!similar.data) return page === 1 ? null : similar.isError ? <p className="failed">{errorText(similar.error)}</p> : <p className="muted small">{t('common.loading')}</p>;
    return (
        <>
            {similar.data.items.map(item => (
                <Link key={item.header.id} to={`/chords/${encodeURIComponent(item.header.id)}`} className="simrow">
                    <SourceIcon source={item.header.source} />
                    <span className="tx">{item.header.artists.join(', ')}{item.header.artists.length ? ' — ' : ''}<b>{item.header.title}</b></span>
                    <span className="muted small">{Math.round(item.coverage * 100)}%</span>
                </Link>
            ))}
        </>
    );
}
