// The original: Shared/ChordsView.razor, with ./Shared/ChordsView.razor.js (served unchanged).
//
// The component keeps the original's fields and methods on an instance (ChordsViewInstance) and is driven as Blazor
// drives a ComponentBase, because its behaviour depends on that:
// - Parameters: Blazor sets them, and runs OnParametersSetAsync, each time the owner renders it (its Action
//   parameters always count as changed). So it runs on each render of the owner — here, each time the props object is
//   a new one (React gives a component the same props object when it renders for its own state). Each such run reloads
//   the chords and records a visit, as in the original.
// - ShouldRender: the original renders only when a hash of some of its state changed. Here StateHasChanged compares
//   the same values as their .NET hashes compare (see getRenderKey) and, when nothing changed, the component returns
//   the very element tree it rendered last, so React leaves the DOM and the child components as they were.
// - Event handlers render after their synchronous part and again after they complete (HandleEventAsync); EventCallback
//   parameters given to LikesBlock and CategoriesButton run as this component's events.
// - OnAfterRenderAsync after each render: ChordsView.razor.js bindAll(Receiver.Reference), which makes the chords
//   clickable (Receiver → ReceiverInstance → ChordClicked).

import { useLayoutEffect, useRef, type ReactNode } from 'react';
import { configuration } from '../configuration';
import { getRequired } from '../dotnet/collections';
import { delay } from '../dotnet/tasks';
import { blazorChecked, stringAttributes } from '../framework/attributes';
import { combineRefs, nativeChange } from '../framework/elementRefs';
import { getLogger } from '../framework/logging';
import { navigationManager } from '../framework/navigation';
import { errorHandledTask } from '../framework/unhandledExceptions';
import { useCollectionComponent } from '../hooks/useCollectionComponent';
import { useEvent } from '../hooks/useEvent';
import { useStateHasChanged } from '../hooks/useStateHasChanged';
import { importJsModule, invokeJs, type ChordsViewRazorJs } from '../interop/jsRuntime';
import type { ChordsProgression } from '../lib/analysis/Models/ChordsProgression';
import { ChordType } from '../lib/analysis/Models/ChordType';
import type { StructureLink } from '../lib/analysis/Models/Structure/StructureLink';
import { deserializeLoop } from '../lib/analysis/Tools/LoopSerializationExtensions';
import { getTitleFromBeginningNote } from '../lib/analysis/Tools/TonalitiesExtensions';
import { ProgressionsVisualizer } from '../lib/analysis/Services/ProgressionsVisualizer';
import type { Chords } from '../lib/api/HarmonyDB.Source.Api.Model/V1/Chords';
import { VersionCollectiveType } from '../lib/api/OneShelf.Frontend.Api.Model/V3/Databasish/VersionCollectiveType';
import type { Guid } from '../lib/common/dotnet/Guid';
import { ArgumentException, ArgumentOutOfRangeException, InvalidOperationException, OperationCanceledException } from '../lib/common/dotnet/exceptions';
import { at, compareNumbers, orderBy, orderByKey } from '../lib/common/dotnet/Linq';
import { intDiv } from '../lib/common/dotnet/Numbers';
import { isNullOrWhiteSpace } from '../lib/common/dotnet/text';
import { Uri } from '../lib/common/dotnet/Uri';
import { NoteAlteration } from '../lib/common/HarmonyDB.Common/NoteAlteration';
import { transposition } from '../lib/common/HarmonyDB.Common/Transposition/TranspositionExtensions';
import { selectSingle, withIndices } from '../lib/common/OneShelf.Common/Extensions';
import { hasFlag, Note, RepresentationSettings, SignFlat, SignSharp, SimplificationMode } from '../lib/representations';
import type { CollectionIndex } from '../models/CollectionIndex';
import { ProgressionsSearchSource } from '../models/ProgressionsSearchSource';
import type { SongVersionProgression } from '../services/ProgressionsCacheLoader';
import { services } from '../services/registry';
import { CategoriesButton } from './CategoriesButton';
import { ChordsView_AddedBy } from './ChordsView.AddedBy';
import { CircleOfFifths } from './CircleOfFifths';
import { LikesBlock } from './LikesBlock';
import './ChordsView.scoped.scss';

export interface ChordsViewProps {
    nextVisible?: boolean;
    previousVisible?: boolean;
    onNext: () => void;
    onPrevious: () => void;
    title: string;
    externalId?: string | null;
    versionId?: number | null;
    songId: number | null;
    stateChanged: (value: { isOpen: boolean; isOpen2: boolean }) => void;
    virtualSongArtist?: string | null;
    virtualSongTitle?: string | null;
}

enum State {
    Loading,
    KnownUnsupported,
    HideUnstable,
    Failed,
    Success,
}

/** (Note note, ChordType chordType) */
interface SpecialTonicCandidate {
    readonly note: Note;
    readonly chordType: ChordType;
}

/** (List<StructureLink> loops, ChordsProgression progression) */
interface LoopsMemory {
    readonly loops: StructureLink[];
    readonly progression: ChordsProgression;
}

/** Nullable<T>.Value */
function value<T>(x: T | null): T {
    if (x === null) throw new InvalidOperationException('Nullable object must have a value.');
    return x;
}

/** {x:000} */
function format000(x: number): string {
    return x < 0 ? `-${String(-x).padStart(3, '0')}` : String(x).padStart(3, '0');
}

const logger = getLogger('OneShelf.Frontend.Web.Shared.ChordsView');

class ChordsViewInstance {
    // @inject, in the original's order
    readonly inputParser = services.inputParser;
    readonly dataProvider = services.dataProvider;
    readonly collectionIndexProvider = services.collectionIndexProvider;
    readonly identityProvider = services.identityProvider;
    readonly instantActions = services.instantActions;
    readonly jsFunctions = services.jsFunctions;
    readonly collectionNavigation = services.collectionNavigation;
    readonly searchContext = services.searchContext;
    readonly receiver = services.receiver;
    readonly receiverInstance = services.receiverInstance;
    readonly player = services.player;
    readonly preferences = services.preferences;
    readonly progressionsSearch = services.progressionsSearch;
    readonly progressionsCacheLoader = services.progressionsCacheLoader;
    readonly progressionsSearchContext = services.progressionsSearchContext;
    readonly progressionsBuilder = services.progressionsBuilder;
    readonly chordDataParser = services.chordDataParser;
    readonly options = services.frontendOptions;
    readonly progressionsVisualizer = services.progressionsVisualizer;
    readonly indexExtractor = services.indexExtractor;

    // [Parameter]s
    nextVisible = false;
    previousVisible = false;
    onNext: () => void = null!;
    onPrevious: () => void = null!;
    title: string = null!;
    externalId: string | null = null;
    versionId: number | null = null;
    songId: number | null = null;
    stateChanged: (value: { isOpen: boolean; isOpen2: boolean }) => void = null!;
    virtualSongArtist: string | null = null;
    virtualSongTitle: string | null = null;

    private _chords: Chords | null = null;
    private _state = State.Loading;
    private _representationSettings = new RepresentationSettings();

    private _circleOfFifths = false;

    private _loops = false;
    private _loopId: number | null = null;
    private _loopsMemory: LoopsMemory | null = null;
    private _loopsCustomAttributes: ReadonlyMap<number, string> | null = null;

    private _specialTonic = false;
    private _specialTonicCandidate: SpecialTonicCandidate | null = null;
    private _zoom = 0;
    private _zooming = false;

    private _searchingFrom: number | null = null;
    private _progression: ChordsProgression | null = null;
    private _needProgressionsCache = false;
    private _searchMode = false;

    // private readonly Timer _timer = new(650); AutoReset = false
    private _timer: ReturnType<typeof setTimeout> | undefined;
    private _timerElapsedSubscribed = false;

    // ComponentBase
    private _initialized = false;
    private _hasNeverRendered = true;
    private _hasPendingQueuedRender = false;
    private _renderedKey: unknown[] | null = null;
    private _rendered: ReactNode = null;
    private _renderedSinceAfterRender = false;
    private _firstAfterRender = true;

    constructor(private readonly forceRender: () => void) {
    }

    // CollectionComponentBase
    private get collectionIndex(): CollectionIndex | null {
        return this.collectionIndexProvider.peek().collectionIndex;
    }

    private get collectionIndexFailed(): boolean {
        return this.collectionIndexProvider.peek().failed;
    }

    private retryCollectionIndex(): void {
        this.collectionIndexProvider.sync();
    }

    // --- Blazor's ComponentBase ---

    assignParameters(props: ChordsViewProps): void {
        this.nextVisible = props.nextVisible ?? false;
        this.previousVisible = props.previousVisible ?? false;
        this.onNext = props.onNext;
        this.onPrevious = props.onPrevious;
        this.title = props.title;
        this.externalId = props.externalId ?? null;
        this.versionId = props.versionId ?? null;
        this.songId = props.songId ?? null;
        this.stateChanged = props.stateChanged;
        this.virtualSongArtist = props.virtualSongArtist ?? null;
        this.virtualSongTitle = props.virtualSongTitle ?? null;
    }

    /**
     * SetParametersAsync: the lifecycle's task is the renderer's to handle (a failure is logged, with the error bar; a
     * cancellation is ignored, as ComponentBase ignores a cancelled lifecycle task).
     */
    setParameters(props: ChordsViewProps): void {
        this.assignParameters(props);
        if (!this._initialized) {
            this._initialized = true;
            void errorHandledTask(this.runInitAndSetParametersAsync());
        } else {
            void errorHandledTask(this.callOnParametersSetAsync());
        }
    }

    private async runInitAndSetParametersAsync(): Promise<void> {
        const task = this.onInitializedAsync();
        this.stateHasChanged();
        try {
            await task;
        } catch (e) {
            if (!(e instanceof OperationCanceledException)) throw e;
        }

        await this.callOnParametersSetAsync();
    }

    private async callOnParametersSetAsync(): Promise<void> {
        const task = this.onParametersSetAsync();
        this.stateHasChanged();
        await task;
        this.stateHasChanged();
    }

    stateHasChanged(): void {
        if (this._hasPendingQueuedRender) return;
        if (this._hasNeverRendered || this.shouldRender()) {
            this._hasPendingQueuedRender = true;
            this.forceRender();
        }
    }

    /** HandleEventAsync: StateHasChanged after the synchronous part and, for an incomplete task, after it completes. */
    handleEvent(handler: () => void | Promise<void>): Promise<void> {
        const result = handler();
        this.stateHasChanged();
        if (result instanceof Promise) {
            return result.then(() => this.stateHasChanged());
        }

        return Promise.resolve();
    }

    /** An EventCallback whose receiver is this component. */
    private eventCallback<T>(handler: (arg: T) => void | Promise<void>): (arg: T) => Promise<void> {
        return arg => this.handleEvent(() => handler(arg));
    }

    /** What the render function gives: a new tree when a render is due, else the last one. */
    render(): ReactNode {
        if (this._hasNeverRendered || this._hasPendingQueuedRender) {
            this._hasNeverRendered = false;
            this._hasPendingQueuedRender = false;
            this._renderedKey = this.getRenderKey();
            this._rendered = this.buildRenderTree();
            this._renderedSinceAfterRender = true;
        }

        return this._rendered;
    }

    afterCommit(): void {
        if (!this._renderedSinceAfterRender) return;
        this._renderedSinceAfterRender = false;
        const firstRender = this._firstAfterRender;
        this._firstAfterRender = false;
        void errorHandledTask(this.onAfterRenderAsync(firstRender));
    }

    // --- the original's code ---

    private shouldRender(): boolean {
        const key = this.getRenderKey();
        if (this._renderedKey !== null && sameRenderKey(this._renderedKey, key)) return false;
        this._renderedKey = key;
        return true;
    }

    /**
     * The values GetHash combines, compared as their .NET hash codes compare: records by their members, the rest by
     * reference; a null int?/enum? hashes as 0, the same as the value 0 (so _loopId null and 0, a chord type null and
     * Major, an alteration null and Flat count as the same — selecting ♭ when no alteration was chosen does not
     * render, as in the original).
     */
    private getRenderKey(): unknown[] {
        return [
            this.nextVisible, this.previousVisible, this.title, this.externalId, this.versionId ?? 0,
            this.songId ?? 0, this.virtualSongArtist, this.virtualSongTitle, this._specialTonicCandidate?.note ?? null, this._specialTonicCandidate?.chordType ?? 0, this._state, this._representationSettings, this._circleOfFifths,
            this.collectionIndex, this._specialTonic, this._loops, this._loopId ?? 0,
            this._chords, this._zooming, this._zoom, this._searchMode, this._needProgressionsCache, this._progression === null,
        ];
    }

    private async onAfterRenderAsync(_firstRender: boolean): Promise<void> {
        const module = await importJsModule<ChordsViewRazorJs>('./Shared/ChordsView.razor.js');
        await invokeJs<void>(module.bindAll, this.receiver.reference);
    }

    private async onInitializedAsync(): Promise<void> {
        // base.OnInitializedAsync(): CollectionChanged is followed by useCollectionComponent;
        // ProgressionsSearchContext.SearchResultsChanged by useEvent.

        this.receiverInstance.setChordClick((chordData, chordIndex) => this.chordClicked(chordData, chordIndex));

        this._circleOfFifths = await this.preferences.isCircleOfFifthsVisible();
        this._zoom = await this.preferences.getZoom();
        this._timerElapsedSubscribed = true;
    }

    progressionsSearchContextOnSearchResultsChanged(_searchSource: ProgressionsSearchSource): void {
        if (this._progression === null || this.progressionsSearchContext.results === null) {
            this._searchMode = false;
        }

        this._representationSettings = this._representationSettings.with({
            customAttributes: this.getCustomAttributes(),
        });

        this.stateHasChanged();
    }

    private async chordClicked(chordData: string, chordIndex: number): Promise<void> {
        if (!this._searchMode || this._progression === null) {
            await this.player.playChord(chordData);
            return;
        }

        if (this._searchingFrom !== null) {
            if (this._searchingFrom < chordIndex) {
                const search = this.progressionsSearch.extractSearch(this._progression, this._searchingFrom, chordIndex);
                if (search === null) {
                    console.log(`Extraction failed ${this._searchingFrom} -> ${chordIndex}.`);
                } else {
                    this.progressionsSearchContext.start(ProgressionsSearchSource.ChordsView, search);
                }

                this._searchingFrom = null;

                this.stateHasChanged();
                return;
            }
        }

        this._searchingFrom = chordIndex;
        this._representationSettings = this._representationSettings.with({
            customAttributes: new Map([[chordIndex, ProgressionsVisualizer.AttributeSearchFirst]]),
        });

        this.stateHasChanged();
    }

    async onNewCollectionIndexReceived(): Promise<void> {
        this.updateRepresentationSettings();
    }

    private updateRepresentationSettings(): void {
        this._specialTonicCandidate = null;
        this._searchingFrom = null;
        this._progression = null;
        this._needProgressionsCache = false;

        const collectionIndex = this.collectionIndex;
        if (collectionIndex === null) return;
        const myId = this.identityProvider.requiredIdentity.id;
        const transpose = this.songId === null
            ? 0
            : orderBy(getRequired(collectionIndex.songsById, this.songId).likes, (a, b) => compareNumbers(a.likeCategoryId !== null ? 2 : 1, b.likeCategoryId !== null ? 2 : 1))
                .find(x => x.versionId === this.versionId && x.userId === myId)?.transpose ?? 0;
        this._representationSettings = new RepresentationSettings({ transpose, customAttributes: this.getCustomAttributes() });

        console.log('UpdateRepresentationSettings');
    }

    private getCustomAttributes(): ReadonlyMap<number, string> {
        if (this._loopId !== null && this._loops) {
            this.setLoopsCustomAttributes();
            return this._loopsCustomAttributes!;
        }

        const none = new Map<number, string>();
        if (this.progressionsSearchContext.results === null || isNullOrWhiteSpace(this.externalId)) {
            return none;
        }

        const progressions = this.progressionsCacheLoader.peek();
        if (progressions === null) {
            return none;
        }

        this._progression = this.getProgression(progressions);
        if (this._progression === null) {
            return none;
        }

        return this.progressionsVisualizer.buildCustomAttributesForSearch(this._progression, this.progressionsSearchContext.results.result);
    }

    private async onParametersSetAsync(): Promise<void> {
        this.updateRepresentationSettings();

        const externalId = this.externalId;
        if (externalId !== null) {
            try {
                this._chords = null;
                this._loopsMemory = null;
                this._loopId = null;
                this._state = State.Loading;
                this._loops = false;
                try {
                    // chords in memory: a completed task, the await goes on at once (no "loading" render between)
                    const gotten = this.dataProvider.getChords(externalId);
                    const chords = gotten instanceof Promise ? await gotten : gotten;
                    if (externalId === this.externalId) {
                        this._chords = chords;
                        if (this._representationSettings.isVariableWidth !== this._chords.output.isVariableWidth) {
                            this._representationSettings = this._representationSettings.with({ isVariableWidth: this._chords.output.isVariableWidth });
                        }

                        if (configuration.getBoolean(State[State.HideUnstable]) && !this._chords.isStable) {
                            this._state = State.HideUnstable;
                        } else {
                            this._state = State.Success;
                        }
                    }
                } catch (e) {
                    // catch (Exception) when (_state == State.Success)
                    if (this._state === State.Success) return;
                    throw e;
                }
            } catch (e) {
                this._state = State.Failed;
                this._chords = null;
                logger.logError(e, 'Chords loading failed.');
            }
        } else {
            this._chords = null;
            this._state = State.KnownUnsupported;
        }

        await this.visit();
    }

    private async refreshChords(): Promise<void> {
        await this.onParametersSetAsync();
    }

    private flat(): void {
        this._representationSettings = this._representationSettings.with({ alteration: NoteAlteration.Flat });
        this.stateHasChanged();
    }

    private sharp(): void {
        this._representationSettings = this._representationSettings.with({ alteration: NoteAlteration.Sharp });
        this.stateHasChanged();
    }

    private async up(): Promise<void> {
        this._representationSettings = this._representationSettings.up().with({
            relativeTo: null,
        });
        this._specialTonicCandidate = null;
        this._specialTonic = false;
        await this.visit();
        this.stateHasChanged();
    }

    private async down(): Promise<void> {
        this._representationSettings = this._representationSettings.down().with({
            relativeTo: null,
        });
        this._specialTonicCandidate = null;
        this._specialTonic = false;
        await this.visit();
        this.stateHasChanged();
    }

    private async visit(): Promise<void> {
        const collectionIndex = this.collectionIndex;
        if (this.songId !== null && collectionIndex === null) return;

        const song = this.songId === null ? null : getRequired(collectionIndex!.songsById, this.songId);
        await this.instantActions.visitedChords(
            this.versionId === null ? null : new Uri(getRequired(collectionIndex!.versionsById, this.versionId).version.uri.toString()),
            this.externalId,
            this.searchContext.peekLastRequested(),
            this._representationSettings.transpose,
            this.songId,
            // string? in the original: VirtualSongArtist / VirtualSongTitle may be null.
            song === null ? this.virtualSongArtist! : selectSingle(song.artists.map(x => getRequired(collectionIndex!.artistsById, x).name).join(', '), x => isNullOrWhiteSpace(x) ? 'n/a' : x),
            song === null ? this.virtualSongTitle! : song.title,
            this._chords?.source ?? null);
    }

    private getCurrentLevel(): number | null {
        const collectionIndex = this.collectionIndex!;
        if (this.songId === null) {
            if (this.externalId === null) throw new ArgumentException('Value cannot be null. (Parameter \'key\')');
            const level = collectionIndex.virtualLikeLevels.get(this.externalId);
            return level !== undefined && level.likeCategoryId === null ? level.level : null;
        }

        const myId = this.identityProvider.requiredIdentity.id;
        const likes = getRequired(collectionIndex.songsById, this.songId).likes
            .filter(x => x.likeCategoryId === null)
            .filter(x => x.userId === myId && x.versionId === this.versionId);
        if (likes.length > 1) throw new InvalidOperationException('Sequence contains more than one matching element');
        const x = likes[0];
        return x === undefined ? null : x.transpose === this._representationSettings.transpose ? x.level : 60; // if it just doesn't match the Transpose, then let no button be selected
    }

    private async onLikesUpdate(level: number | null, likeCategoryId: number | null): Promise<void> {
        if (this.songId !== null) {
            await this.instantActions.updateLike(
                this.songId,
                value(this.versionId),
                level,
                level !== null ? this._representationSettings.transpose : null,
                likeCategoryId);
        } else {
            await this.instantActions.versionImport(
                this.externalId!,
                value(level),
                this._representationSettings.transpose,
                likeCategoryId);
        }
    }

    private async adminRemoveVersion(): Promise<void> {
        if (await this.jsFunctions.confirm('Точно хотите удалить эти аккорды из этой песни?')) {
            await this.instantActions.versionRemove(value(this.versionId));
            navigationManager.navigateTo(this.collectionNavigation.searchResultsLocalSongChordsRemoved(value(this.songId)));
        }
    }

    private async invertCircleOfFifths(): Promise<void> {
        this._circleOfFifths = !this._circleOfFifths;
        if (!this._circleOfFifths) this._specialTonicCandidate = null;
        await this.preferences.circleOfFifthsVisible(this._circleOfFifths);
    }

    private async invertLoops(): Promise<void> {
        this._loops = !this._loops;
        if (!this._loops || this._chords === null) {
            this._loopId = null;
            this._loopsMemory = null;
            this._loopsCustomAttributes = null;

            this._representationSettings = this._representationSettings.with({
                customAttributes: this.getCustomAttributes(),
            });
        } else {
            const chords = this._chords.output.asChords(this._representationSettings);
            const progression = this.progressionsBuilder.buildProgression(chords.map(x => this.chordDataParser.getProgressionData(x)));
            const loops = this.indexExtractor.findStructureLinks('does not matter', progression.compact());
            this._loopsMemory = { loops, progression };

            this.setLoopsCustomAttributes();

            this._representationSettings = this._representationSettings.with({
                customAttributes: this.getCustomAttributes(),
            });
        }
    }

    private setLoopsCustomAttributes(): void {
        if (this._loopId === null) {
            this._loopsCustomAttributes = new Map<number, string>();
            return;
        }

        const loop = at(this._loopsMemory!.loops, this._loopId);
        const searchProgression = this.inputParser.parseSequence(getTitleFromBeginningNote(loop.normalized, 0, true));
        const found = this.progressionsSearch.search([this._loopsMemory!.progression], searchProgression);

        this._loopsCustomAttributes = this.progressionsVisualizer.buildCustomAttributesForSearch(this._loopsMemory!.progression, found);
    }

    private async invertSearch(): Promise<void> {
        if (this._searchMode) {
            this._searchMode = false;
            this._searchingFrom = null;
            this._needProgressionsCache = false;
            this._representationSettings = this._representationSettings.with({
                customAttributes: this.getCustomAttributes(),
            });
            this.stateHasChanged();
            return;
        }

        this._searchMode = true;
        this.stateHasChanged();

        await delay(100);

        const progressions = await this.progressionsCacheLoader.get();
        if (progressions === null) {
            this._needProgressionsCache = true;
            this.stateHasChanged();
            return;
        }

        this._progression = this.getProgression(progressions);
        if (this._progression === null) {
            this._searchMode = false;
        }

        this.stateHasChanged();
    }

    private getProgression(progressions: SongVersionProgression[]): ChordsProgression | null {
        // progressions.SingleOrDefault(x => x.version.ExternalId == ExternalId).progression
        const matching = progressions.filter(x => x.version.externalId === this.externalId);
        if (matching.length > 1) throw new InvalidOperationException('Sequence contains more than one matching element');
        const result = matching[0]?.progression ?? null;
        if (result !== null) {
            return result;
        }

        if (this._chords !== null) {
            return this.progressionsBuilder.buildProgression(this._chords.output.asChords(this._representationSettings).map(x => this.chordDataParser.getProgressionData(x)));
        }

        return null;
    }

    private invertSpecialTonic(): void {
        this._specialTonic = !this._specialTonic;
        this._representationSettings = this._representationSettings.with({
            relativeTo: this._specialTonic ? { note: value(this._specialTonicCandidate).note.value, major: value(this._specialTonicCandidate).chordType === ChordType.Major } : null,
        });
    }

    private circleOfFifthsPlayed(note: Note, chordType: ChordType): void {
        this._specialTonicCandidate = { note, chordType };
        this._specialTonic = note.value === this._representationSettings.relativeTo?.note && (chordType === ChordType.Major) === this._representationSettings.relativeTo?.major;
        this.stateHasChanged();
    }

    dispose(): void {
        // base.Dispose(); SearchResultsChanged -= ...: the hooks unsubscribe.
        this.player.noChord();
        clearTimeout(this._timer);
        this._timer = undefined;
        this._timerElapsedSubscribed = false;
    }

    private zoomInit(): void {
        this._zooming = true;
    }

    private zoom(delta: number): void {
        this._zoom += delta;
        clearTimeout(this._timer);
        this._timer = setTimeout(() => this.timerElapsed(), 650);
    }

    private timerElapsed(): void {
        this._timer = undefined;
        if (!this._timerElapsedSubscribed) return;
        // Task.Run(() => Preferences.SetZoom(_zoom)): not awaited, its failure unobserved.
        void this.preferences.setZoom(this._zoom).catch(() => undefined);
    }

    private collectiveUpdate(collectiveId: Guid): void {
        navigationManager.navigateTo(this.collectionNavigation.collectiveUpdate(collectiveId));
    }

    private collectiveDelete(collectiveId: Guid): void {
        navigationManager.navigateTo(this.collectionNavigation.collectiveDelete(collectiveId));
    }

    private collectiveRefreshTag(collectiveId: Guid): void {
        navigationManager.navigateTo(this.collectionNavigation.collectiveRefresh(collectiveId));
    }

    private collectiveAdd(): void {
        try {
            navigationManager.navigateTo(this.collectionNavigation.collectiveAdd(value(this.versionId)));
        } catch (e) {
            logger.logError(e, 'Error trying to add a collective.');
        }
    }

    private simplify(mode: SimplificationMode): void {
        const adding = !hasFlag(this._representationSettings.simplification, mode);
        console.log(`${adding ? 'True' : 'False'}, ${SimplificationMode[mode]}`);

        let mode2 = mode;
        if (!adding && mode === SimplificationMode.Remove9AndMore) {
            mode2 = SimplificationMode.Remove7;
        }

        let newValue: SimplificationMode = adding
            ? this._representationSettings.simplification | mode2
            : this._representationSettings.simplification & ~mode2;

        if (!adding && mode === SimplificationMode.Remove7) {
            newValue |= SimplificationMode.Remove9AndMore;
        }

        this._representationSettings = this._representationSettings.with({
            simplification: newValue,
        });
    }

    private setSimplification(mode: SimplificationMode): void {
        this._representationSettings = this._representationSettings.with({
            simplification: mode,
        });
    }

    private searchClear(): void {
        this.progressionsSearchContext.clear(ProgressionsSearchSource.ChordsView);
        this._searchMode = false;
        this.stateHasChanged();
    }

    private loopIdClick(id: number): void {
        this._loopId = id;

        this._representationSettings = this._representationSettings.with({
            customAttributes: this.getCustomAttributes(),
        });
    }

    // --- the markup ---

    private buildRenderTree(): ReactNode {
        const on = (handler: () => void | Promise<void>) => () => {
            void errorHandledTask(this.handleEvent(handler));
        };

        const onPrevious = this.onPrevious;
        const onNext = this.onNext;

        return (
            <>
                <div className="page-header bg-info bg-opacity-75 mt-4 my-header my-content-addition">
                    <div className="page-header-content d-block">
                        <div className="d-flex justify-content-between">
                            <div className="breadcrumb">
                                <h5 className="my-pt-25">{this.title}</h5>
                            </div>
                            {'\n\n            '}
                            <div className="d-flex">
                                {this.previousVisible && (
                                    <a className="d-flex align-items-center text-body py-2 ms-2 collapsed my-link" onClick={on(onPrevious)}>
                                        <i className="ph-arrow-left my-white"></i>
                                    </a>
                                )}
                                {this.nextVisible && (
                                    <a className="d-flex align-items-center text-body py-2 ms-2 collapsed my-link" onClick={on(onNext)}>
                                        <i className="ph-arrow-right my-white"></i>
                                    </a>
                                )}
                                <a className="d-flex align-items-center text-body py-2 ms-2 collapsed my-link" onClick={on(() => this.stateChanged({ isOpen: true, isOpen2: false }))}>
                                    <i className="ph-x-circle my-white"></i>
                                </a>
                            </div>
                        </div>
                    </div>
                </div>
                {this.collectionIndexFailed ? (
                    <>
                        <p><em>Не получилось загрузить, может нету интернета, исполнителей.</em></p>
                        {'\n    '}
                        <button className="btn btn-secondary" onClick={on(() => this.retryCollectionIndex())}>Повтор</button>
                    </>
                ) : this.collectionIndex === null ? (
                    <div className="small-loading"><img className="music-loading" src="img/music-loading.gif" title="Загрузка..." alt="Загрузка..." /></div>
                ) : this.buildContent(this.collectionIndex, on)}
            </>
        );
    }

    private buildContent(collectionIndex: CollectionIndex, on: (handler: () => void | Promise<void>) => () => void): ReactNode {
        const rs = this._representationSettings;
        const myId = this.identityProvider.requiredIdentity.id;
        const chords = this._chords;

        return (
            <>
                <div className="likes-links">
                    {this._state === State.Success && (
                        <div className=" row transpose-group  ">
                            <div className="col col-auto ">
                                <div className="input-group mb-3">
                                    <div className="input-group-prepend">
                                        <button onClick={on(() => this.down())} className="btn btn-secondary rounded-0 rounded-start"><svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 448 512"><path d="M432 256c0 17.7-14.3 32-32 32L48 288c-17.7 0-32-14.3-32-32s14.3-32 32-32l352 0c17.7 0 32 14.3 32 32z"/></svg></button>
                                    </div>
                                    {'\n                    '}
                                    <span className={`transpose form-control ${rs.transpose === 0 ? '' : 'transpose-highlighted'}`}>{transposition(rs.transpose)}</span>
                                    {'\n\n                    '}
                                    <div className="input-group-prepend">
                                        <button onClick={on(() => this.up())} className="btn btn-secondary  rounded-0 rounded-end"><svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 448 512"><path d="M256 80c0-17.7-14.3-32-32-32s-32 14.3-32 32V224H48c-17.7 0-32 14.3-32 32s14.3 32 32 32H192V432c0 17.7 14.3 32 32 32s32-14.3 32-32V288H400c17.7 0 32-14.3 32-32s-14.3-32-32-32H256V80z"/></svg></button>
                                    </div>
                                </div>
                            </div>
                            {'\n\n            '}
                            <div className="col-auto ps-0">
                                <div className="input-group mb-3 songsconstants">
                                    <div className="input-group-prepend">
                                        <input className=" btn-check" name="SongsConstants" ref={combineRefs<HTMLInputElement>(blazorChecked(rs.alteration === NoteAlteration.Flat), nativeChange<HTMLInputElement>(on(() => this.flat())))} type="radio" id="preferFlat"/>
                                        {'\n\n                        '}
                                        <label className="btn btn-outline-secondary rounded-0 rounded-start" htmlFor="preferFlat">{SignFlat}{SignFlat}{SignFlat}</label>
                                    </div>
                                    {'\n                    '}
                                    <div className="input-group-prepend">
                                        <input className="btn-check" name="SongsConstants" ref={combineRefs<HTMLInputElement>(blazorChecked(rs.alteration === NoteAlteration.Sharp), nativeChange<HTMLInputElement>(on(() => this.sharp())))} type="radio" id="preferSharp"/>
                                        {'\n\n                        '}
                                        <label className="btn btn-outline-secondary rounded-0 rounded-end" htmlFor="preferSharp">{SignSharp}{SignSharp}{SignSharp}</label>
                                    </div>
                                </div>
                            </div>
                            {'\n\n            '}
                            <div className="col-auto ps-0">
                                <div className="input-group mb-3 songsconstants">
                                    {!this._zooming ? (
                                        <div className="input-group-prepend">
                                            <button onClick={on(() => this.zoomInit())} className="btn btn-outline-secondary  rounded-0 rounded-end rounded-start">
                                                <span className="ph-magnifying-glass-plus"></span>
                                            </button>
                                        </div>
                                    ) : (
                                        <>
                                            <div className="input-group-prepend">
                                                <button onClick={on(() => this.zoom(-5))} className="btn btn-outline-secondary  rounded-0 rounded-start">
                                                    <span className="ph-magnifying-glass-minus"></span>
                                                    {'\n                                '}
                                                    <span className="ph-magnifying-glass-minus"></span>
                                                </button>
                                            </div>
                                            {'\n                        '}
                                            <div className="input-group-prepend">
                                                <button onClick={on(() => this.zoom(-1))} className="btn btn-outline-secondary  rounded-0">
                                                    <span className="ph-magnifying-glass-minus"></span>
                                                </button>
                                            </div>
                                            {'\n                        '}
                                            <div className="input-group-prepend">
                                                <button onClick={on(() => this.zoom(100 - this._zoom))} className="btn btn-outline-secondary  rounded-0">
                                                    <span className="ph-arrow-counter-clockwise"></span>
                                                </button>
                                            </div>
                                            {'\n                        '}
                                            <div className="input-group-prepend">
                                                <button onClick={on(() => this.zoom(1))} className="btn btn-outline-secondary  rounded-0">
                                                    <span className="ph-magnifying-glass-plus"></span>
                                                </button>
                                            </div>
                                            {'\n                        '}
                                            <div className="input-group-prepend">
                                                <button onClick={on(() => this.zoom(5))} className="btn btn-outline-secondary  rounded-0 rounded-end">
                                                    <span className="ph-magnifying-glass-plus"></span>
                                                    {'\n                                '}
                                                    <span className="ph-magnifying-glass-plus"></span>
                                                </button>
                                            </div>
                                        </>
                                    )}
                                </div>
                            </div>
                        </div>
                    )}
                    {chords !== null && (
                        <span className="badge bg-light border-start border-width-3 text-body rounded-start-0 border-yellow me-2 mb-1 mt-1">
                            <span>Ссылка:</span>
                            {'\n            '}
                            <a href={chords.sourceUri.toString()} target="_blank">{chords.source}</a>
                        </span>
                    )}
                    {this.songId !== null && (
                        <span className="badge bg-light border-start border-width-3 text-body rounded-start-0 border-yellow me-2 mb-1 mt-1">
                            <ChordsView_AddedBy versionId={this.versionId}></ChordsView_AddedBy>
                        </span>
                    )}
                    {(this.songId !== null || this.externalId !== null) && this._state === State.Success && (
                        <>
                            <CategoriesButton versionId={this.versionId} update={this.eventCallback(v => this.onLikesUpdate(v.level, v.likeCategoryId))} songId={this.songId} currentTranspose={rs.transpose}/>
                            <span className="dropdown me-1">
                                <a href="#" className={`my-link badge ${rs.simplification > 0 ? 'bg-danger border-danger bg-opacity-75' : 'bg-light text-body border-indigo'} border-start border-width-3 rounded-start-0 d-inline-flex align-items-center dropdown-toggle mb-1 mt-1`} data-bs-toggle="dropdown" aria-expanded="false">Проще</a>
                                {'\n            '}
                                <div className="dropdown-menu dropdown-menu-end" ref={stringAttributes({ style: '' })}>
                                    {rs.simplification !== SimplificationMode.None && (
                                        <a onClick={on(() => this.setSimplification(SimplificationMode.None))} className="dropdown-item my-link">
                                            {'\n                        Сделать оригинальные!\n                    '}
                                        </a>
                                    )}
                                    {rs.simplification !== SimplificationMode.All && (
                                        <a onClick={on(() => this.setSimplification(SimplificationMode.All))} className="dropdown-item my-link">
                                            {'\n                        Am\n                        '}
                                            <span className="opacity-75">{' совсем простые'}</span>
                                        </a>
                                    )}
                                    {rs.simplification !== (SimplificationMode.Remove7 | SimplificationMode.Remove6 | SimplificationMode.RemoveSus) && (
                                        <a onClick={on(() => this.setSimplification(SimplificationMode.Remove7 | SimplificationMode.Remove6 | SimplificationMode.RemoveSus))} className="dropdown-item my-link">
                                            {'\n                        Am/G\n                        '}
                                            <span className="opacity-75">{' простые + басы'}</span>
                                        </a>
                                    )}
                                    {rs.simplification !== (SimplificationMode.RemoveBass | SimplificationMode.Remove6 | SimplificationMode.RemoveSus) && (
                                        <a onClick={on(() => this.setSimplification(SimplificationMode.RemoveBass | SimplificationMode.Remove6 | SimplificationMode.RemoveSus))} className="dropdown-item my-link">
                                            {'\n                        Am7\n                        '}
                                            <span className="opacity-75">{' простые 7'}</span>
                                        </a>
                                    )}
                                    {rs.simplification !== (SimplificationMode.Remove9AndMore | SimplificationMode.Remove6 | SimplificationMode.RemoveSus) && (
                                        <a onClick={on(() => this.setSimplification(SimplificationMode.Remove9AndMore | SimplificationMode.Remove6 | SimplificationMode.RemoveSus))} className="dropdown-item my-link">
                                            {'\n                        Am7/G\n                    '}
                                        </a>
                                    )}
                                    <div className="dropdown-divider"></div>
                                    {simplificationModes.map(({ mode, title }, i) => (
                                        <a key={i} onClick={on(() => this.simplify(mode))} className="dropdown-item my-link">
                                            <span className={`border border-width-1 ${!hasFlag(rs.simplification, mode) ? 'border-info ph-plus' : 'border-ping ph-minus'} rounded-pill me-2`}></span>
                                            {'\n                        '}
                                            {hasFlag(rs.simplification, mode) ? 'Показать ' : 'Спрятать '}
                                            {' '}
                                            {title}
                                        </a>
                                    ))}
                                </div>
                            </span>
                        </>
                    )}
                    {this.buildMyVersion(collectionIndex, myId, on)}
                    {this._state === State.Success
                        && this.songId !== null
                        && this.versionId !== null
                        && !getRequired(collectionIndex.songsById, this.songId).versions.some(x => x.collectiveType !== null && x.userId === myId)
                        && getRequired(collectionIndex.songsById, this.songId).versions.some(x => x.externalId !== null) && (
                        <span className="badge border-start border-width-3 text-body rounded-start-0 border-purple my-link me-2 bg-purple bg-opacity-20 mb-1 mt-1" onClick={on(() => this.collectiveAdd())}>+ мой вариант</span>
                    )}
                    <div className="row">
                        <div className="col-md-12">
                            <div className="likes-links row mt-3">
                                <div className="col-auto ">
                                    <LikesBlock
                                        currentLevel={this.getCurrentLevel()}
                                        update={this.eventCallback((level: number | null) => this.onLikesUpdate(level, null))}
                                        displayDislike={this.songId !== null}
                                        level0Available={true}></LikesBlock>
                                </div>
                                {this._state === State.Success && (
                                    <>
                                        <button className={`btn ${this._circleOfFifths ? 'btn-secondary' : 'btn-outline-secondary'} col-auto mb-3 me-2`} type="button" onClick={on(() => this.invertCircleOfFifths())}>
                                            <span>
                                                {'\n                            ккк\n                        '}
                                            </span>
                                        </button>
                                        <button className={`btn ${this._loops ? 'btn-secondary' : 'btn-outline-secondary'} col-auto mb-3 me-2`} type="button" onClick={on(() => this.invertLoops())}>
                                            <span>
                                                {'\n                            прг\n                        '}
                                            </span>
                                        </button>
                                        {!isNullOrWhiteSpace(this.externalId) && (
                                            <button className={`btn ${this._searchMode ? 'btn-secondary' : 'btn-outline-secondary'} col-auto mb-3 me-2`} type="button" onClick={on(() => this.invertSearch())}>
                                                <span className="ph-line-segments">
                                                </span>
                                            </button>
                                        )}
                                        {this._circleOfFifths && this._specialTonicCandidate !== null && (
                                            <button className={`btn ${this._specialTonic ? 'btn-secondary' : 'btn-outline-secondary'} col-auto mb-3`} type="button" onClick={on(() => this.invertSpecialTonic())}>
                                                <span>
                                                    {'T = ' + this._specialTonicCandidate.note.representation(new RepresentationSettings()) + (this._specialTonicCandidate.chordType === ChordType.Minor ? 'm' : '')}
                                                </span>
                                            </button>
                                        )}
                                    </>
                                )}
                            </div>
                        </div>
                    </div>
                    {this._state === State.Loading && (
                        <div className="row">
                            <div className="col">
                                <div className="small-loading"><img className="music-loading" src="img/music-loading.gif" title="Загрузка..." alt="Загрузка..."/></div>
                            </div>
                        </div>
                    )}
                </div>
                {this._searchMode && (
                    <div className="search-note badge bg-light border-width-3 border-start border-yellow text-body rounded-start-0 mb-3">
                        <span className="ph-line-segments"></span>
                        {this._progression === null ? (
                            this._needProgressionsCache ? (
                                <span>Надо сначала построить прогрессии. Для этого <a href="/cache">сходите в кэш</a>, пожалуйста.</span>
                            ) : (
                                <span>Инициализация поиска прогрессий (прогресс в шапке)...</span>
                            )
                        ) : (
                            <>
                                <span>Чтобы воспользоваться поиском аккордовой последовательности, просто нажмите на аккорд начала, а потом на аккорд конца прогрессии. И произойдёт поиск. Если это зацикленная последовательность, лучше выберите один цикл, где аккорд начала такой же, как аккорд конца. Тогда результаты поиска будут лучше.</span>
                                {this.progressionsSearchContext.results !== null && (
                                    <div className="mt-1">
                                        {'\n                        Найдено '}
                                        {this.progressionsSearchContext.results.result.foundProgressionsWithCoverage.size}
                                        {' песен.\n                        '}
                                        <span className="badge border-start border-width-3 text-body rounded-start-0 border-success my-link ms-1 bg-success bg-opacity-20 mb-1 mt-1" onClick={on(() => this.stateChanged({ isOpen: false, isOpen2: false }))}>Смотреть</span>
                                        {'\n                        '}
                                        <span className="badge border-start border-width-3 text-body rounded-start-0 border-success my-link ms-1 bg-danger bg-opacity-10 mb-1 mt-1" onClick={on(() => this.searchClear())}>Очистить</span>
                                    </div>
                                )}
                            </>
                        )}
                    </div>
                )}
                {this._state === State.Success && this._circleOfFifths && (
                    <CircleOfFifths chords={chords!.output.asChords(rs)} playOnClick={true} played={(note, chordType) => this.circleOfFifthsPlayed(note, chordType)}></CircleOfFifths>
                )}
                {this._loopsMemory !== null && (
                    <div>
                        {orderByKey(withIndices(this._loopsMemory.loops), x => deserializeLoop(x.x.normalized).length, compareNumbers).map(({ x: loop, i: id }, i) => (
                            <div key={i} ref={stringAttributes({ style: this._loopId === id ? 'color: darkgreen' : '' })} onClick={on(() => this.loopIdClick(id))} className="my-link">
                                <span>{this.progressionsVisualizer.getLoopStatisticsTitle(loop)}</span>
                                {'\n                    '}
                                <b><span>{this.progressionsVisualizer.getLoopChordsTitle(loop)}</span></b>
                            </div>
                        ))}
                    </div>
                )}
                {this._state === State.Success && (
                    <div className="chords-block mt-4" ref={stringAttributes({ style: `font-size: ${this._zoom}%` })} dangerouslySetInnerHTML={{ __html: chords!.output.asHtml(rs) }}>
                    </div>
                )}
                {this.buildState(collectionIndex, on)}
            </>
        );
    }

    private buildMyVersion(collectionIndex: CollectionIndex, myId: number, on: (handler: () => void | Promise<void>) => () => void): ReactNode {
        if (this.songId === null || this.versionId === null) return null;
        const x = collectionIndex.versionsById.get(this.versionId);
        if (x === undefined || x.version.userId !== myId) return null;

        const version = x.version;
        const tag = `${version.collectiveSearchTag === null ? '' : format000(intDiv(version.collectiveSearchTag, 1000))} ${version.collectiveSearchTag === null ? '' : format000(version.collectiveSearchTag % 1000)}`;
        if (version.collectiveId !== null) {
            const collectiveId = version.collectiveId;
            let title: string;
            switch (version.collectiveType) {
                case VersionCollectiveType.Private: title = `Приватные ${tag}`; break;
                case VersionCollectiveType.Public: title = 'Публичные'; break;
                default: throw new ArgumentOutOfRangeException();
            }

            return (
                <div className="dropdown me-2">
                    <a href="#" className="my-link badge bg-purple bg-opacity-75 border-start border-width-3 rounded-start-0 border-purple d-inline-flex align-items-center dropdown-toggle mb-1 mt-1" data-bs-toggle="dropdown" aria-expanded="false">
                        {title}
                    </a>
                    {'\n\n                '}
                    <div className="dropdown-menu" ref={stringAttributes({ style: '' })}>
                        <a onClick={on(() => this.collectiveUpdate(collectiveId))} className="dropdown-item my-link">
                            <span className="ph-pen me-2"></span>
                            {'\n                        Редактировать…\n                    '}
                        </a>
                        {version.collectiveType === VersionCollectiveType.Private && (
                            <>
                                <a ref={stringAttributes({ onclick: `alert('Эти аккорды не видны в библиотеках других людей, но если вы скажете кому-то эти шесть цифр и они введут их в поиске, то они увидят ваши аккорды. Еще они доступны в интернете по ссылке, ею можно поделиться (${this.options.localSourceNameSafe}). Собственно, чтобы отобрать доступ, если вы кому-то дали циферки или ссылку - обновите циферки.')` })} className="dropdown-item my-link">
                                    <span className="ph-question me-2"></span>
                                    {'\n                            Что такое '}
                                    {tag}
                                    {'?\n                        '}
                                </a>
                                {'\n                        '}
                                <a onClick={on(() => this.collectiveRefreshTag(collectiveId))} className="dropdown-item my-link">
                                    <span className="ph-arrows-counter-clockwise me-2"></span>
                                    {'\n                            Обновить номер '}
                                    {tag}
                                    {'…\n                        '}
                                </a>
                            </>
                        )}
                        <a onClick={on(() => this.collectiveDelete(collectiveId))} className="dropdown-item my-link">
                            <span className="ph-x me-2"></span>
                            {'\n                        Удалить…\n                    '}
                        </a>
                    </div>
                </div>
            );
        }

        return (
            <span className="badge border-start border-width-3 text-body rounded-start-0 border-pink my-link me-2 bg-pink bg-opacity-20 mb-1 mt-1" onClick={on(() => this.adminRemoveVersion())}>Удалить аккорды</span>
        );
    }

    private buildState(collectionIndex: CollectionIndex, on: (handler: () => void | Promise<void>) => () => void): ReactNode {
        switch (this._state) {
            case State.Failed: {
                const version = this.versionId !== null ? collectionIndex.versionsById.get(this.versionId) : undefined;
                return (
                    <>
                        <span>
                            {'\n                Не получилось загрузить, может нету интернета.\n                '}
                            <span className="my-text-gray">
                                {'\n                    Или редкая проблема конкретно с этими аккордами, напишите мне.\n                '}
                            </span>
                            {'\n                Попробуйте нажать:\n                '}
                            <button className="btn btn-success mt-2 mb-2 ms-1" onClick={on(() => this.refreshChords())}>Повтор</button>
                        </span>
                        {version !== undefined && (
                            <div>
                                <span>
                                    {'\n                        Или же источник:\n                        '}
                                    <span></span>
                                    {'\n                        '}
                                    <a href={version.version.uri.toString()} target="_blank">{version.version.uri.host}</a>
                                </span>
                            </div>
                        )}
                    </>
                );
            }
            case State.KnownUnsupported: {
                const versionUri = getRequired(collectionIndex.versionsById, value(this.versionId)).version.uri.toString();
                return (
                    <span>
                        {'\n                Этот источник не поддерживается. Смотрите напрямую: '}
                        <a href={versionUri} target="_blank">{versionUri}</a>
                    </span>
                );
            }
            case State.HideUnstable:
                return (
                    <span>
                        {this._chords!.unstableErrorMessage}
                        {' '}
                        <a href={this._chords!.sourceUri.toString()} target="_blank">{this._chords!.source}</a>
                    </span>
                );
            default:
                return null;
        }
    }
}

const simplificationModes: readonly { mode: SimplificationMode; title: string }[] = [
    { mode: SimplificationMode.Remove6, title: '6' },
    { mode: SimplificationMode.Remove7, title: '7' },
    { mode: SimplificationMode.Remove9AndMore, title: '9…' },
    { mode: SimplificationMode.RemoveBass, title: '/' },
    { mode: SimplificationMode.RemoveSus, title: 'sus' },
];

/** NoteAlteration?'s hash: null as Flat (0). */
function alterationHash(alteration: NoteAlteration | null): NoteAlteration {
    return alteration ?? NoteAlteration.Flat;
}

/** The records' GetHashCode compared: their members' hashes. */
function sameRenderKey(a: unknown[], b: unknown[]): boolean {
    for (let i = 0; i < a.length; i++) {
        const x = a[i];
        const y = b[i];
        if (x instanceof RepresentationSettings && y instanceof RepresentationSettings) {
            if (!(x.transpose === y.transpose
                && alterationHash(x.alteration) === alterationHash(y.alteration)
                && x.testOnlyX === y.testOnlyX
                && (x.relativeTo === y.relativeTo
                    || (x.relativeTo !== null && y.relativeTo !== null && x.relativeTo.note === y.relativeTo.note && x.relativeTo.major === y.relativeTo.major))
                && x.isVariableWidth === y.isVariableWidth
                && x.simplification === y.simplification
                && x.customAttributes === y.customAttributes)) return false;
        } else if (x instanceof Note && y instanceof Note) {
            if (!(x.value === y.value && alterationHash(x.alteration) === alterationHash(y.alteration))) return false;
        } else if (x !== y) {
            return false;
        }
    }

    return true;
}

export function ChordsView(props: ChordsViewProps) {
    const forceRender = useStateHasChanged();
    const instanceRef = useRef<ChordsViewInstance | null>(null);
    if (instanceRef.current === null) {
        instanceRef.current = new ChordsViewInstance(forceRender);
        // Blazor sets the parameters before the first render.
        instanceRef.current.assignParameters(props);
    }

    const instance = instanceRef.current;

    // CollectionComponentBase: CollectionChanged → OnNewCollectionIndexReceived, then StateHasChanged.
    useCollectionComponent(async () => {
        await instance.onNewCollectionIndexReceived();
        instance.stateHasChanged();
    });

    // ProgressionsSearchContext.SearchResultsChanged += ProgressionsSearchContextOnSearchResultsChanged
    useEvent(services.progressionsSearchContext.searchResultsChanged, searchSource => instance.progressionsSearchContextOnSearchResultsChanged(searchSource));

    // SetParametersAsync, each time the owner renders this component.
    useLayoutEffect(() => {
        instance.setParameters(props);
    }, [instance, props]);

    useLayoutEffect(() => () => instance.dispose(), [instance]);

    // OnAfterRenderAsync, after each render that made a new tree.
    useLayoutEffect(() => {
        instance.afterCommit();
    });

    return <>{instance.render()}</>;
}
