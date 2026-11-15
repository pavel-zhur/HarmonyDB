// The original: Shared/SearchResultsInternet.razor.
//
// The results of the internet search (SearchContext), grouped by artist and title. As there: the results are taken on
// the first render and whenever a parameter changes (OnInitializedAsync, OnParametersSetAsync), and when they arrive
// (SearchContext.Arrived). The select's value is set as Blazor sets it (blazorValue), the options' `selected` as an
// attribute.
import { useLayoutEffect, useRef } from 'react';
import { blazorEventHandler } from '../framework/blazorEventHandler';
import { navigationManager } from '../framework/navigation';
import { blazorValue, booleanAttribute } from '../framework/attributes';
import { Virtualize } from '../framework/Virtualize';
import { errorHandledTask } from '../framework/unhandledExceptions';
import { useCollectionComponent } from '../hooks/useCollectionComponent';
import { useAsyncEvent } from '../hooks/useEvent';
import { useStateHasChanged } from '../hooks/useStateHasChanged';
import type { SearchHeader } from '../lib/api/HarmonyDB.Source.Api.Model/V1/SearchHeader';
import { enumParse } from '../lib/common/dotnet/Enum';
import { ArgumentOutOfRangeException } from '../lib/common/dotnet/exceptions';
import { compareStrings, groupBy } from '../lib/common/dotnet/Linq';
import type { Uri } from '../lib/common/dotnet/Uri';
import type { HumanTitles } from '../models/HumanTitleAttribute';
import { getHumanTitle } from '../models/HumanTitleAttribute';
import { services } from '../services/registry';
import type { SearchArrival } from '../services/SearchContext';
import { AvailableChords } from './AvailableChords';
import './SearchResultsInternet.scoped.scss';

/** SearchResultsInternet.SongsMode */
export enum SearchResultsInternetSongsMode {
    Artist = 0,
    ArtistCompact = 1,
    ArtistCompactOriginal = 2,
    Title = 3,
    Original = 4,
}

/** The [HumanTitle] of SearchResultsInternet.SongsMode's members. */
export const SearchResultsInternetSongsModeHumanTitles: HumanTitles = {
    [SearchResultsInternetSongsMode.Artist]: 'По исполнителю',
    [SearchResultsInternetSongsMode.ArtistCompact]: 'По исполнителю',
    [SearchResultsInternetSongsMode.ArtistCompactOriginal]: 'По релевантности',
    [SearchResultsInternetSongsMode.Title]: 'По имени',
    [SearchResultsInternetSongsMode.Original]: 'По релевантности',
};

const SongsMode = SearchResultsInternetSongsMode;
type SongsMode = SearchResultsInternetSongsMode;

export interface SearchResultsInternetProps {
    modeInternet: string | null;
    virtualSongArtist: string | null;
    virtualSongTitle: string | null;
    isCompact: boolean;
    googleSearchLink: string | null;
}

interface Expanded {
    artistName: string;
    songTitle: string;
    headers: SearchHeader[];
}

interface Compact {
    artistName: string;
    songs: { songTitle: string; headers: SearchHeader[] }[];
}

const googleIcon = (
    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 488 512"><path d="M488 261.8C488 403.3 391.1 504 248 504 110.8 504 0 393.2 0 256S110.8 8 248 8c66.8 0 123 24.5 166.3 64.9l-67.5 64.9C258.5 52.6 94.3 116.6 94.3 256c0 86.5 69.1 156.6 153.7 156.6 98.2 0 135-70.4 140.8-106.9H248v-85.3h236.1c2.3 12.7 3.9 24.9 3.9 41.4z" /></svg>
);

export function SearchResultsInternet(props: SearchResultsInternetProps) {
    const { modeInternet, virtualSongArtist, virtualSongTitle, isCompact, googleSearchLink } = props;
    const collectionNavigation = services.collectionNavigation;
    const searchContext = services.searchContext;
    const { collectionIndex, collectionIndexFailed, retryCollectionIndex } = useCollectionComponent();
    const stateHasChanged = useStateHasChanged();

    const self = useRef({ failed: false, expanded: null as Expanded[] | null, compact: null as Compact[] | null }).current;

    const getAvailableModes = (): SongsMode[] => isCompact
        ? [SongsMode.ArtistCompactOriginal, SongsMode.ArtistCompact]
        : [SongsMode.Original, SongsMode.Artist, SongsMode.Title];

    const getMode = (): SongsMode => {
        const available = getAvailableModes();
        const parsed = modeInternet === null ? available[0]! : enumParse<SongsMode>(SongsMode, modeInternet);
        return available.includes(parsed) ? parsed : available[0]!;
    };

    const resultArrived = async (results: SearchArrival | null) => {
        if (results === null) {
            self.expanded = null;
            self.compact = null;
            self.failed = false;
            stateHasChanged();
            return;
        }

        if (results.failed) {
            self.failed = true;
            stateHasChanged();
            return;
        }

        self.failed = false;

        let expanded: Expanded[] = groupBy(results.results!, x => ({ artist: x.artist, title: x.title }))
            .map(g => ({ artistName: g.key.artist, songTitle: g.key.title, headers: g.items.map(x => x.header) }));

        const mode = getMode();
        let compact: Compact[] | null;
        if (mode === SongsMode.ArtistCompact || mode === SongsMode.ArtistCompactOriginal) {
            compact = groupBy(expanded, x => x.artistName)
                .map(g => ({ artistName: g.key, songs: g.items.map(x => ({ songTitle: x.songTitle, headers: x.headers })) }));
        } else {
            compact = null;
        }

        switch (mode) {
            case SongsMode.Artist:
                expanded = [...expanded].sort((a, b) => compareStrings(a.artistName, b.artistName) || compareStrings(a.songTitle, b.songTitle));
                break;
            case SongsMode.ArtistCompact:
                compact = [...compact!]
                    .sort((a, b) => compareStrings(a.artistName, b.artistName))
                    .map(x => ({ artistName: x.artistName, songs: [...x.songs].sort((a, b) => compareStrings(a.songTitle, b.songTitle)) }));
                break;
            case SongsMode.ArtistCompactOriginal:
                break;
            case SongsMode.Title:
                expanded = [...expanded].sort((a, b) => compareStrings(a.songTitle, b.songTitle) || compareStrings(a.artistName, b.artistName));
                break;
            case SongsMode.Original:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        self.expanded = expanded;
        self.compact = compact;
        stateHasChanged();
    };

    // SearchContext.Arrived += ResultArrived (OnInitializedAsync); -= (Dispose)
    useAsyncEvent(searchContext.arrived, resultArrived);

    // OnInitializedAsync and OnParametersSetAsync: ResultArrived(SearchContext.Peek)
    useLayoutEffect(() => {
        void errorHandledTask(resultArrived(searchContext.peek));
    }, [modeInternet, virtualSongArtist, virtualSongTitle, isCompact, googleSearchLink]);

    const maybeHighlighted = (artistName: string, songTitle: string) =>
        artistName === virtualSongArtist && songTitle === virtualSongTitle ? 'last-highlighted' : null;

    const goTo = (artistName: string, songTitle: string, externalId: string, _sourceUri: Uri) => {
        navigationManager.navigateTo(collectionNavigation.searchResultsInternetJustHighlight(artistName, songTitle), false, true);
        navigationManager.navigateTo(collectionNavigation.searchResultsInternetLink(artistName, songTitle, externalId));
    };

    const mode = getMode();

    if (collectionIndexFailed) {
        return (
            <div className="sidebar-section-body">
                <p><em>Не получилось загрузить, может нету интернета, исполнителей.</em></p>
                <button className="btn btn-secondary" onClick={blazorEventHandler(stateHasChanged, retryCollectionIndex)}>Повтор</button>
            </div>
        );
    }

    if (collectionIndex === null) {
        return <div className="small-loading"><img className="music-loading" src="img/music-loading.gif" title="Загрузка..." alt="Загрузка..." /></div>;
    }

    if (self.failed) {
        return (
            <div className="sidebar-section-body">
                <div className="row">
                    <div className="col-md-12">
                        <p><em>Не получилось загрузить, может нету интернета, библиотеку.</em></p>
                        <button className="btn btn-secondary" onClick={blazorEventHandler(stateHasChanged, () => searchContext.restartFailed())}>Повтор</button>
                    </div>
                </div>
            </div>
        );
    }

    const isCompactMode = mode === SongsMode.ArtistCompact || mode === SongsMode.ArtistCompactOriginal;
    if (self.expanded === null || isCompactMode && self.compact === null) {
        return <div className="small-loading"><img className="music-loading" src="img/music-loading.gif" title="Загрузка..." alt="Загрузка..." /></div>;
    }

    return (
        <>
            <div className="page-header bg-yellow opacity-75 mt-2 text-body">
                <div className="page-header-content d-block">
                    <div className="d-flex justify-content-between">
                        <div className="breadcrumb">
                            <h5 className="my-pt-25">Интернет</h5>
                        </div>

                        <div className="d-flex">
                            <div className="row align-items-center">
                                <div className="col  ">
                                    <select ref={blazorValue(SongsMode[mode])} className="dropdown btn" onChange={blazorEventHandler(stateHasChanged, e => navigationManager.navigateTo(collectionNavigation.searchResultsInternetMode(enumParse<SongsMode>(SongsMode, e.currentTarget.value))))}>
                                        {getAvailableModes().map(songsMode => (
                                            <option key={songsMode} value={SongsMode[songsMode]} ref={booleanAttribute('selected', songsMode === mode)}>{getHumanTitle(SongsMode, SearchResultsInternetSongsModeHumanTitles, songsMode)}</option>
                                        ))}
                                    </select>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </div>

            {isCompactMode ? (
                <div className="sidebar-section-body">
                    <table className="songs table-artistcompact">
                        {self.compact!.map(({ artistName, songs }, i) => (
                            <tr key={i}>
                                <td className="artistName-td ">
                                    <span className="artistName">{artistName}</span>
                                </td>
                                <td className="songs-td">

                                    {songs.map(({ songTitle, headers }, j) => (
                                        <div key={j}>
                                            <span className={`song-name  ${maybeHighlighted(artistName, songTitle) ?? ''} my-link`} onClick={blazorEventHandler(stateHasChanged, () => goTo(artistName, songTitle, headers[0]!.externalId, headers[0]!.sourceUri))}>
                                                <b><span className="active-content songtitle">{songTitle}</span></b>
                                                {'\n                                        '}
                                                <AvailableChords headers={headers}></AvailableChords>
                                            </span>
                                        </div>
                                    ))}

                                </td>
                            </tr>
                        ))}

                    </table>
                    <div className="col-md-12">
                        <div className="input-group  mt-3 mb-5">
                            <a className="btn btn-primary" href={googleSearchLink ?? undefined}>{googleIcon} Искать в гугле</a>
                        </div>
                    </div>
                </div>
            ) : (
                <div className="sidebar-section-body">
                    <table className="songs table-one-column mt-2">
                        <Virtualize items={self.expanded}>
                            {item => (
                                <tr className={`${maybeHighlighted(item.artistName, item.songTitle) ?? ''} `}>
                                    <td>
                                        <div className="my-link inline-non-touch" onClick={blazorEventHandler(stateHasChanged, () => goTo(item.artistName, item.songTitle, item.headers[0]!.externalId, item.headers[0]!.sourceUri))}>
                                            {mode === SongsMode.Title ? (
                                                <>
                                                    <b><span className="active-content songtitle">{item.songTitle}</span></b>
                                                    {'\n                                    '}
                                                    <span>&nbsp;(</span>
                                                    {'\n                                    '}
                                                    <span className="artistName">{item.artistName}</span>
                                                    {'\n                                    '}
                                                    <span>)</span>
                                                </>
                                            ) : (
                                                <>
                                                    <span className="active-content artistName">{item.artistName}</span>
                                                    {'\n                                    '}
                                                    <span> &mdash; </span>
                                                    {'\n                                    '}
                                                    <b><span className="songtitle">{item.songTitle}</span></b>
                                                </>
                                            )}
                                            <AvailableChords headers={item.headers}></AvailableChords>
                                        </div>
                                    </td>
                                </tr>
                            )}
                        </Virtualize>
                    </table>
                    <div className="col-md-12">
                        <div className="input-group mt-3 mb-5">
                            <a className="btn btn-primary" href={googleSearchLink ?? undefined}>{googleIcon} Искать в гугле </a>
                        </div>
                    </div>
                </div>
            )}
        </>
    );
}
