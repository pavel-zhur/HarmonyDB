// The original: Shared/SearchResultsLocal.razor.
//
// The library's list, filtered and ordered by the parameters. As there:
// - OnParametersSetAsync (when a parameter changes): SearchContext.Update(Query), the illustrations peeked (and loaded
//   when a mode needs them), Update();
// - Update() recomputes the list only when its hash (the parameters and states it depends on) changed, then renders;
// - ShouldRender: CssNthOfType is told the SongId, and the component renders only when the hash it rendered differs
//   from the last computed one — here the previous render's element is returned as it is, so React leaves the whole
//   subtree alone, as Blazor does;
// - the progressions search's results (ProgressionsSearchContext.SearchResultsChanged) and a new library
//   (CollectionChanged) run Update().
// The hash is a key of the same values (object identities where C# hashes references), not C#'s int.
// Handlers read the latest parameters, as the C# lambdas read the component's properties when they run.
import { Fragment, useLayoutEffect, useRef, type ReactElement, type ReactNode } from 'react';
import { toDictionary } from '../dotnet/collections';
import { blazorValue, booleanAttribute, stringAttributes } from '../framework/attributes';
import { blazorEventHandler } from '../framework/blazorEventHandler';
import { navigationManager } from '../framework/navigation';
import { asyncVoid } from '../framework/unhandledExceptions';
import { Virtualize } from '../framework/Virtualize';
import { useCollectionComponent } from '../hooks/useCollectionComponent';
import { useEvent } from '../hooks/useEvent';
import { useStateHasChanged } from '../hooks/useStateHasChanged';
import type { ISong } from '../lib/api/OneShelf.Frontend.Api.Model/V3/Databasish/Interfaces/ISong';
import type { Like } from '../lib/api/OneShelf.Frontend.Api.Model/V3/Databasish/Like';
import type { User } from '../lib/api/OneShelf.Frontend.Api.Model/V3/Databasish/User';
import type { AllIllustrations } from '../lib/api/OneShelf.Frontend.Api.Model/V3/Illustrations/AllIllustrations';
import type { DateTime } from '../lib/common/dotnet/DateTime';
import { enumParse } from '../lib/common/dotnet/Enum';
import { objectId } from '../lib/common/dotnet/Equality';
import { ArgumentOutOfRangeException, InvalidOperationException } from '../lib/common/dotnet/exceptions';
import { compareNumbers, compareStrings, distinct, groupBy, maxInt } from '../lib/common/dotnet/Linq';
import { isNullOrWhiteSpace, toLowerInvariant, tryParseInt32 } from '../lib/common/dotnet/text';
import { withIndices } from '../lib/common/OneShelf.Common/Extensions';
import { getRating } from '../lib/common/OneShelf.Common.Songs/SongsConstants';
import { getHumanTitle, type HumanTitles } from '../models/HumanTitleAttribute';
import type { ProgressionsSearchSource } from '../models/ProgressionsSearchSource';
import { services } from '../services/registry';
import { CssNthOfType, type CssNthOfTypeRef } from './CssNthOfType';
import { SearchHighlighting } from './SearchHighlighting';
import './SearchResultsLocal.scoped.scss';

/** SearchResultsLocal.SongsMode */
export enum SearchResultsLocalSongsMode {
    WithIllustrations = 0,
    WithoutIllustrations = 1,
    WithMyCollectives = 2,
    Artist = 3,
    ArtistCompact = 4,
    Title = 5,
    Index = 6,
    IndexWithChords = 7,
    IndexDesc = 8,
    VersionCreationDesc = 9,
    RatingAll = 10,
    MyRating = 11,
    NewestLike = 12,
}

/** The [HumanTitle] of SearchResultsLocal.SongsMode's members. */
export const SearchResultsLocalSongsModeHumanTitles: HumanTitles = {
    [SearchResultsLocalSongsMode.WithIllustrations]: 'С иллюстрациями',
    [SearchResultsLocalSongsMode.WithoutIllustrations]: 'Без иллюстраций',
    [SearchResultsLocalSongsMode.WithMyCollectives]: 'С моими версиями',
    [SearchResultsLocalSongsMode.Artist]: 'По исполнителю',
    [SearchResultsLocalSongsMode.ArtistCompact]: 'По исполнителю, компактно',
    [SearchResultsLocalSongsMode.Title]: 'По названию',
    [SearchResultsLocalSongsMode.Index]: 'По порядку',
    [SearchResultsLocalSongsMode.IndexWithChords]: 'По порядку, с аккордами',
    [SearchResultsLocalSongsMode.IndexDesc]: 'Новые',
    [SearchResultsLocalSongsMode.VersionCreationDesc]: 'Новые аккорды',
    [SearchResultsLocalSongsMode.RatingAll]: 'По рейтингу',
    [SearchResultsLocalSongsMode.MyRating]: 'Личный шортлист - по рейтингу',
    [SearchResultsLocalSongsMode.NewestLike]: 'Шортлист - недавние',
};

const SongsMode = SearchResultsLocalSongsMode;
type SongsMode = SearchResultsLocalSongsMode;

/** Enum.GetValues<SongsMode>() */
const allSongsModes: SongsMode[] = [
    SongsMode.WithIllustrations, SongsMode.WithoutIllustrations, SongsMode.WithMyCollectives, SongsMode.Artist,
    SongsMode.ArtistCompact, SongsMode.Title, SongsMode.Index, SongsMode.IndexWithChords, SongsMode.IndexDesc,
    SongsMode.VersionCreationDesc, SongsMode.RatingAll, SongsMode.MyRating, SongsMode.NewestLike,
];

export interface SearchResultsLocalProps {
    mode: SongsMode;
    likeCategoryId: number | null;
    query: string | null;
    songId: number | null;
    artistId: number | null;
    shortlistedUserId: number | null;
}

/** (string? artistName, int? artistId, ISong song) */
interface Item {
    readonly artistName: string | null;
    readonly artistId: number | null;
    readonly song: ISong;
}

interface Group {
    readonly key: { artistName: string | null; artistId: number | null };
    readonly items: Item[];
}

/** Comparer<T?>.Default for a nullable key: null first. */
function compareNullable<T>(a: T | null, b: T | null, compare: (a: T, b: T) => number): number {
    if (a === null) return b === null ? 0 : -1;
    if (b === null) return 1;
    return compare(a, b);
}

const compareDates = (a: DateTime, b: DateTime) => a.compareTo(b);

/** Max() of DateTime: throws on an empty sequence. */
function maxDate(values: DateTime[]): DateTime {
    if (values.length === 0) throw new InvalidOperationException('Sequence contains no elements');
    return values.reduce((a, b) => b.compareTo(a) > 0 ? b : a);
}

/** Min() of DateTime: throws on an empty sequence. */
function minDate(values: DateTime[]): DateTime {
    if (values.length === 0) throw new InvalidOperationException('Sequence contains no elements');
    return values.reduce((a, b) => b.compareTo(a) < 0 ? b : a);
}

/** Max(x => (DateTime?)x): null on an empty sequence. */
function maxNullableDate(values: DateTime[]): DateTime | null {
    return values.length === 0 ? null : maxDate(values);
}

/** OrderBy / OrderByDescending with ThenBy keys: a stable sort. */
function orderBy<T>(source: readonly T[], ...comparisons: ((a: T, b: T) => number)[]): T[] {
    const keyed = [...source];
    keyed.sort((a, b) => {
        for (const comparison of comparisons) {
            const result = comparison(a, b);
            if (result !== 0) return result;
        }

        return 0;
    });
    return keyed;
}

/** OrderBy with keys computed once per element, as LINQ does. */
function orderByKeys<T, K>(source: readonly T[], keys: (x: T) => K, compare: (a: K, b: K) => number): T[] {
    return orderBy(source.map(x => ({ x, k: keys(x) })), (a, b) => compare(a.k, b.k)).map(x => x.x);
}

const descending = <K,>(compare: (a: K, b: K) => number) => (a: K, b: K) => compare(b, a);

export function SearchResultsLocal(props: SearchResultsLocalProps) {
    const collectionNavigation = services.collectionNavigation;
    const searchContext = services.searchContext;
    const identityProvider = services.identityProvider;
    const illustrationsProvider = services.illustrationsProvider;
    const progressionsSearchContext = services.progressionsSearchContext;
    const progressionsCacheLoader = services.progressionsCacheLoader;
    const stateHasChanged = useStateHasChanged();

    const self = useRef({
        props,
        songs: null as Item[] | null,
        usersWithLikes: null as User[] | null,
        songsGroups: null as Group[] | null,
        coverages: new Map<ISong, number>(),
        updateHash: null as string | null,
        renderedHash: null as string | null,
        illustrations: null as AllIllustrations | null,
        element: null as ReactElement | null,
    }).current;
    self.props = props;

    const cssNthOfType = useRef<CssNthOfTypeRef>(null);

    const getHash = () => {
        const p = self.props;
        const { collectionIndex, failed } = services.collectionIndexProvider.peek();
        const results = progressionsSearchContext.results;
        return JSON.stringify([
            p.mode, p.query, p.mode === SongsMode.ArtistCompact ? p.songId : 0, p.artistId, p.shortlistedUserId,
            [collectionIndex === null ? null : objectId(collectionIndex), failed, results === null ? null : objectId(results.result.foundProgressionsWithCoverage)],
            p.likeCategoryId, self.illustrations === null ? 1 : 0,
        ]);
    };

    const update = () => {
        const collectionIndex = services.collectionIndexProvider.peek().collectionIndex;
        if (collectionIndex === null) return;

        const hash = getHash();
        if (hash === self.updateHash) return;
        self.updateHash = hash;

        const { mode, query, artistId, shortlistedUserId, likeCategoryId } = self.props;

        self.usersWithLikes = orderBy(
            distinct(collectionIndex.collection.songs.flatMap(x => x.likes.map(x => x.userId))).map(x => collectionIndex.usersById.get(x)!),
            (a, b) => compareStrings(a.title, b.title));

        let songs: readonly ISong[] = collectionIndex.collection.songs;

        const results = progressionsSearchContext.results;
        if (results !== null) {
            const progressionsByVersionId = toDictionary(progressionsCacheLoader.peek()!, x => x.version.id, x => x.progression);

            const foundProgressionsWithCoverage = results.result.foundProgressionsWithCoverage;
            const withCoverages = songs.map(x => {
                const coverages = x.versions
                    .map(x => progressionsByVersionId.get(x.id) ?? null)
                    .filter(x => x !== null)
                    .map(x => foundProgressionsWithCoverage.get(x!) ?? 0)
                    .filter(x => x > 0);

                if (coverages.length === 0) {
                    return { x, coverage: null as number | null };
                }

                const max = coverages.reduce((a, b) => compareNumbers(b, a) > 0 ? b : a);
                self.coverages.set(x, Math.trunc(Math.fround(max * 100)));

                return { x, coverage: max };
            });
            songs = orderBy(withCoverages.filter(x => x.coverage !== null), (a, b) => compareNumbers(b.coverage!, a.coverage!)).map(x => x.x);
        } else {
            if (artistId !== null) {
                songs = songs.filter(x => x.artists.includes(artistId));
            }

            if (query !== null) {
                const found = new Set(collectionIndex.find(query));
                songs = songs.filter(x => found.has(x));
            }

            if (shortlistedUserId !== null) {
                songs = songs.filter(x => x.likes.some(x => x.userId === shortlistedUserId && (x.level > 0 || x.likeCategoryId !== null)));
            } else if (mode === SongsMode.MyRating) {
                songs = songs.filter(x => x.likes.length > 0);
            } else if (mode === SongsMode.NewestLike) {
                songs = songs.filter(x => x.likes.length > 0);

                if (likeCategoryId !== null) {
                    songs = songs.filter(x => x.likes.some(x => x.likeCategoryId === likeCategoryId));
                }
            }

            const illustrations = self.illustrations;
            if (mode === SongsMode.WithIllustrations) {
                songs = illustrations === null
                    ? []
                    : songs.filter(x => illustrations.songs.has(x.id));
            }

            if (mode === SongsMode.WithoutIllustrations) {
                songs = illustrations === null
                    ? []
                    : songs.filter(x => !illustrations.songs.has(x.id) && illustrations.underGeneration !== null && !illustrations.underGeneration.includes(x.id));
            }

            if (mode === SongsMode.WithMyCollectives) {
                songs = songs.filter(x => x.versions.some(x => x.userId === identityProvider.requiredIdentity.id && x.collectiveId !== null));
            }

            if (likeCategoryId !== null) {
                songs = songs.filter(x => x.likes.some(x => x.likeCategoryId === likeCategoryId));
            }
        }

        const plain = () => songs.map(x => ({ artistName: null, artistId: null, song: x }) as Item);
        const byArtists = () => songs.flatMap(x => x.artists.map(x => collectionIndex.artistsById.get(x)!).map(a => ({ artistName: a.name, artistId: a.id, song: x }) as Item));

        let items: Item[];
        if (mode !== SongsMode.ArtistCompact && results !== null) {
            items = plain();
        } else {
            switch (mode) {
                case SongsMode.Artist:
                    items = orderBy(byArtists(), (a, b) => compareStrings(a.artistName, b.artistName), (a, b) => compareStrings(a.song.title, b.song.title));
                    break;
                case SongsMode.Title:
                    items = orderBy(plain(), (a, b) => compareStrings(a.song.title, b.song.title));
                    break;
                case SongsMode.Index:
                case SongsMode.IndexWithChords:
                    items = orderBy(plain(), (a, b) => a.song.index - b.song.index);
                    break;
                case SongsMode.RatingAll:
                case SongsMode.WithoutIllustrations:
                    items = orderByKeys(plain(),
                        x => [getRating(x.song.likes), maxNullableDate(x.song.likes.map(x => x.createdOn)), x.song.templateRating] as [number, DateTime | null, number | null],
                        (a, b) => compareNumbers(b[0], a[0])
                            || compareNullable(b[1], a[1], compareDates)
                            || compareNullable(b[2], a[2], compareNumbers));
                    break;
                case SongsMode.MyRating:
                    items = orderByKeys(plain(),
                        x => [
                            maxInt(x.song.likes.filter(x => x.userId === shortlistedUserId).map(x => x.level)),
                            minDate(x.song.likes.filter(x => x.userId === shortlistedUserId).map(x => x.createdOn)),
                            x.song.templateRating,
                        ] as [number, DateTime, number | null],
                        (a, b) => b[0] - a[0]
                            || compareDates(b[1], a[1])
                            || compareNullable(b[2], a[2], compareNumbers));
                    break;
                case SongsMode.NewestLike: {
                    const predicate: (x: Like) => boolean = shortlistedUserId !== null
                        ? x => x.userId === shortlistedUserId
                        : likeCategoryId !== null
                            ? x => x.likeCategoryId === likeCategoryId
                            : () => true;
                    items = orderByKeys(plain(),
                        x => [maxDate(x.song.likes.filter(predicate).map(x => x.createdOn)), x.song.templateRating] as [DateTime, number | null],
                        (a, b) => compareDates(b[0], a[0]) || compareNullable(b[1], a[1], compareNumbers));
                    break;
                }
                case SongsMode.IndexDesc:
                    items = orderBy(plain(), (a, b) => b.song.index - a.song.index);
                    break;
                case SongsMode.VersionCreationDesc:
                    items = orderByKeys(plain(),
                        x => maxDate([...x.song.versions.map(x => x.createdOn), x.song.createdOn]),
                        descending(compareDates));
                    break;
                case SongsMode.WithIllustrations:
                    items = orderByKeys(plain(),
                        x => [self.illustrations?.songs.get(x.song.id)?.latestCreatedOn ?? null, x.song.index] as [DateTime | null, number],
                        (a, b) => compareNullable(b[0], a[0], compareDates) || b[1] - a[1]);
                    break;
                case SongsMode.WithMyCollectives:
                    items = orderByKeys(plain(),
                        x => maxDate(x.song.versions.filter(x => x.collectiveId !== null && x.userId === identityProvider.requiredIdentity.id).map(x => x.createdOn)),
                        descending(compareDates));
                    break;
                case SongsMode.ArtistCompact:
                    items = results !== null
                        ? byArtists()
                        : orderBy(byArtists(), (a, b) => compareStrings(a.song.title, b.song.title), (a, b) => compareNullable(b.song.templateRating, a.song.templateRating, compareNumbers));
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        self.songs = items;
        self.songsGroups = mode === SongsMode.ArtistCompact
            ? orderBy(groupBy(items, x => ({ artistName: x.artistName, artistId: x.artistId })), (a, b) => compareStrings(a.key.artistName, b.key.artistName))
                .map(g => ({ key: g.key, items: g.items }))
            : null;

        stateHasChanged();
    };

    // OnNewCollectionIndexReceived
    const { collectionIndex, collectionIndexFailed, retryCollectionIndex } = useCollectionComponent(() => {
        self.illustrations = illustrationsProvider.peek();
        update();
        console.log('OnNewCollectionIndexReceived -> StateHasChanged');
    });

    // ProgressionsSearchContext.SearchResultsChanged += ...
    useEvent(progressionsSearchContext.searchResultsChanged, (_source: ProgressionsSearchSource) => update());

    // async void AddIllustrations()
    const addIllustrations = () => asyncVoid(async () => {
        self.illustrations = await illustrationsProvider.get();
        const mode = self.props.mode;
        if (self.illustrations !== null && (mode === SongsMode.WithIllustrations || mode === SongsMode.WithoutIllustrations)) {
            update();
        }
    });

    // OnParametersSetAsync
    useLayoutEffect(() => {
        searchContext.update(props.query);
        self.illustrations = illustrationsProvider.peek();
        if (self.illustrations === null && (props.mode === SongsMode.WithIllustrations || props.mode === SongsMode.WithoutIllustrations)) {
            addIllustrations();
        }

        update();
    }, [props.mode, props.likeCategoryId, props.query, props.songId, props.artistId, props.shortlistedUserId]);

    // ShouldRender (on every render request): CssNthOfType is told the SongId.
    useLayoutEffect(() => {
        cssNthOfType.current?.updated(self.props.songId);
    });

    // ShouldRender: only when the list was recomputed since the last render.
    if (self.element !== null) {
        if (self.renderedHash === self.updateHash) return self.element;
        self.renderedHash = self.updateHash;
    }

    const me = () => identityProvider.requiredIdentity.id;

    const renderHighlighting = (text: string): ReactNode => isNullOrWhiteSpace(props.query)
        ? text
        : <SearchHighlighting text={text} query={props.query} />;

    const maybeHighlighted = (songId: number) => songId === self.props.songId ? 'last-highlighted' : null;

    const goTo = (song: ISong) => {
        navigationManager.navigateTo(collectionNavigation.searchResultsLocalJustHighlight(song.id), false, true);

        if (self.props.mode === SongsMode.WithIllustrations) {
            navigationManager.navigateTo(collectionNavigation.searchResultsLocalIllustrationsLink(song));
        } else {
            navigationManager.navigateTo(collectionNavigation.searchResultsLocalLink(song));
        }
    };

    const { mode, likeCategoryId, query, shortlistedUserId } = props;

    const renderArtists = (song: ISong, className: string) => withIndices(
        orderBy(song.artists.map(x => ({ id: x, a: collectionIndex!.artistsById.get(x)! })), (a, b) => compareStrings(a.a.name, b.a.name)))
        .map(({ x: { a: artist }, i: j }) => (
            <Fragment key={j}>
                {j > 0 && <span>, </span>}
                <span className={className}>{renderHighlighting(artist.name)}</span>
            </Fragment>
        ));

    const renderItem = (item: Item) => {
        const likes = item.song.likes.filter(x => x.userId === (shortlistedUserId ?? me()));
        let flyoutHeart: ReactNode = null;
        if (likes.length > 0) {
            let level: string;
            if (likeCategoryId !== null) {
                if (likes.some(x => x.likeCategoryId === likeCategoryId)) {
                    level = 'x1';
                } else {
                    level = maxInt(likes.map(x => x.level)).toString();
                }
            } else {
                const nonCategory = likes.filter(x => x.likeCategoryId === null && x.level > 0).map(x => x.level);
                const maxNonCategory = nonCategory.length === 0 ? null : maxInt(nonCategory);
                if (maxNonCategory !== null) {
                    level = maxNonCategory.toString();
                } else if (likes.some(x => x.likeCategoryId !== null)) {
                    level = 'x1';
                } else {
                    level = '0';
                }
            }

            if (level === '0') {
                level = 'x0';
            }

            flyoutHeart = (
                <span className="flyout-heart">
                    <span className={`fas fa-heart heart-${level}`}></span>
                </span>
            );
        }

        const others = shortlistedUserId ?? me();
        const otherHearts = orderBy(
            groupBy(item.song.likes.filter(x => x.likeCategoryId === null), x => x.userId)
                .filter(x => x.key !== others)
                .map(g => maxInt(g.items.map(x => x.level))),
            (a, b) => b - a);

        return (
            <tr {...{ songid: item.song.id }}>
                <td align="left" valign="middle">
                    <div className="my-link inline-non-touch" onClick={blazorEventHandler(stateHasChanged, () => goTo(item.song))}>
                        {flyoutHeart}

                        {progressionsSearchContext.results !== null ? (
                            <span className="active-content songtitle">{self.coverages.get(item.song)}%. </span>
                        ) : mode === SongsMode.Index || mode === SongsMode.IndexDesc || mode === SongsMode.IndexWithChords || mode === SongsMode.VersionCreationDesc ? (
                            <span className="active-content">{item.song.index}. </span>
                        ) : null}

                        {mode === SongsMode.Title ? (
                            <>
                                <b><span className="active-content songtitle">{renderHighlighting(item.song.title)}</span></b>
                                {item.song.additionalKeywords !== null && (
                                    <span> ({renderHighlighting(item.song.additionalKeywords)})</span>
                                )}
                                <span> (</span>
                                {renderArtists(item.song, 'active-content artistname')}
                                <span>)</span>
                            </>
                        ) : (
                            <>
                                <span>
                                    {item.artistId !== null ? (
                                        <span className="artistname">{renderHighlighting(item.artistName!)}</span>
                                    ) : renderArtists(item.song, 'active-content')}
                                    <span> &mdash; </span>
                                </span>
                                {'\n                                '}
                                <b><span className="songtitle">{renderHighlighting(item.song.title)}</span></b>
                                {item.song.additionalKeywords !== null && (
                                    <span> ({renderHighlighting(item.song.additionalKeywords)})</span>
                                )}
                            </>
                        )}

                        {mode === SongsMode.IndexWithChords && (
                            <span ref={stringAttributes({ style: 'white-space: nowrap' })}>
                                {item.song.versions.map((version, k) => (
                                    <Fragment key={k}>
                                        <span></span>
                                        {'\n                                        '}
                                        <span className="source">{toLowerInvariant(version.source ?? version.uri.host)}</span>
                                    </Fragment>
                                ))}
                            </span>
                        )}

                        <span className="hearts-span">
                            {otherHearts.map((like, k) => (
                                <span key={k} className={`fas fa-heart heart-${like}`}></span>
                            ))}
                            {item.song.likes.some(x => x.likeCategoryId !== null && x.userId !== others) && (
                                <span className="fas fa-heart heart-x0"></span>
                            )}
                        </span>
                    </div>
                </td>
            </tr>
        );
    };

    const renderCompactGroup = (artist: Group, i: number) => (
        <tr key={i}>
            <td className="artistname-td">
                <span className="hyperlink artistname" onClick={blazorEventHandler(stateHasChanged, () => navigationManager.navigateTo(collectionNavigation.searchResultsLocalArtist(artist.key.artistId, self.props.query, artist.key.artistName!)))}>{renderHighlighting(artist.key.artistName!)}</span>
            </td>
            {'\n                    '}
            <td className="songs-td">
                <div>
                    {artist.items.map(({ song }, k) => (
                        <span key={k} className={`song-name ${maybeHighlighted(song.id) ?? ''} my-link`} onClick={blazorEventHandler(stateHasChanged, () => goTo(song))}>
                            <b><span className="songtitle">{renderHighlighting(song.title)}</span></b>
                            {song.additionalKeywords !== null && (
                                <span>&nbsp;({renderHighlighting(song.additionalKeywords)})</span>
                            )}
                            {groupBy(orderBy(song.likes.filter(x => x.likeCategoryId === null), (a, b) => b.level - a.level).filter(x => x.userId !== shortlistedUserId), x => x.userId)
                                .map(g => ({ userId: g.key, level: maxInt(g.items.map(x => x.level)) }))
                                .map((like, l) => (
                                    <span key={l} title={`&nbsp;${collectionIndex!.usersById.get(like.userId)!.title}`}>&nbsp;<span className={`fas fa-heart heart-${like.level}`}></span></span>
                                ))}
                            <span>; </span>
                        </span>
                    ))}
                </div>
            </td>
        </tr>
    );

    let element: ReactElement;
    if (collectionIndexFailed) {
        element = (
            <>
                <p><em>Не получилось загрузить, может нету интернета, библиотеку.</em></p>
                <button className="btn btn-outline-secondary" onClick={blazorEventHandler(stateHasChanged, retryCollectionIndex)}>Повтор</button>
            </>
        );
    } else if (collectionIndex === null || self.songs === null && self.songsGroups === null) {
        element = <div className="small-loading"><img className="music-loading" src="img/music-loading.gif" title="Загрузка..." alt="Загрузка..." /></div>;
    } else {
        const songs = self.songs!;
        element = (
            <>
                {progressionsSearchContext.results === null && (
                    <div className="input-group">
                        <div className="d-inline-block me-1">
                            <select ref={blazorValue(SongsMode[mode] ?? String(mode))} className={`my-dropdown-top border-indigo mb-2 ${mode === SongsMode.WithIllustrations || mode === SongsMode.WithoutIllustrations || mode === SongsMode.WithMyCollectives ? 'bg-success bg-opacity-25' : ''}`} onChange={blazorEventHandler(stateHasChanged, e => navigationManager.navigateTo(collectionNavigation.searchResultsLocalMode(enumParse<SongsMode>(SongsMode, e.currentTarget.value))))}>
                                {allSongsModes.filter(songsMode => !(songsMode === SongsMode.MyRating && shortlistedUserId === null)).map(songsMode => (
                                    <option key={songsMode} value={SongsMode[songsMode]} ref={booleanAttribute('selected', songsMode === mode)}>{getHumanTitle(SongsMode, SearchResultsLocalSongsModeHumanTitles, songsMode)}</option>
                                ))}
                            </select>
                        </div>
                        {'\n            '}
                        <div className="d-inline-block me-1">
                            {self.usersWithLikes !== null && (
                                <select ref={blazorValue(String(shortlistedUserId ?? -1))} className={`my-dropdown-top border-indigo col selectusers mb-2 ${shortlistedUserId !== null ? 'bg-success bg-opacity-25' : ''}`} onChange={blazorEventHandler(stateHasChanged, e => {
                                    const value = e.currentTarget.value;
                                    const currentMode = self.props.mode;
                                    navigationManager.navigateTo(value === '-1'
                                        ? collectionNavigation.searchResultsLocalShortlistedUserNone(currentMode === SongsMode.MyRating || currentMode === SongsMode.NewestLike ? SongsMode.RatingAll : currentMode)
                                        : collectionNavigation.searchResultsLocalShortlistedUser(parseInt64(value)));
                                })}>
                                    {[null, ...orderBy(self.usersWithLikes, (a, b) => compareStrings(a.title, b.title))].map((user, k) => (
                                        <option key={k} value={user?.id ?? -1}>{user?.title ?? 'Все шортлисты'}</option>
                                    ))}
                                </select>
                            )}
                        </div>
                        {collectionIndex.collection.likeCategories.length > 0 && (
                            <div className="d-inline-block">
                                <select ref={blazorValue(String(likeCategoryId ?? -1))} className={`my-dropdown-top border-indigo mb-2 ${likeCategoryId !== null ? 'bg-success bg-opacity-25' : ''}`} onChange={blazorEventHandler(stateHasChanged, e => {
                                    const value = parseInt32(e.currentTarget.value);
                                    navigationManager.navigateTo(collectionNavigation.searchResultsLocalLikeCategoryId(value === -1 ? null : value));
                                })}>
                                    {[null, ...collectionIndex.collection.likeCategories].map((likeCategory, k) => (
                                        <option key={k} value={likeCategory?.id ?? -1} ref={booleanAttribute('selected', (likeCategory?.id ?? null) === likeCategoryId)}>{likeCategory === null ? 'Все списочки' : likeCategory.name + (likeCategory.userId === me() ? '' : ` - ${collectionIndex.usersById.get(likeCategory.userId)!.title}`)}</option>
                                    ))}
                                </select>
                            </div>
                        )}
                        {(shortlistedUserId !== null || mode === SongsMode.NewestLike || mode === SongsMode.MyRating || mode === SongsMode.WithIllustrations || mode === SongsMode.WithoutIllustrations || mode === SongsMode.WithMyCollectives || likeCategoryId !== null) && (
                            <button className="btn btn-secondary btn-secondary-close col-auto" onClick={blazorEventHandler(stateHasChanged, () => navigationManager.navigateTo(collectionNavigation.noShortlistOrLikeCategory()))}><svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 384 512"><path d="M342.6 150.6c12.5-12.5 12.5-32.8 0-45.3s-32.8-12.5-45.3 0L192 210.7 86.6 105.4c-12.5-12.5-32.8-12.5-45.3 0s-12.5 32.8 0 45.3L146.7 256 41.4 361.4c-12.5 12.5-12.5 32.8 0 45.3s32.8 12.5 45.3 0L192 301.3 297.4 406.6c12.5 12.5 32.8 12.5 45.3 0s12.5-32.8 0-45.3L237.3 256 342.6 150.6z" /></svg></button>
                        )}
                    </div>
                )}

                {mode === SongsMode.ArtistCompact ? (
                    <table className="mt-3 songs table  table-artistcompact ">

                        {self.songsGroups!.map(renderCompactGroup)}
                    </table>
                ) : (
                    <>
                        <CssNthOfType ref={cssNthOfType} />
                        {'\n        '}
                        <table className="mt-3 songs table table-one-column mb-0 local">
                            <Virtualize items={songs}>
                                {renderItem}
                            </Virtualize>
                        </table>
                        {isNullOrWhiteSpace(query) && (
                            <button className="btn btn-secondary col-auto" onClick={blazorEventHandler(stateHasChanged, () => navigationManager.navigateTo(`/print?indices=${self.songs!.map(x => x.song.index).join(',')}`))}>В книжку!</button>
                        )}
                    </>
                )}
            </>
        );
    }

    self.element = element;
    return element;
}

/** int.Parse */
function parseInt32(value: string): number {
    const parsed = tryParseInt32(value);
    if (parsed === null) throw new Error(`The input string '${value}' was not in a correct format.`);
    return parsed;
}

/** long.Parse */
function parseInt64(value: string): number {
    if (!/^[\t\n\v\f\r ]*[+-]?[0-9]+[\t\n\v\f\r ]*$/.test(value)) throw new Error(`The input string '${value}' was not in a correct format.`);
    return Number(value.trim());
}
