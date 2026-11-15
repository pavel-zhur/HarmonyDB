// The original: Pages/Artists.razor.
//
// Query is a parameter from the query string that the page also sets itself (typing, clearing): the page's own value
// lasts until the query string changes, when the parameters are set again (and the list is updated, as
// OnParametersSetAsync does). Mode is parsed with Enum.TryParse (case-sensitive names or numbers).
import { useLayoutEffect, useRef, type FormEvent } from 'react';
import { useLocation } from 'react-router-dom';
import { navigationManager } from '../framework/navigation';
import { escapeDataString } from '../framework/navigationManagerExtensions';
import { PageTitle } from '../framework/PageTitle';
import { QueryParameters } from '../framework/supplyParameterFromQuery';
import { blazorValue } from '../framework/attributes';
import { useCollectionComponent } from '../hooks/useCollectionComponent';
import { useStateHasChanged } from '../hooks/useStateHasChanged';
import { blazorEventHandler } from '../framework/blazorEventHandler';
import type { Artist } from '../lib/api/OneShelf.Frontend.Api.Model/V3/Databasish/Artist';
import { ArgumentOutOfRangeException } from '../lib/common/dotnet/exceptions';
import { enumParse } from '../lib/common/dotnet/Enum';
import { groupBy } from '../lib/common/dotnet/Linq';
import { isNullOrWhiteSpace, split, StringSplitOptions, toLowerInvariant } from '../lib/common/dotnet/text';
import { searchSyntaxRemoveSeparators, Separators, toSearchSyntax } from '../lib/common/HarmonyDB.Common.FullTextSearch/FullTextSearchExtensions';
import { getRating } from '../lib/common/OneShelf.Common.Songs/SongsConstants';
import { getHumanTitle, type HumanTitles } from '../models/HumanTitleAttribute';
import { services } from '../services/registry';
import { compareOrdinal } from '../dotnet/collections';
import './Artists.scoped.scss';

/** Artists.ArtistsMode */
export enum ArtistsMode {
    Title = 0,
    Rating = 1,
    MyRating = 2,
}

export const ArtistsModeHumanTitles: HumanTitles = {
    [ArtistsMode.Title]: 'По имени',
    [ArtistsMode.Rating]: 'По рейтингу',
    [ArtistsMode.MyRating]: 'По моему рейтингу',
};

const artistsModes = [ArtistsMode.Title, ArtistsMode.Rating, ArtistsMode.MyRating];

/** Math.Round(value, 1): to even, as .NET scales, rounds and scales back. */
function round1(value: number): number {
    const scaled = value * 10;
    let rounded = Math.round(scaled);
    if (Math.abs(scaled % 1) === 0.5) rounded = 2 * Math.round(scaled / 2);
    return rounded / 10;
}

/** Enum.TryParse<ArtistsMode>(value, out x) */
function tryParseMode(value: string): ArtistsMode | null {
    try {
        return enumParse<ArtistsMode>(ArtistsMode, value);
    } catch {
        return null;
    }
}

interface ArtistRow {
    artist: Artist;
    songsCount: number;
    rating: number;
}

export function Artists() {
    const identityProvider = services.identityProvider;
    const collectionNavigation = services.collectionNavigation;
    const stateHasChanged = useStateHasChanged();
    const self = useRef({ search: null as string | null, query: null as string | null, modeRaw: null as string | null, artists: null as ArtistRow[] | null }).current;

    const update = () => {
        const collectionIndex = services.collectionIndexProvider.peek().collectionIndex;
        if (collectionIndex === null) {
            return;
        }

        const me = identityProvider.requiredIdentity.id;
        const songs = collectionIndex.collection.songs.map(x => ({
            rating: getRating(x.likes),
            myRating: getRating(x.likes.filter(l => l.userId === me)),
            artists: x.artists,
        }));
        let artists = groupBy(songs.flatMap(x => x.artists.map(a => ({ a, rating: x.rating, myRating: x.myRating }))), x => x.a)
            .map(g => ({
                artist: collectionIndex.artistsById.get(g.key)!,
                songsCount: g.items.length,
                rating: g.items.reduce((sum, x) => sum + x.rating, 0),
                myRating: g.items.reduce((sum, x) => sum + x.myRating, 0),
            }))
            .map(g => ({ ...g, withSynonyms: searchSyntaxRemoveSeparators(toSearchSyntax(toLowerInvariant([...g.artist.synonyms, g.artist.name].join(' ')))) }));

        const query = self.query;
        if (!isNullOrWhiteSpace(query)) {
            const terms = split(toSearchSyntax(toLowerInvariant(query!)), Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            artists = artists.filter(x => terms.every(t => x.withSynonyms.includes(t)));
        }

        const mode = getMode();
        let ordered: typeof artists;
        switch (mode) {
            case ArtistsMode.Title:
                ordered = [...artists].sort((a, b) => compareOrdinal(a.artist.name, b.artist.name));
                break;
            case ArtistsMode.Rating:
                ordered = [...artists].sort((a, b) => b.rating - a.rating);
                break;
            case ArtistsMode.MyRating:
                ordered = [...artists].sort((a, b) => b.myRating - a.myRating);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        self.artists = ordered.map(x => ({ artist: x.artist, songsCount: x.songsCount, rating: x.rating }));
    };

    const { collectionIndex, collectionIndexFailed, retryCollectionIndex } = useCollectionComponent(() => {
        update();
    });

    // The parameters, set again when the query string changes (SetParametersAsync, then OnParametersSetAsync).
    const search = useLocation().search;
    if (self.search !== search) {
        self.search = search;
        const parameters = new QueryParameters(search);
        self.query = parameters.string('Query');
        self.modeRaw = parameters.string('mode');
        update();
    }

    function getMode(): ArtistsMode {
        const parsed = self.modeRaw !== null ? tryParseMode(self.modeRaw) : null;
        return parsed ?? ArtistsMode.Rating;
    }

    // OnInitializedAsync
    useLayoutEffect(() => {
        if (!identityProvider.ensureAuthorized()) return;
        update();
        stateHasChanged();
    }, []);

    const getNavigationLink = (mode: ArtistsMode, query: string | null) =>
        `/artists?mode=${ArtistsMode[mode] ?? String(mode)}&query=${query === null ? '' : escapeDataString(query)}`;

    const navigateTo = (mode: ArtistsMode, query: string | null) => {
        navigationManager.navigateTo(getNavigationLink(mode, query), false, true);
    };

    const change = () => {
        if (isNullOrWhiteSpace(self.query)) {
            navigateTo(getMode(), null);
            return;
        }

        navigateTo(getMode(), self.query);
    };

    const clearSearch = () => {
        self.query = null;
        update();
    };


    const mode = getMode();
    return (
        <div className="left-collection-col col ">
            <PageTitle>Исполнители</PageTitle>

            <div className="input-group  input-group-lg  ">
                <input ref={blazorValue(self.query)} type="text" className="form-control" placeholder="Поиск..." onInput={blazorEventHandler(stateHasChanged, (e: FormEvent<HTMLInputElement>) => { self.query = e.currentTarget.value; change(); })} />

                <div className="input-group-append">
                    <button className="input-group-text button-clear" type="button" onClick={blazorEventHandler(stateHasChanged, clearSearch)}>
                        <i className="ph-x"></i>
                    </button>
                </div>

            </div>
            {collectionIndexFailed ? (
                <>
                    <p><em>Не получилось загрузить, может нету интернета, исполнителей.</em></p>
                    <button className="btn btn-secondary" onClick={blazorEventHandler(stateHasChanged, retryCollectionIndex)}>Повтор</button>
                </>
            ) : collectionIndex === null ? (
                <div className="small-loading"><img className="music-loading" src="img/music-loading.gif" title="Загрузка..." alt="Загрузка..." /></div>
            ) : (
                <div className="row">
                    <div className="col-md-12 search-result-section-content">
                        <div className="align-content-sm-center mt-3 mb-3">
                            <div className="row ">
                                <div className="col-md-auto">
                                    <div className="row ">
                                        <div className="input-group artists-switch">
                                            {artistsModes.map(artistsMode => artistsMode === mode ? (
                                                <div key={artistsMode} className="input-group-prepend ">
                                                    <b className="btn btn-secondary  px-2">{getHumanTitle(ArtistsMode, ArtistsModeHumanTitles, artistsMode)}</b>
                                                </div>
                                            ) : (
                                                <div key={artistsMode} className="input-group-prepend ">
                                                    <a className="btn btn-secondary   px-2" href={getNavigationLink(artistsMode, self.query)}>{getHumanTitle(ArtistsMode, ArtistsModeHumanTitles, artistsMode)}</a>
                                                </div>
                                            ))}
                                        </div>
                                    </div>
                                </div>
                            </div>
                        </div>
                        <table className="mt-3 songs table  table-artistcompact ">
                            <thead>
                                <tr>
                                    <td><span>Исполнитель</span></td>
                                    <td className="text-center"><span className="px-2">Песен</span></td>
                                    <td className="text-center"><span className="px-2">Рейтинг</span></td>
                                </tr>
                            </thead>
                            {self.artists!.map(({ artist, songsCount, rating }, i) => (
                                <tr key={i}>
                                    <td>
                                        <a href={collectionNavigation.getExternalNavigationLink(artist.id, artist.name)}>{artist.name}</a>
                                    </td>
                                    <td className="text-center px-2">{songsCount}</td>
                                    <td className="text-center px-2"><span className="hearts-span"><span className="fas fa-heart heart-1"></span></span>&times;{round1(rating)}</td>
                                </tr>
                            ))}
                        </table>
                    </div>
                </div>
            )}
        </div>
    );
}
