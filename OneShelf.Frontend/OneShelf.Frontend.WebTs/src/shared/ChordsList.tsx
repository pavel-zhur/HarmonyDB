// The original: Shared/ChordsList.razor.
//
// As there:
// - OnParametersSetAsync runs each time the owner renders it (StateChanged is an Action, which always counts as
//   changed) — here each time the props object is a new one — and when a new library arrives (then it renders again);
// - SearchContext.Arrived updates the list of a virtual song (a song found on the internet);
// - event handlers render after their synchronous part and again after they complete; EventCallback parameters given
//   to LikesBlock and CategoriesButton run as this component's events.
// The illustration in the background is picked at random at every render, as there.
import { Fragment, useLayoutEffect, useRef, type ReactNode } from 'react';
import { getRequired } from '../dotnet/collections';
import { stringAttributes } from '../framework/attributes';
import { blazorEventHandler } from '../framework/blazorEventHandler';
import { navigationManager } from '../framework/navigation';
import { errorHandledTask } from '../framework/unhandledExceptions';
import { useCollectionComponent } from '../hooks/useCollectionComponent';
import { useAsyncEvent } from '../hooks/useEvent';
import { useStateHasChanged } from '../hooks/useStateHasChanged';
import type { SearchHeader } from '../lib/api/HarmonyDB.Source.Api.Model/V1/SearchHeader';
import type { Like } from '../lib/api/OneShelf.Frontend.Api.Model/V3/Databasish/Like';
import type { Version } from '../lib/api/OneShelf.Frontend.Api.Model/V3/Databasish/Version';
import type { AllIllustrations } from '../lib/api/OneShelf.Frontend.Api.Model/V3/Illustrations/AllIllustrations';
import { toFrontendAttributesV1 } from '../lib/api/OneShelf.Frontend.SpecificModel/ConversionExtensions';
import { InvalidOperationException } from '../lib/common/dotnet/exceptions';
import { compareNumbers, orderBy, single } from '../lib/common/dotnet/Linq';
import { Uri } from '../lib/common/dotnet/Uri';
import { transposition } from '../lib/common/HarmonyDB.Common/Transposition/TranspositionExtensions';
import { withIndices } from '../lib/common/OneShelf.Common/Extensions';
import type { CollectionIndex } from '../models/CollectionIndex';
import { toLetterIndex } from '../models/IndexingExtensions';
import { services } from '../services/registry';
import type { SearchArrival } from '../services/SearchContext';
import { CategoriesButton } from './CategoriesButton';
import { ChordsView } from './ChordsView';
import { IllustrationsView } from './IllustrationsView';
import { LikesBlock } from './LikesBlock';
import './ChordsList.scoped.scss';

export interface ChordsListProps {
    title: string | null;
    songId: number | null;
    virtualSongArtist: string | null;
    virtualSongTitle: string | null;
    searchInternet: () => void | Promise<void>;
    externalId: string | null;
    versionId: number | null;
    isOpen2: boolean;
    stateChanged: (value: { isOpen: boolean; isOpen2: boolean }) => void;
}

/** (Version? version, SearchHeader? header, bool isSelected, string title) */
interface Item {
    readonly version: Version | null;
    readonly header: SearchHeader | null;
    readonly isSelected: boolean;
    readonly title: string;
}

/** (int i, Version? version, SearchHeader? header, string title)? */
interface SelectedItem {
    readonly i: number;
    readonly version: Version | null;
    readonly header: SearchHeader | null;
    readonly title: string;
}

/** Math.Round(value, digits) of a double: scaled, rounded to even, scaled back. */
function round(value: number, digits: number): number {
    const power10 = 10 ** digits;
    const scaled = value * power10;
    let rounded = Math.round(scaled);
    if (Math.abs(scaled % 1) === 0.5) rounded = 2 * Math.round(scaled / 2);
    return rounded / power10;
}

export function ChordsList(props: ChordsListProps) {
    const { title, songId, virtualSongArtist, externalId, versionId, isOpen2, stateChanged } = props;
    const identityProvider = services.identityProvider;
    const searchContext = services.searchContext;
    const collectionNavigation = services.collectionNavigation;
    const instantActions = services.instantActions;
    const illustrationsProvider = services.illustrationsProvider;
    const stateHasChanged = useStateHasChanged();

    const self = useRef({
        props,
        failed: false,
        items: null as Item[] | null,
        selectedItem: null as SelectedItem | null,
        illustrations: null as AllIllustrations | null,
    }).current;
    self.props = props;

    const peekIndex = (): CollectionIndex | null => services.collectionIndexProvider.peek().collectionIndex;

    const isSelected = (version: Version | null, header: SearchHeader | null): boolean => {
        const p = self.props;
        return p.externalId === (version?.externalId ?? header?.externalId ?? null) && p.externalId !== null
            || p.versionId === (version?.id ?? null) && p.versionId !== null;
    };

    const setSelectedItem = () => {
        let found: SelectedItem | null = null;
        let count = 0;
        for (const { x, i } of withIndices(self.items!)) {
            if (!x.isSelected) continue;
            if (++count > 1) throw new InvalidOperationException('Sequence contains more than one matching element');
            found = { i, version: x.version, header: x.header, title: x.title };
        }

        self.selectedItem = found;
    };

    /** ResultArrived without its StateHasChanged calls (it renders in each of its branches). */
    const resultArrivedCore = (results: SearchArrival | null) => {
        const p = self.props;
        if (p.virtualSongArtist === null) {
            return false;
        }

        if (results === null) {
            self.failed = false;
            self.items = null;
            return true;
        }

        if (results.failed) {
            self.failed = true;
            return true;
        }

        self.failed = false;

        self.items = results.results!
            .filter(x => x.artist === p.virtualSongArtist && x.title === p.virtualSongTitle)
            .map(x => ({ version: null, header: x.header, isSelected: isSelected(null, x.header), title: x.header.source }));

        setSelectedItem();

        return true;
    };

    const resultArrived = async (results: SearchArrival | null) => {
        if (resultArrivedCore(results)) stateHasChanged();
    };

    /**
     * OnParametersSetAsync up to its only real await (IllustrationsProvider.Get(), when nothing was peeked): true when
     * that await is due — the task then completes later; otherwise it has completed synchronously.
     */
    const onParametersSetSync = (): boolean => {
        const p = self.props;
        self.illustrations = illustrationsProvider.peek();
        const collectionIndex = peekIndex();
        if (collectionIndex !== null) {
            if (p.virtualSongArtist !== null) {
                self.items = null;
                resultArrivedCore(searchContext.peek);
            } else if (p.songId !== null) {
                self.items = orderBy(getRequired(collectionIndex.songsById, p.songId).versions, (a, b) => a.createdOn.compareTo(b.createdOn))
                    .map(x => ({ version: x, header: null, isSelected: isSelected(x, null), title: x.source ?? x.uri.host }));

                setSelectedItem();

                return self.illustrations === null;
            } else {
                self.items = null;
            }
        }

        return false;
    };

    const loadIllustrations = async () => {
        self.illustrations = await illustrationsProvider.get();
    };

    // OnNewCollectionIndexReceived: await OnParametersSetAsync(); StateHasChanged();
    const { collectionIndex, collectionIndexFailed, retryCollectionIndex } = useCollectionComponent(async () => {
        if (onParametersSetSync()) await loadIllustrations();
        stateHasChanged();
    });

    // SearchContext.Arrived += ResultArrived (OnInitializedAsync); -= (Dispose)
    useAsyncEvent(searchContext.arrived, resultArrived);

    // SetParametersAsync, each time the owner renders this component (a new props object): the synchronous part of
    // OnParametersSetAsync runs before this render, as Blazor runs it before rendering; when its task goes on
    // (loading the illustrations), the component renders again when it completes.
    const parametersSet = useRef<{ props: ChordsListProps | null; awaiting: boolean }>({ props: null, awaiting: false }).current;
    if (parametersSet.props !== props) {
        parametersSet.props = props;
        parametersSet.awaiting = onParametersSetSync();
    }

    useLayoutEffect(() => {
        if (!parametersSet.awaiting) return;
        parametersSet.awaiting = false;
        void errorHandledTask(loadIllustrations().then(() => stateHasChanged()));
    }, [props]);

    const eventCallback = <TArgs extends unknown[]>(handler: (...args: TArgs) => void | Promise<unknown>) => (...args: TArgs): Promise<void> => {
        const result = handler(...args);
        stateHasChanged();
        return result instanceof Promise ? result.then(() => stateHasChanged()) : Promise.resolve();
    };

    const onLikesUpdate = async (level: number | null) => {
        await instantActions.updateLike(self.props.songId!, null, level, null, null);
    };

    const onLikesCategoryUpdate = async (value: { level: number | null; likeCategoryId: number }) => {
        await instantActions.updateLike(self.props.songId!, null, value.level, null, value.likeCategoryId);
    };

    const getChordsListLink = (version: Version | null, header: SearchHeader | null) =>
        collectionNavigation.chordsListLink(version?.externalId ?? header?.externalId ?? null, version?.id ?? null);

    const getIllustrationsLink = () => collectionNavigation.illustrationsLink();

    const getChordsViewTitle = (): string => {
        const selectedItem = self.selectedItem!;
        let result = toLetterIndex(single(withIndices(self.items!), x => x.x.isSelected).i);
        result += '. ';
        result += selectedItem.title;
        if (selectedItem.header !== null && toFrontendAttributesV1(selectedItem.header.specificAttributes).badgeText !== null) {
            result += `(${toFrontendAttributesV1(selectedItem.header.specificAttributes).badgeText})`;
        }

        if (selectedItem.header?.rating != null) {
            result += ' ☆ ';
            result += round(selectedItem.header.rating, 2);
        }

        if (selectedItem.header?.type != null) {
            result += ` ${selectedItem.header.type}`;
        }

        return result;
    };

    const searchArtist = (artistId: number | null) => {
        const index = peekIndex();
        if (index !== null || artistId === null) {
            navigationManager.navigateTo(collectionNavigation.searchResultsLocalArtist(artistId, null, artistId !== null ? getRequired(index!.artistsById, artistId).name : self.props.virtualSongArtist!));
        }
    };

    const findSongId = (): number | null => {
        const p = self.props;
        if (p.songId !== null) return p.songId;
        if (p.externalId === null) return null;
        const index = peekIndex();
        if (index === null) return null;
        return index.versionsByExternalId.get(p.externalId).map(x => x.song)[0]?.id ?? null;
    };

    const areIllustrationsOpen = (): boolean => {
        const p = self.props;
        return p.isOpen2 && p.songId !== null && (p.externalId === null || p.externalId.trim() === '' && /^\s*$/.test(p.externalId)) && p.versionId === null
            && self.illustrations?.songs.has(p.songId) === true;
    };

    const go = (condition: boolean, url: string) => {
        if (!condition) return;
        navigationManager.navigateTo(url);
    };

    const me = () => identityProvider.requiredIdentity.id;

    const renderLikes = (index: CollectionIndex, version: Version | null, header: SearchHeader | null, selected: boolean) => {
        const source: readonly Like[] = songId !== null
            ? getRequired(index.songsById, songId).likes
            : index.versionsByUri.get(version?.uri ?? header!.sourceUri).flatMap(x => x.song.likes);
        const ordered = orderBy(
            source
                .filter(x => x.versionId !== null)
                .filter(x => version !== null
                    ? version.id === x.versionId
                    : Uri.equals(getRequired(index.versionsById, x.versionId!).version.uri, header!.sourceUri)),
            (a, b) => a.createdOn.compareTo(b.createdOn));
        const withVirtual: { like: Like | null; virtualLike: { level: number; likeCategoryId: number | null } | null }[] = ordered.map(x => ({ like: x, virtualLike: null }));
        const virtualLevel = header !== null ? index.virtualLikeLevels.get(header.externalId) : undefined;
        if (header !== null && virtualLevel !== undefined) {
            withVirtual.push({ like: null, virtualLike: virtualLevel });
        }

        return withVirtual
            .map(x => ({
                level: x.like?.level ?? x.virtualLike!.level,
                transpose: x.like?.transpose ?? null,
                userId: x.like?.userId ?? me(),
                likeCategoryId: x.like?.likeCategoryId ?? x.virtualLike?.likeCategoryId ?? null,
            }))
            .map((like, k) => like.likeCategoryId !== null ? (
                <span key={k} onClick={blazorEventHandler(stateHasChanged, () => go(selected && isOpen2, collectionNavigation.searchResultsLocalLikeCategoryId(like.likeCategoryId, true)))} className={`badge ${selected ? 'bg-light bg-opacity-75' : 'bg-indigo bg-opacity-10'} border-start border-width-3 text-body rounded-start-0 border-indigo me-2`}>
                    {'\n                                            '}{getRequired(index.likeCategoriesById, like.likeCategoryId).name}
                    {like.userId !== me() && (
                        <span> – {getRequired(index.usersById, like.userId).title}</span>
                    )}
                    {like.transpose !== null && like.transpose !== 0 && (
                        <span> ({transposition(like.transpose)})</span>
                    )}
                </span>
            ) : (
                <span key={k} className="pe-2">
                    <span className={`fas fa-heart heart-${like.level}`}></span>
                    {'\n                                            '}
                    <span onClick={blazorEventHandler(stateHasChanged, () => go(selected && isOpen2, collectionNavigation.searchResultsLocalShortlistedUser(like.userId, true)))}>{getRequired(index.usersById, like.userId).title}</span>
                    {like.transpose !== null && like.transpose !== 0 && (
                        <span> ({transposition(like.transpose)})</span>
                    )}
                </span>
            ));
    };

    const renderBody = (index: CollectionIndex): ReactNode => {
        const items = self.items;
        if (self.failed) {
            return (
                <>
                    <span>Loading failed.</span>
                    <button className="btn btn-primary" onClick={blazorEventHandler(stateHasChanged, () => searchContext.restartFailed())}>Refresh</button>
                </>
            );
        }

        if (items === null) {
            return <span>No song is selected.</span>;
        }

        const songIllustrations = songId !== null ? self.illustrations?.songs.get(songId) : undefined;
        const background = songIllustrations !== undefined
            ? orderBy(songIllustrations.illustrations.map(x => ({ x, k: Math.random() })), (a, b) => compareNumbers(a.k, b.k))[0]!.x.publicUrls.url256
            : null;
        const song = songId !== null ? getRequired(index.songsById, songId) : null;

        return (
            <>
                <div className="my-pre-background">
                    {background !== null && (
                        <div className="my-background" ref={stringAttributes({ style: `--background: url('${background}')` })}></div>
                    )}

                    <div className="my-post-background">
                        <ul className="list-group chordslist list-group-flush mb-3">
                            {withIndices(items).map(({ x: { version, header, isSelected: selected, title: itemTitle }, i }) => (
                                <li key={i} className={`list-group-item ${selected ? 'bg-info bg-opacity-75' : 'my-link'}`} onClick={blazorEventHandler(stateHasChanged, () => go(!selected || !isOpen2, getChordsListLink(version, header)))}>
                                    <div>
                                        <span>{toLetterIndex(i)}. </span>
                                        {'\n                                '}
                                        <span className="source active-content me-2">{itemTitle}</span>
                                        {header !== null && index.existingExternalIdsWithVirtual.has(header.externalId) && (
                                            <span className="badge bg-pink me-2">добавлены!</span>
                                        )}

                                        {header !== null && toFrontendAttributesV1(header.specificAttributes).badgeText !== null && (
                                            <span className="badge bg-success me-2">{toFrontendAttributesV1(header.specificAttributes).badgeText}</span>
                                        )}

                                        {header?.rating != null && (
                                            <span className="badge bg-light text-body me-2"> ☆ {round(header.rating, 2)}</span>
                                        )}

                                        {header?.type != null && (
                                            <span className={`badge ${header.isSupported ? 'bg-light text-body' : 'bg-dark bg-opacity-20 text-reset'} me-2`}>{header.type}</span>
                                        )}

                                        {renderLikes(index, version, header, selected)}

                                    </div>
                                </li>
                            ))}

                            {songIllustrations !== undefined && (
                                <li className={`list-group-item ${areIllustrationsOpen() ? 'bg-info bg-opacity-75' : ''} my-link`} onClick={blazorEventHandler(stateHasChanged, () => navigationManager.navigateTo(getIllustrationsLink()))}>
                                    <div>
                                        <span className="source active-content me-2">Иллюстрации ({songIllustrations.illustrations.length})</span>
                                    </div>
                                </li>
                            )}
                        </ul>
                        {song !== null && (
                            <>
                                <div className="chordslist-likes-section mb-3">
                                    <div className="row">
                                        <div className="col-md-12">
                                            <div className="likes mb-3">
                                                {orderBy(song.likes.filter(x => x.versionId === null), (a, b) => a.createdOn.compareTo(b.createdOn)).map((like, k) => like.likeCategoryId !== null ? (
                                                    <a key={k} href={collectionNavigation.searchResultsLocalLikeCategoryId(like.likeCategoryId, true)} className="badge bg-indigo bg-opacity-10 border-start border-width-3 text-body rounded-start-0 border-indigo me-2">
                                                        {'\n                                                '}{getRequired(index.likeCategoriesById, like.likeCategoryId).name}
                                                        {like.userId !== me() && (
                                                            <span> – {getRequired(index.usersById, like.userId).title}</span>
                                                        )}
                                                    </a>
                                                ) : (
                                                    <span key={k} className="chordslike">
                                                        <span className={`fas fa-heart heart-${like.level}`}></span>
                                                        {'\n                                                '}
                                                        <a href={collectionNavigation.searchResultsLocalShortlistedUser(like.userId, true)}>{getRequired(index.usersById, like.userId).title}</a>
                                                    </span>
                                                ))}
                                            </div>
                                        </div>
                                    </div>
                                    {'\n                        '}
                                    <div className="row">
                                        <div className="col-md-12">
                                            <div className="likes-links row">
                                                <LikesBlock
                                                    currentLevel={singleOrDefaultLike(song.likes, x => x.likeCategoryId === null && x.userId === me() && x.versionId === null)?.level ?? null}
                                                    update={eventCallback(onLikesUpdate)}
                                                    level0Available={false}></LikesBlock>
                                            </div>
                                        </div>
                                    </div>
                                </div>

                                <CategoriesButton songId={song.id} update={eventCallback(onLikesCategoryUpdate)} />
                            </>
                        )}

                        {song !== null && song.artists.map((artistId, k) => {
                            const count = [...index.songsById.values()].filter(x => x.artists.includes(artistId)).length;
                            return (
                                <span key={k} className="badge border-start border-width-3 text-body rounded-start-0 border-teal my-link me-2 bg-teal bg-opacity-20 mb-1 mt-1" onClick={blazorEventHandler(stateHasChanged, () => searchArtist(artistId))}>
                                    {'\n                            Еще '}{getRequired(index.artistsById, artistId).name}
                                    {count > 1 && (
                                        <span> ({count}+)</span>
                                    )}
                                </span>
                            );
                        })}
                        {virtualSongArtist !== null && (
                            <span className="badge border-start border-width-3 text-body rounded-start-0 border-teal my-link me-2 bg-teal bg-opacity-20 mb-1 mt-1" onClick={blazorEventHandler(stateHasChanged, () => searchArtist(null))}>Еще {virtualSongArtist}</span>
                        )}

                        <span className="badge border-start border-width-3 text-body rounded-start-0 border-teal my-link me-1 bg-teal bg-opacity-20 mb-1 mt-1" onClick={() => void errorHandledTask(eventCallback(() => self.props.searchInternet())())}>Искать в интернете</span>
                    </div>
                </div>

                {isOpen2 && self.selectedItem !== null && (
                    <ChordsView externalId={externalId}
                                versionId={versionId}
                                songId={findSongId()}
                                stateChanged={stateChanged}
                                virtualSongArtist={virtualSongArtist}
                                virtualSongTitle={props.virtualSongTitle}
                                previousVisible={self.selectedItem.i > 0}
                                onPrevious={() => navigationManager.navigateTo(getChordsListLink(items[self.selectedItem!.i - 1]!.version, items[self.selectedItem!.i - 1]!.header))}
                                nextVisible={!items[items.length - 1]!.isSelected}
                                onNext={() => navigationManager.navigateTo(getChordsListLink(items[self.selectedItem!.i + 1]!.version, items[self.selectedItem!.i + 1]!.header))}
                                title={getChordsViewTitle()} />
                )}

                {areIllustrationsOpen() && (
                    <IllustrationsView
                        songId={findSongId()}
                        stateChanged={stateChanged} />
                )}
            </>
        );
    };

    return (
        <>
            <div className="page-header bg-indigo bg-opacity-75 my-header my-content-addition">

                <div className="page-header-content d-block">
                    <div className="d-flex">
                        <div className="breadcrumb">
                            <h5 className="my-pt-25">{title ?? 'Загрузка...'}</h5>
                        </div>

                        {'\n\n            '}
                        <a className="d-flex align-items-center text-body py-2 ms-auto collapsed my-link" onClick={blazorEventHandler(stateHasChanged, () => stateChanged({ isOpen: false, isOpen2: false }))}>
                            <i className="ph-x-circle my-white"></i>
                        </a>
                    </div>

                </div>
            </div>

            {collectionIndexFailed ? (
                <>
                    <p><em>Не получилось загрузить, может нету интернета, библиотеку.</em></p>
                    <button className="btn btn-secondary" onClick={blazorEventHandler(stateHasChanged, retryCollectionIndex)}>Повтор</button>
                </>
            ) : collectionIndex === null ? (
                <div className="small-loading"><img className="music-loading" src="img/music-loading.gif" title="Загрузка..." alt="Загрузка..." /></div>
            ) : (
                <Fragment>{renderBody(collectionIndex)}</Fragment>
            )}
        </>
    );
}

/** SingleOrDefault(predicate) of likes. */
function singleOrDefaultLike(likes: readonly Like[], predicate: (x: Like) => boolean): Like | null {
    let found: Like | null = null;
    let count = 0;
    for (const like of likes) {
        if (!predicate(like)) continue;
        if (++count > 1) throw new InvalidOperationException('Sequence contains more than one matching element');
        found = like;
    }

    return found;
}
