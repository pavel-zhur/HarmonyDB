// The original: Pages/Collection.razor.
//
// The parameters come from the query string ([SupplyParameterFromQuery]); they are set again whenever the query string
// changes. Query is also set by the page itself, by @bind (the input's change event): that value lasts until the query
// string changes. The search box's value is written as Blazor writes it (blazorValue).
// CircleOfFifths gets a copy of the chosen chords at each render: the original passes the same List, which Blazor
// counts as changed at each render; the copy is how CircleOfFifths sees a changed list here.
import { useEffect, useLayoutEffect, useRef } from 'react';
import { useLocation } from 'react-router-dom';
import { yieldTask, delay } from '../dotnet/tasks';
import { getRequired } from '../dotnet/collections';
import { SecondarySidebar } from '../components/SecondarySidebar';
import { blazorEventHandler } from '../framework/blazorEventHandler';
import { combineRefs, nativeChange, type NativeChangeEvent } from '../framework/elementRefs';
import { navigationManager } from '../framework/navigation';
import { escapeDataString } from '../framework/navigationManagerExtensions';
import { QueryParameters } from '../framework/supplyParameterFromQuery';
import { blazorValue, stringAttributes } from '../framework/attributes';
import { useCollectionComponent } from '../hooks/useCollectionComponent';
import { useEvent } from '../hooks/useEvent';
import { useStateHasChanged } from '../hooks/useStateHasChanged';
import type { ChordType } from '../lib/analysis/Models/ChordType';
import { chordTypeToString } from '../lib/analysis/Tools/Extensions';
import { enumParse } from '../lib/common/dotnet/Enum';
import { InvalidOperationException } from '../lib/common/dotnet/exceptions';
import { isDigit, isNullOrEmpty, isNullOrWhiteSpace, isWhiteSpace, trim } from '../lib/common/dotnet/text';
import type { Note } from '../lib/representations/Note';
import { RepresentationSettings } from '../lib/representations/RepresentationSettings';
import { ProgressionsSearchSource } from '../models/ProgressionsSearchSource';
import { services } from '../services/registry';
import type { SongVersionProgression } from '../services/ProgressionsCacheLoader';
import { ChordsList } from '../shared/ChordsList';
import { CircleOfFifths } from '../shared/CircleOfFifths';
import { SearchResultsInternet } from '../shared/SearchResultsInternet';
import { SearchResultsLocal, SearchResultsLocalSongsMode as SongsMode } from '../shared/SearchResultsLocal';
import './Collection.scoped.scss';
import { errorHandledTask } from '../framework/unhandledExceptions';

interface Parameters {
    query: string | null;
    modeLocalRaw: string | null;
    modeInternet: string | null;
    artistId: number | null;
    shortlistedUserId: number | null;
    likeCategoryId: number | null;
    songId: number | null;
    virtualSongArtist: string | null;
    virtualSongTitle: string | null;
    isOpen: boolean;
    isOpen2: boolean;
    externalId: string | null;
    versionId: number | null;
}

function readParameters(search: string): Parameters {
    const q = new QueryParameters(search);
    return {
        query: q.string('Query'),
        modeLocalRaw: q.string('ModeLocal'),
        modeInternet: q.string('ModeInternet'),
        artistId: q.nullableInt('ArtistId'),
        shortlistedUserId: q.nullableLong('ShortlistedUserId'),
        likeCategoryId: q.nullableInt('LikeCategoryId'),
        songId: q.nullableInt('SongId'),
        virtualSongArtist: q.string('VirtualSongArtist'),
        virtualSongTitle: q.string('VirtualSongTitle'),
        isOpen: q.bool('IsOpen'),
        isOpen2: q.bool('IsOpen2'),
        externalId: q.string('ExternalId'),
        versionId: q.nullableInt('VersionId'),
    };
}

export function Collection() {
    const identityProvider = services.identityProvider;
    const collectionNavigation = services.collectionNavigation;
    const progressionsSearchContext = services.progressionsSearchContext;
    const stateHasChanged = useStateHasChanged();
    const { collectionIndex } = useCollectionComponent();
    const progressionsSearch = services.progressionsSearch;
    const progressionsBuilder = services.progressionsBuilder;
    const progressionsCacheLoader = services.progressionsCacheLoader;
    const chordDataParser = services.chordDataParser;

    const self = useRef({
        search: null as string | null,
        parameters: null as unknown as Parameters,
        searchMode: false,
        progressionSearchChords: [] as string[],
        progressionSearchChord: null as string | null,
        progressions: null as SongVersionProgression[] | null,
        needProgressionsCache: false,
    }).current;

    // SetParametersAsync: the parameters, read again when the query string changes.
    const search = useLocation().search;
    if (self.search !== search) {
        self.search = search;
        self.parameters = readParameters(search);
    }

    const p = self.parameters;
    const { query, artistId, songId, virtualSongArtist, virtualSongTitle, isOpen, isOpen2 } = p;

    /** Enum.GetValues<SongsMode>() */
    const getAvailableModes = (): SongsMode[] => Object.values(SongsMode).filter((x): x is SongsMode => typeof x === 'number');
    const modeLocal: SongsMode = p.modeLocalRaw === null
        ? SongsMode.RatingAll
        : ((x: SongsMode) => getAvailableModes().includes(x) ? x : SongsMode.RatingAll)(enumParse<SongsMode>(SongsMode, p.modeLocalRaw));

    // OnInitializedAsync (after base): ProgressionsSearchContext.SearchResultsChanged += ...; await Task.Yield(); EnsureAuthorized
    useEvent(progressionsSearchContext.searchResultsChanged, (source: ProgressionsSearchSource) => {
        if (self.searchMode && source === ProgressionsSearchSource.ChordsView) {
            self.searchMode = false;
            self.progressionSearchChords.length = 0;
            self.progressionSearchChord = null;
            self.needProgressionsCache = false;
        }

        stateHasChanged();
    });

    useEffect(() => {
        void errorHandledTask((async () => {
            await yieldTask();
            identityProvider.ensureAuthorized();
        })());
    }, []);

    const artistNameOrNull = (): string | null => {
        const index = services.collectionIndexProvider.peek().collectionIndex;
        return self.parameters.artistId === null || index === null ? null : getRequired(index.artistsById, self.parameters.artistId).name;
    };

    const change = (newValue: string | null) => {
        navigationManager.navigateTo(collectionNavigation.collectionQueryChange(newValue, self.parameters.artistId, artistNameOrNull()), false, true);
    };

    const clearSearch = () => {
        if (self.searchMode) {
            const count = self.progressionSearchChords.length;
            if (count === 0) {
                self.searchMode = false;
            } else if (count === 1) {
                self.progressionSearchChords.length = 0;
                self.progressionSearchChord = null;
                self.searchMode = true;
            } else {
                progressionsSearchContext.clear(ProgressionsSearchSource.Collection);
                self.searchMode = true;
                self.progressionSearchChords.length = 0;
                self.progressionSearchChord = null;
                self.needProgressionsCache = false;
            }

            return;
        }

        if (progressionsSearchContext.results !== null) {
            progressionsSearchContext.clear(ProgressionsSearchSource.Collection);
            self.progressionSearchChords.length = 0;
            self.progressionSearchChord = null;
            self.needProgressionsCache = false;
            return;
        }

        navigationManager.navigateTo(collectionNavigation.collectionClearSearch(self.parameters.query, self.parameters.artistId, artistNameOrNull()));
    };

    const getSelectedSongTitle = (): string | null => {
        if (collectionIndex === null) {
            return null;
        }

        if (songId !== null) {
            const song = getRequired(collectionIndex.songsById, songId);
            return `${song.index}. ${song.artists.map(x => getRequired(collectionIndex.artistsById, x).name).join(', ')} — ${song.title}`;
        }

        return `${virtualSongArtist ?? ''} – ${virtualSongTitle ?? ''}`;
    };

    const searchInternet = () => {
        const index = services.collectionIndexProvider.peek().collectionIndex!;
        const parameters = self.parameters;
        const searchQuery = parameters.songId !== null
            ? [...getRequired(index.songsById, parameters.songId).artists.map(x => getRequired(index.artistsById, x).name), getRequired(index.songsById, parameters.songId).title].join(' ')
            : `${parameters.virtualSongArtist ?? ''} ${parameters.virtualSongTitle ?? ''}`;

        navigationManager.navigateTo(collectionNavigation.collectionSearchInternet(searchQuery));
    };

    const getGoogleSearchLink = (): string | null => {
        if (collectionIndex === null) {
            return null;
        }

        return `https://www.google.com/search?q=${escapeDataString(trim(`${artistId !== null ? getRequired(collectionIndex.artistsById, artistId).name : ''} ${query ?? ''} аккорды`))}`;
    };

    const layoutStateChanged = (arg: { isOpen: boolean; isOpen2: boolean }) => {
        navigationManager.navigateTo(collectionNavigation.collectionLayoutStateChanged(arg.isOpen, arg.isOpen2), false, true);
    };

    const progressionAdd = (note: Note, chordType: ChordType) => {
        self.progressionSearchChords.push(`[!${note.value}!]${chordTypeToString(chordType)}`);
        self.progressionSearchChord = self.progressionSearchChords.length !== 1 ? null : `${note.representation(new RepresentationSettings())}${chordTypeToString(chordType)}`;

        if (self.progressionSearchChords.length > 1) {
            progressionsSearchContext.start(
                ProgressionsSearchSource.Collection,
                progressionsSearch.extractSearch(
                    progressionsBuilder.buildProgression(
                        self.progressionSearchChords.map(x => chordDataParser.getProgressionData(x))),
                    0,
                    self.progressionSearchChords.length - 1)!);
        } else {
            stateHasChanged();
        }
    };

    const enableProgressionsSearch = async () => {
        self.searchMode = true;
        stateHasChanged();

        await delay(100);

        self.progressions = await progressionsCacheLoader.get();
        if (self.progressions === null) {
            self.needProgressionsCache = true;
            stateHasChanged();
            return;
        }
    };

    const searchInput = useRef<HTMLInputElement | null>(null);
    const searchInputShown = progressionsSearchContext.results === null && !self.searchMode;

    // OnAfterRenderAsync(firstRender). The search box is in the secondary sidebar, which MainLayout renders after this
    // page's first render (in the original, in the same render batch, before OnAfterRenderAsync): so the box is looked
    // for once MainLayout has rendered it. As there, an unset reference (the box not shown) fails.
    useEffect(() => {
        if (!isNullOrWhiteSpace(self.parameters.query)) {
            void errorHandledTask((async () => {
                await yieldTask();
                if (searchInput.current === null) {
                    throw new InvalidOperationException('ElementReference has not been configured correctly.');
                }

                searchInput.current.focus();
            })());
        }
    }, []);

    const results = progressionsSearchContext.results;
    const internetShown = results === null && (artistId !== null
        || !isNullOrWhiteSpace(query) && (![...query!].every(c => isDigit(c) || isWhiteSpace(c)) || [...query!].filter(c => isDigit(c)).length === 6));

    return (
        <>
            <SecondarySidebar isJust={!isOpen}>
                <div id="sidebar-search" ref={stringAttributes({ style: '' })}>
                    <div className="sidebar-section-body">
                        <div className="form-control-feedback form-control-feedback-end mt-2">
                            {searchInputShown ? (
                                <input type="search" className="form-control my-search" placeholder="Поиск..."
                                       ref={combineRefs<HTMLInputElement>(
                                           blazorValue(query),
                                           element => { searchInput.current = element; },
                                           nativeChange<HTMLInputElement>(blazorEventHandler(stateHasChanged, (e: NativeChangeEvent<HTMLInputElement>) => {
                                               // @bind: Query = value
                                               self.parameters = { ...self.parameters, query: e.currentTarget.value };
                                           })))}
                                       onInput={blazorEventHandler(stateHasChanged, e => change(e.currentTarget.value))} />
                            ) : (
                                <input type="search" className="form-control my-search" placeholder={results?.title ?? (self.progressions === null ? 'Инициализация...' : self.progressionSearchChord ?? 'Жмите на аккорды, пожалуйста...')} ref={stringAttributes({ disabled: 'disabled' })} />
                            )}

                            {isNullOrEmpty(query) && results === null && !self.searchMode ? (
                                <div className="form-control-feedback-icon">
                                    <i className="ph-magnifying-glass opacity-50"></i>
                                    {'\n                        '}
                                    <i className="ph-line-segments ms-2 progressions-search-button my-link my-bigger-link" onClick={blazorEventHandler(stateHasChanged, enableProgressionsSearch)}></i>
                                </div>
                            ) : (
                                <div className="form-control-feedback-icon">
                                    <a onClick={blazorEventHandler(stateHasChanged, clearSearch)} className="my-link my-bigger-link">
                                        <i className={`ph-x ${self.searchMode || results !== null ? 'progressions-search-button' : ''}`}></i>
                                    </a>
                                </div>
                            )}
                        </div>

                        {self.searchMode && (self.progressions === null ? (
                            <div className="search-note badge bg-light border-width-3 border-start border-yellow text-body rounded-start-0 mb-2 mt-3">
                                <span className="ph-line-segments"></span>
                                {'\n                        '}
                                {self.needProgressionsCache ? (
                                    <span>Надо сначала построить прогрессии. Для этого <a href="/cache">сходите в кэш</a>, пожалуйста.</span>
                                ) : (
                                    <span>Инициализация поиска прогрессий (прогресс в шапке)...</span>
                                )}
                            </div>
                        ) : (
                            <CircleOfFifths chords={[...self.progressionSearchChords]} playOnClick={false} played={progressionAdd}></CircleOfFifths>
                        ))}

                        {(!self.searchMode || results !== null) && (
                            <div className="mt-4">
                                <SearchResultsLocal query={query}
                                                    mode={modeLocal}
                                                    artistId={artistId}
                                                    shortlistedUserId={p.shortlistedUserId}
                                                    likeCategoryId={p.likeCategoryId}
                                                    songId={virtualSongArtist === null ? songId : null}></SearchResultsLocal>
                            </div>
                        )}
                    </div>
                </div>

                {internetShown && (
                    <SearchResultsInternet modeInternet={p.modeInternet}
                                           virtualSongArtist={virtualSongArtist}
                                           virtualSongTitle={virtualSongTitle}
                                           isCompact={modeLocal === SongsMode.ArtistCompact}
                                           googleSearchLink={getGoogleSearchLink()}>
                    </SearchResultsInternet>
                )}
            </SecondarySidebar>

            {isOpen && (
                <ChordsList
                    title={getSelectedSongTitle()}
                    songId={songId}
                    virtualSongArtist={virtualSongArtist}
                    virtualSongTitle={virtualSongTitle}
                    externalId={p.externalId}
                    versionId={p.versionId}
                    isOpen2={isOpen2}
                    searchInternet={searchInternet}
                    stateChanged={layoutStateChanged}></ChordsList>
            )}
        </>
    );
}
