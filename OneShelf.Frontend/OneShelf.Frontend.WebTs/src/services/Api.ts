// The original: Services/Api.cs. It takes its FrontendApiClient from a service scope of its own (one client for the
// application's lifetime): here the client is given to it.

import type { FrontendApiClient } from '../lib/api/OneShelf.Frontend.Api.Client/FrontendApiClient';
import type { ApplyRequest } from '../lib/api/OneShelf.Frontend.Api.Model/V3/Api/ApplyRequest';
import type { GetPdfsRequestVersion } from '../lib/api/OneShelf.Frontend.Api.Model/V3/Api/GetPdfsRequestVersion';
import type { GetPdfsResponse } from '../lib/api/OneShelf.Frontend.Api.Model/V3/Api/GetPdfsResponse';
import type { GetVisitedChordsResponse } from '../lib/api/OneShelf.Frontend.Api.Model/V3/Api/GetVisitedChordsResponse';
import type { GetVisitedSearchesResponse } from '../lib/api/OneShelf.Frontend.Api.Model/V3/Api/GetVisitedSearchesResponse';
import type { IllustrationsResponse } from '../lib/api/OneShelf.Frontend.Api.Model/V3/Api/IllustrationsResponse';
import type { Collection } from '../lib/api/OneShelf.Frontend.Api.Model/V3/Databasish/Collection';
import type { LikeCategory } from '../lib/api/OneShelf.Frontend.Api.Model/V3/Databasish/LikeCategory';
import type { Chords } from '../lib/api/HarmonyDB.Source.Api.Model/V1/Chords';
import type { SearchHeader } from '../lib/api/HarmonyDB.Source.Api.Model/V1/SearchHeader';
import type { NodeHtml } from '../lib/representations/NodeHtml';
import type { IdentityProvider } from './IdentityProvider';

export class Api {
    private readonly _identityProvider: IdentityProvider;
    private readonly _frontendApiClient: FrontendApiClient;

    constructor(identityProvider: IdentityProvider, frontendApiClient: FrontendApiClient) {
        this._identityProvider = identityProvider;
        this._frontendApiClient = frontendApiClient;
    }

    dispose(): void {
    }

    async getChords(externalIds: readonly string[], cancellationToken?: AbortSignal): Promise<Map<string, Chords>> {
        return (await this._frontendApiClient.getChords(this._identityProvider.requiredIdentity, externalIds, this._identityProvider.navigateToUserUnauthorizedIdentity, cancellationToken)).songs;
    }

    async formatPreview(content: string, cancellationToken?: AbortSignal): Promise<NodeHtml> {
        return (await this._frontendApiClient.formatPreview(this._identityProvider.requiredIdentity, content, this._identityProvider.navigateToUserUnauthorizedIdentity, cancellationToken)).output;
    }

    async getPdfs(includeData: boolean, versions: GetPdfsRequestVersion[], includeInspiration = false, caption: string | null = null, reindex = false): Promise<GetPdfsResponse> {
        return await this._frontendApiClient.getPdfs(this._identityProvider.requiredIdentity, includeData, versions, this._identityProvider.navigateToUserUnauthorizedIdentity, includeInspiration, caption, reindex);
    }

    async search(query: string): Promise<SearchHeader[]> {
        return (await this._frontendApiClient.search(this._identityProvider.requiredIdentity, query, this._identityProvider.navigateToUserUnauthorizedIdentity)).headers;
    }

    async getVisitedChords(pageIndex = 0): Promise<GetVisitedChordsResponse> {
        return await this._frontendApiClient.getVisitedChords(this._identityProvider.requiredIdentity, this._identityProvider.navigateToUserUnauthorizedIdentity, pageIndex);
    }

    async getVisitedSearches(pageIndex = 0): Promise<GetVisitedSearchesResponse> {
        return await this._frontendApiClient.getVisitedSearches(this._identityProvider.requiredIdentity, this._identityProvider.navigateToUserUnauthorizedIdentity, pageIndex);
    }

    async apply(applyRequest: ApplyRequest, existingCollection: Collection | null): Promise<Collection> {
        applyRequest.etag = existingCollection?.etag ?? null;
        return (await this._frontendApiClient.apply(applyRequest, this._identityProvider.navigateToUserUnauthorizedIdentity))?.collection ?? existingCollection!;
    }

    async deleteLikeCategory(id: number): Promise<boolean> {
        return (await this._frontendApiClient.deleteLikeCategory(this._identityProvider.requiredIdentity, id, this._identityProvider.navigateToUserUnauthorizedIdentity)).result;
    }

    async createLikeCategory(likeCategory: LikeCategory): Promise<boolean> {
        return (await this._frontendApiClient.createLikeCategory(this._identityProvider.requiredIdentity, likeCategory, this._identityProvider.navigateToUserUnauthorizedIdentity)).result;
    }

    async updateLikeCategory(likeCategory: LikeCategory): Promise<boolean> {
        return (await this._frontendApiClient.updateLikeCategory(this._identityProvider.requiredIdentity, likeCategory, this._identityProvider.navigateToUserUnauthorizedIdentity)).result;
    }

    async getIllustrations(eTag: number | null, allVersions: boolean): Promise<IllustrationsResponse> {
        return await this._frontendApiClient.getIllustrations(this._identityProvider.requiredIdentity, eTag, allVersions, this._identityProvider.navigateToUserUnauthorizedIdentity);
    }

    async getIllustrationsAnonymous(): Promise<IllustrationsResponse | null> {
        return await this._frontendApiClient.getIllustrationsAnonymous();
    }
}
