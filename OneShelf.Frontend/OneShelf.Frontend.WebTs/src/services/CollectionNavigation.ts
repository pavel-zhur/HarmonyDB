// The original: Services/CollectionNavigation.cs — the addresses within /collection.
// The query parameters are named after the Collection page's parameters (nameof(Collection.X)).
// GetExternalNavigationLink's C# overloads are TypeScript overloads of one method, told apart by their arguments.

import { navigationManager } from '../framework/navigation';
import { escapeDataString, getUriWithQueryParameter, getUriWithQueryParameters, type QueryParameterValue } from '../framework/navigationManagerExtensions';
import type { ISong } from '../lib/api/OneShelf.Frontend.Api.Model/V3/Databasish/Interfaces/ISong';
import type { Like } from '../lib/api/OneShelf.Frontend.Api.Model/V3/Databasish/Like';
import type { Version } from '../lib/api/OneShelf.Frontend.Api.Model/V3/Databasish/Version';
import type { Guid } from '../lib/common/dotnet/Guid';
import { InvalidOperationException } from '../lib/common/dotnet/exceptions';
import { isNullOrWhiteSpace, trim } from '../lib/common/dotnet/text';
import type { ILogger } from '../framework/logging';
import { SearchResultsInternetSongsMode } from '../shared/SearchResultsInternet';
import { SearchResultsLocalSongsMode as SongsMode } from '../shared/SearchResultsLocal';
import type { IdentityProvider } from './IdentityProvider';

type Parameters = (readonly [string, QueryParameterValue])[];

/** `SingleOrDefault(predicate)` */
function singleOrDefault<T>(source: readonly T[], predicate: (x: T) => boolean): T | null {
    let found: T | null = null;
    let count = 0;
    for (const x of source) {
        if (predicate(x)) {
            if (++count > 1) throw new InvalidOperationException('Sequence contains more than one matching element');
            found = x;
        }
    }

    return found;
}

export class CollectionNavigation {
    private readonly _logger: ILogger;
    private readonly _identityProvider: IdentityProvider;

    constructor(logger: ILogger, identityProvider: IdentityProvider) {
        this._logger = logger;
        this._identityProvider = identityProvider;
    }

    collectiveAdd(versionId: number): string {
        return `/collective?action=Add&versionId=${versionId}`;
    }

    collectiveDelete(collectiveId: Guid): string {
        return `/collective?action=Delete&collectiveId=${collectiveId}`;
    }

    collectiveUpdate(collectiveId: Guid): string {
        return `/collective?action=Update&collectiveId=${collectiveId}`;
    }

    collectiveRefresh(collectiveId: Guid): string {
        return `/collective?action=Refresh&collectiveId=${collectiveId}`;
    }

    chordsListLink(externalId: string | null, versionId: number | null): string {
        return getUriWithQueryParameters([
            ['ExternalId', externalId],
            ['VersionId', versionId],
            ['IsOpen2', true],
        ]);
    }

    illustrationsLink(): string {
        return getUriWithQueryParameters([
            ['ExternalId', null],
            ['VersionId', null],
            ['IsOpen2', true],
        ]);
    }

    searchResultsInternetJustHighlight(virtualSongArtist: string, virtualSongTitle: string): string {
        return getUriWithQueryParameters([
            ['VirtualSongArtist', virtualSongArtist],
            ['VirtualSongTitle', virtualSongTitle],
            ['SongId', null],
        ]);
    }

    searchResultsInternetLink(virtualSongArtist: string, virtualSongTitle: string, externalId: string): string {
        return getUriWithQueryParameters([
            ['VirtualSongArtist', virtualSongArtist],
            ['VirtualSongTitle', virtualSongTitle],
            ['IsOpen', true],
            ['IsOpen2', true],
            ['ExternalId', externalId],
            ['VersionId', null],
            ['SongId', null],
        ]);
    }

    searchResultsInternetMode(songsMode: SearchResultsInternetSongsMode): string {
        return getUriWithQueryParameter('ModeInternet', SearchResultsInternetSongsMode[songsMode] ?? String(songsMode));
    }

    searchResultsLocalLink(song: ISong): string {
        let selectVersion: Version | null = null;
        if (song.versions.length === 1) {
            selectVersion = song.versions[0]!;
        } else if (song.versions.length > 0) {
            const keyed = song.versions.map(x => ({
                x,
                k1: x.collectiveId != null ? x.userId === this._identityProvider.requiredIdentity.id ? 1 : 2 : 3,
                k2: singleOrDefault<Like>(song.likes, l => l.versionId === x.id && l.userId === this._identityProvider.requiredIdentity.id && l.likeCategoryId == null)?.level ?? 0,
            }));
            keyed.sort((a, b) => (a.k1 - b.k1) || (b.k2 - a.k2) || a.x.createdOn.compareTo(b.x.createdOn));
            selectVersion = keyed[0]!.x;
        }

        return getUriWithQueryParameters([
            ['SongId', song.id],
            ['VirtualSongArtist', null],
            ['VirtualSongTitle', null],
            ['IsOpen', true],
            ['IsOpen2', selectVersion !== null],
            ['ExternalId', selectVersion?.externalId ?? null],
            ['VersionId', selectVersion?.id ?? null],
        ]);
    }

    searchResultsLocalIllustrationsLink(song: ISong): string {
        return getUriWithQueryParameters([
            ['SongId', song.id],
            ['VirtualSongArtist', null],
            ['VirtualSongTitle', null],
            ['IsOpen', true],
            ['IsOpen2', true],
            ['ExternalId', null],
            ['VersionId', null],
        ]);
    }

    searchResultsLocalJustHighlight(songId: number): string {
        return getUriWithQueryParameters([
            ['SongId', songId],
            ['VirtualSongArtist', null],
            ['VirtualSongTitle', null],
        ]);
    }

    searchResultsLocalSongChordsRemoved(songId: number): string {
        return getUriWithQueryParameters([
            ['SongId', songId],
            ['VirtualSongArtist', null],
            ['VirtualSongTitle', null],
            ['IsOpen', true],
            ['IsOpen2', false],
            ['ExternalId', null],
            ['VersionId', null],
        ]);
    }

    collectionQueryChange(newValue: string | null, artistId: number | null, artistName: string | null): string {
        newValue = isNullOrWhiteSpace(newValue) ? null : newValue;
        const noArtist = artistId === null || artistName === null || !` ${newValue ?? ''} `.includes(` ${artistName} `);
        return getUriWithQueryParameters([
            ['Query', newValue],
            ['ArtistId', noArtist ? null : artistId],
            ['IsOpen', false],
            ['IsOpen2', false],
        ]);
    }

    collectionClearSearch(query: string | null, artistId: number | null, artistName: string | null): string {
        const noArtist = artistId === null || artistName === null || !` ${query ?? ''} `.includes(` ${artistName} `) || (query === null ? null : trim(query)) === artistName;
        return getUriWithQueryParameters([
            ['Query', noArtist ? null : artistName],
            ['ArtistId', noArtist ? null : artistId],
            ['IsOpen', false],
            ['IsOpen2', false],
        ]);
    }

    collectionLayoutStateChanged(isOpen: boolean, isOpen2: boolean): string {
        return getUriWithQueryParameters([
            ['IsOpen', isOpen],
            ['IsOpen2', isOpen2],
        ]);
    }

    collectionSearchInternet(query: string): string {
        return getUriWithQueryParameters([
            ['Query', query],
            ['IsOpen', false],
            ['IsOpen2', false],
            ['ArtistId', null],
            ['SongId', null],
        ]);
    }

    searchResultsLocalMode(songsMode: SongsMode): string {
        return getUriWithQueryParameter('ModeLocal', SongsMode[songsMode] ?? String(songsMode));
    }

    searchResultsLocalShortlistedUser(shortlistedUserId: number, withReset = false): string {
        const parameters = new Map<string, QueryParameterValue>([
            ['ShortlistedUserId', shortlistedUserId],
            ['IsOpen', false],
            ['IsOpen2', false],
            ['ModeLocal', SongsMode[SongsMode.NewestLike]],
        ]);

        if (withReset) {
            parameters.set('Query', null);
            parameters.set('LikeCategoryId', null);
        }

        return getUriWithQueryParameters([...parameters] as Parameters);
    }

    searchResultsLocalLikeCategoryId(likeCategoryId: number | null, withReset = false): string {
        const parameters = new Map<string, QueryParameterValue>([
            ['LikeCategoryId', likeCategoryId],
            ['IsOpen', false],
            ['IsOpen2', false],
            ['ModeLocal', SongsMode[SongsMode.NewestLike]],
        ]);

        if (likeCategoryId === null) {
            parameters.set('ModeLocal', SongsMode[SongsMode.RatingAll]);
        }

        if (withReset) {
            parameters.set('Query', null);
            parameters.set('ShortlistedUserId', null);
        }

        return getUriWithQueryParameters([...parameters] as Parameters);
    }

    searchResultsLocalShortlistedUserNone(songsMode: SongsMode): string {
        return getUriWithQueryParameters([
            ['ModeLocal', SongsMode[songsMode] ?? String(songsMode)],
            ['ShortlistedUserId', null],
        ]);
    }

    searchResultsLocalArtist(artistId: number | null, existingQuery: string | null, artistName: string): string {
        return getUriWithQueryParameters([
            ['ArtistId', artistId],
            ['SongId', null],
            ['VirtualSongArtist', null],
            ['VirtualSongTitle', null],
            ['IsOpen', false],
            ['IsOpen2', false],
            ['Query', ` ${existingQuery ?? ''} `.includes(` ${artistName} `) ? existingQuery : isNullOrWhiteSpace(existingQuery) ? artistName : `${artistName} ${existingQuery}`],
        ]);
    }

    /** GetExternalNavigationLink(int artistId, string artistName) */
    getExternalNavigationLink(artistId: number, artistName: string): string;
    /** GetExternalNavigationLink(string query) */
    getExternalNavigationLink(query: string): string;
    /** GetExternalNavigationLink(int likeCategoryId) */
    getExternalNavigationLink(likeCategoryId: number): string;
    /** GetExternalNavigationLink(string query, string externalId, string virtualSongArtist, string virtualSongTitle) */
    getExternalNavigationLink(query: string, externalId: string, virtualSongArtist: string, virtualSongTitle: string): string;
    /** GetExternalNavigationLink(int songId, int versionId, string? externalId, SongsMode? mode = null) */
    getExternalNavigationLink(songId: number, versionId: number, externalId: string | null, mode?: SongsMode | null): string;
    getExternalNavigationLink(...args: unknown[]): string {
        if (args.length === 1 && typeof args[0] === 'string') {
            const query = args[0];
            return `/collection?Query=${escapeDataString(query)}`;
        }

        if (args.length === 1) {
            const likeCategoryId = args[0] as number;
            return `/collection?LikeCategoryId=${likeCategoryId}`;
        }

        if (args.length === 2 && typeof args[1] === 'string') {
            const [artistId, artistName] = args as [number, string];
            return `/collection?ArtistId=${artistId}&ModeLocal=${SongsMode[SongsMode.Title]}&Query=${escapeDataString(artistName)}`;
        }

        if (typeof args[0] === 'string') {
            const [query, externalId, virtualSongArtist, virtualSongTitle] = args as [string, string, string, string];
            return `/collection?Query=${escapeDataString(query)}&ExternalId=${escapeDataString(externalId)}&isopen=true&isopen2=true&VirtualSongArtist=${escapeDataString(virtualSongArtist)}&VirtualSongTitle=${escapeDataString(virtualSongTitle)}`;
        }

        const [songId, versionId, externalId, mode] = args as [number, number, string | null, SongsMode | null | undefined];
        return `/collection?SongId=${songId}&VersionId=${versionId}&isopen=true&isopen2=true&ExternalId=${externalId === null ? '' : escapeDataString(externalId)}${mode == null ? '' : `&ModeLocal=${SongsMode[mode] ?? String(mode)}`}`;
    }

    /** GetExternalIllustrationsNavigationLink() and GetExternalIllustrationsNavigationLink(int songId) */
    getExternalIllustrationsNavigationLink(songId?: number): string {
        if (songId === undefined) {
            return `/collection?ModeLocal=${SongsMode[SongsMode.WithIllustrations]}`;
        }

        return `/collection?ModeLocal=${SongsMode[SongsMode.WithIllustrations]}&SongId=${songId}&isopen=true&isopen2=true`;
    }

    getExternalMyCollectivesNavigationLink(): string {
        return `/collection?ModeLocal=${SongsMode[SongsMode.WithMyCollectives]}`;
    }

    getExternalNavigationLinkMyRatingNewest(): string {
        return `/collection?ModeLocal=${SongsMode[SongsMode.NewestLike]}&ShortlistedUserId=${this._identityProvider.requiredIdentity.id}`;
    }

    getMyContainedString(): string {
        return `ShortlistedUserId=${this._identityProvider.requiredIdentity.id}`;
    }

    noShortlistOrLikeCategory(): string {
        return getUriWithQueryParameters([
            ['ModeLocal', null],
            ['ShortlistedUserId', null],
            ['LikeCategoryId', null],
        ]);
    }

    synced(): void {
        if (!navigationManager.uri.toLowerCase().includes('IsOpen2=true'.toLowerCase())) {
            navigationManager.navigateTo(`/collection?ModeLocal=${SongsMode[SongsMode.VersionCreationDesc]}`);
        }
    }
}
