// The original: Pages/Progressions.razor.
//
// The page's fields live in one object (as the component instance's fields). The value tuples are objects with their
// element names. Whitespace text between elements, where the Razor markup keeps it, is a single space here.

import { useEffect, useRef } from 'react';
import { getRequired } from '../dotnet/collections';
import { blazorEventHandler } from '../framework/blazorEventHandler';
import { nativeChange, type NativeChangeEvent } from '../framework/elementRefs';
import { Virtualize } from '../framework/Virtualize';
import { errorHandledTask } from '../framework/unhandledExceptions';
import { useCollectionComponent } from '../hooks/useCollectionComponent';
import { useStateHasChanged } from '../hooks/useStateHasChanged';
import type { ChordsProgression } from '../lib/analysis/Models/ChordsProgression';
import { ChordType } from '../lib/analysis/Models/ChordType';
import type { SearchResult } from '../lib/analysis/Services/ProgressionsSearch';
import { ProgressionsVisualizer } from '../lib/analysis/Services/ProgressionsVisualizer';
import type { ISong } from '../lib/api/OneShelf.Frontend.Api.Model/V3/Databasish/Interfaces/ISong';
import { withIsFirst } from '../lib/common/OneShelf.Common/Extensions';
import { services } from '../services/registry';
import type { SongVersionProgression } from '../services/ProgressionsCacheLoader';
import './Progressions.scoped.scss';

/** `{n:000}` */
function format000(n: number): string {
    return n < 0 ? '-' + String(-n).padStart(3, '0') : String(n).padStart(3, '0');
}

export function Progressions() {
    const progressionsCacheLoader = services.progressionsCacheLoader;
    const progressionsSearch = services.progressionsSearch;

    const stateHasChanged = useStateHasChanged();

    const f = useRef({
        loadInitiated: false,
        data: null as SongVersionProgression[] | null,
        preparedData: null as SongVersionProgression[] | null,
        harmonyView: false,
        searchingFrom: null as { progression: ChordsProgression; chordIndex: number } | null,
        found: null as SearchResult | null,
        needCache: false,
    }).current;

    const collectionIndex = () => services.collectionIndexProvider.peek().collectionIndex;

    const { collectionIndex: renderCollectionIndex, collectionIndexFailed, retryCollectionIndex } = useCollectionComponent(async () => {
        if (collectionIndex() !== null) {
            await update();
        }
    });

    // OnAfterRenderAsync(firstRender)
    const firstRender = useRef(true);
    useEffect(() => {
        const isFirstRender = firstRender.current;
        firstRender.current = false;
        if (isFirstRender && collectionIndex() !== null) {
            void errorHandledTask(update());
        }
    });

    async function update(): Promise<void> {
        if (f.loadInitiated) {
            return;
        }

        f.loadInitiated = true;

        f.preparedData = f.data = await progressionsCacheLoader.get();
        if (f.data === null) {
            f.needCache = true;
        }

        stateHasChanged();
    }

    function setHarmonyView(args: NativeChangeEvent<HTMLInputElement>): void {
        f.harmonyView = args.currentTarget.checked;
    }

    function chordClick(progression: ChordsProgression, chordIndex: number): void {
        if (f.searchingFrom !== null) {
            if (f.searchingFrom.progression === progression) {
                if (f.searchingFrom.chordIndex < chordIndex) {
                    console.log(`Search initiated ${f.searchingFrom.chordIndex} -> ${chordIndex}`);

                    const search = progressionsSearch.extractSearch(progression, f.searchingFrom.chordIndex, chordIndex);
                    if (search === null) {
                        console.log('Extraction failed.');
                    } else {
                        console.log(search.movements.map(x => x.title).join(', '));
                        f.found = progressionsSearch.search(f.data!.map(x => x.progression), search);
                        f.preparedData = f.data!.filter(x => f.found?.foundProgressionsWithCoverage.has(x.progression) !== false);
                    }

                    f.searchingFrom = null;
                    return;
                }
            }
        }

        f.searchingFrom = { progression, chordIndex };
    }

    function reset(): void {
        f.preparedData = f.data;
        f.found = null;
    }

    function getSongTitle(song: ISong): string {
        const index = collectionIndex()!;
        return `${format000(song.index)}. ${song.artists.map(a => getRequired(index.artistsById, a).name).join(', ')} — ${song.title}`;
    }

    const on = <TArgs extends unknown[]>(handler: (...args: TArgs) => void | Promise<unknown>) => blazorEventHandler(stateHasChanged, handler);
    const loading = <div className="small-loading"><img className="music-loading" src="img/music-loading.gif" title="Загрузка..." alt="Загрузка..." /></div>;

    if (collectionIndexFailed) {
        return (
            <>
                <p><em>Не получилось загрузить, может нету интернета, исполнителей.</em></p>{' '}
                <button className="btn btn-secondary" onClick={on(retryCollectionIndex)}>Повтор</button>
            </>
        );
    }

    if (renderCollectionIndex === null) {
        return loading;
    }

    if (f.data === null) {
        if (f.needCache) {
            return (
                <div>
                    {' Надо сначала построить прогрессии. Для этого '}<a href="/cache">сходите в кэш</a>{', пожалуйста. '}
                </div>
            );
        }

        return (
            <>
                <div>Построение прогрессий...</div>{' '}
                {loading}
            </>
        );
    }

    const data = f.data;
    const found = f.found;
    const searchingFrom = f.searchingFrom;

    let foundList = null;
    if (found !== null) {
        // GroupBy(x => x.song), in the order of first appearance
        const groups = new Map<ISong, SongVersionProgression[]>();
        for (const x of f.preparedData!) {
            const group = groups.get(x.song);
            if (group === undefined) groups.set(x.song, [x]);
            else group.push(x);
        }

        const songs = [...groups].map(([song, items]) => {
            let coverage = getRequired(found.foundProgressionsWithCoverage, items[0]!.progression);
            for (const item of items) {
                const value = getRequired(found.foundProgressionsWithCoverage, item.progression);
                if (value > coverage) coverage = value;
            }

            return { song, coverage };
        });

        // OrderByDescending(x => x.coverage): stable
        const ordered = songs.map((x, i) => ({ x, i })).sort((a, b) => b.x.coverage - a.x.coverage || a.i - b.i).map(x => x.x);

        foundList = (
            <>
                <h1>Найдено</h1>{' '}
                <ul>
                    {ordered.map(({ song, coverage }, i) => <li key={i}>{getSongTitle(song)} {Math.trunc(Math.fround(coverage * 100))}%</li>)}
                </ul>
            </>
        );
    }

    const isError = (chordType: ChordType | undefined) => chordType === undefined || chordType === ChordType.Unknown;

    return (
        <>
            <div className="my-header-fixed">
                {'\n        Count: '}{data.length}{'\n        '}
                <input id="original" type="checkbox" ref={nativeChange<HTMLInputElement>(on(setHarmonyView))} />{' '}
                <label htmlFor="original">
                    {' Гармоническое представление '}
                </label>
                {found !== null && <button onClick={on(reset)}>Сбросить поиск</button>}
            </div>{' '}
            <div className="mt-5"></div>
            {foundList}
            <Virtualize items={f.preparedData!}>
                {item => (
                    <div className="element">
                        <h3 className={`mt-3 ${item.progression.originalSequence.some(x => isError(x.original.harmonyData?.chordType)) ? 'chord-error' : item.progression.originalSequence.some(x => !x.original.isStandard) ? 'chord-yellow' : ''}`}>{getSongTitle(item.song)}</h3>{' '}
                        <div className="chords-block">
                            <span>Оригинальные: </span>
                            {item.progression.originalSequence.map((chord, i) => {
                                const searchFirst = searchingFrom?.progression === item.progression && searchingFrom?.chordIndex === i;
                                return (
                                    <b
                                        key={i}
                                        chord-custom={searchFirst ? ProgressionsVisualizer.AttributeSearchFirst : undefined}
                                        onClick={on(() => chordClick(item.progression, i))}
                                        className={`chord me-2 ${chord.extendedMovementsSequence === null ? 'no-click' : ''} ${chord.indexInHarmonyGroup === 0 ? 'my-bolder' : ''} ${searchFirst ? '' : isError(chord.original.harmonyData?.chordType) ? 'chord-error' : chord.original.isStandard ? '' : 'chord-yellow'}`}
                                    >{f.harmonyView ? chord.original.harmonyRepresentation : chord.original.originalRepresentation}</b>
                                );
                            })}
                        </div>{' '}
                        <div className="chords-block">
                            <span>Гармония: </span>
                            {item.progression.harmonySequence.map(({ harmonyGroup }, i) => {
                                const isFirst = found?.harmonyGroupsWithIsFirst.get(harmonyGroup);
                                return (
                                    <b
                                        key={i}
                                        chord-custom={isFirst !== undefined ? isFirst ? ProgressionsVisualizer.AttributeSearchFirst : ProgressionsVisualizer.AttributeSearch : undefined}
                                        className={`chord no-click me-2 my-bolder ${found?.harmonyGroupsWithIsFirst.has(harmonyGroup) === true ? '' : harmonyGroup.harmonyData === null ? 'chord-error' : harmonyGroup.isStandard ? '' : 'chord-yellow'}`}
                                    >{harmonyGroup.harmonyRepresentation}</b>
                                );
                            })}
                        </div>
                        {item.progression.standardHarmonyMovementsSequences !== null && (
                            <div className="chords-block">
                                <span>Стандартные секвенции: </span>
                                {item.progression.standardHarmonyMovementsSequences.flatMap(s => withIsFirst(s.movements)).map(({ isFirst, x: movement }, i) => (
                                    <b key={i} className={`chord no-click me-2 ${isFirst ? 'my-bolder' : ''}`}>{movement.title}</b>
                                ))}
                            </div>
                        )}
                        <div className="chords-block">
                            <span>Расширенные секвенции: </span>
                            {item.progression.extendedHarmonyMovementsSequences.flatMap(s => withIsFirst(s.movements)).map(({ isFirst, x: movement }, i) => (
                                <b key={i} className={`chord no-click me-2 ${isFirst ? 'my-bolder' : ''}`}>{movement.title}</b>
                            ))}
                        </div>
                    </div>
                )}
            </Virtualize>
        </>
    );
}
