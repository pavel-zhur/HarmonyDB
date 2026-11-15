import { GetSongsRequest } from '../HarmonyDB.Source.Api.Model/V1/Api/GetSongsRequest';
import { GetSongsResponse } from '../HarmonyDB.Source.Api.Model/V1/Api/GetSongsResponse';
import { SearchRequest } from '../HarmonyDB.Source.Api.Model/V1/Api/SearchRequest';
import { SearchResponse } from '../HarmonyDB.Source.Api.Model/V1/Api/SearchResponse';
import type { Identity } from '../OneShelf.Authorization.Api.Model/Identity';
import { ApiClientBase, type HttpFetch } from '../OneShelf.Common.Api.Client/ApiClientBase';
import { ApiClientOptions } from '../OneShelf.Common.Api.Client/ApiClientOptions';
import type { ConfigurationGetter } from '../OneShelf.Common.Api.Client/Configuration';
import * as ApiUrls from '../OneShelf.Frontend.Api.Model/V3/Api/ApiUrls';
import { ApplyRequest } from '../OneShelf.Frontend.Api.Model/V3/Api/ApplyRequest';
import { ApplyResponse } from '../OneShelf.Frontend.Api.Model/V3/Api/ApplyResponse';
import { CreateLikeCategoryRequest } from '../OneShelf.Frontend.Api.Model/V3/Api/CreateLikeCategoryRequest';
import { CreateLikeCategoryResponse } from '../OneShelf.Frontend.Api.Model/V3/Api/CreateLikeCategoryResponse';
import { DeleteLikeCategoryRequest } from '../OneShelf.Frontend.Api.Model/V3/Api/DeleteLikeCategoryRequest';
import { DeleteLikeCategoryResponse } from '../OneShelf.Frontend.Api.Model/V3/Api/DeleteLikeCategoryResponse';
import { FormatPreviewRequest } from '../OneShelf.Frontend.Api.Model/V3/Api/FormatPreviewRequest';
import { FormatPreviewResponse } from '../OneShelf.Frontend.Api.Model/V3/Api/FormatPreviewResponse';
import { GetPdfsRequest } from '../OneShelf.Frontend.Api.Model/V3/Api/GetPdfsRequest';
import type { GetPdfsRequestVersion } from '../OneShelf.Frontend.Api.Model/V3/Api/GetPdfsRequestVersion';
import { GetPdfsResponse } from '../OneShelf.Frontend.Api.Model/V3/Api/GetPdfsResponse';
import { GetVisitedChordsRequest } from '../OneShelf.Frontend.Api.Model/V3/Api/GetVisitedChordsRequest';
import { GetVisitedChordsResponse } from '../OneShelf.Frontend.Api.Model/V3/Api/GetVisitedChordsResponse';
import { GetVisitedSearchesRequest } from '../OneShelf.Frontend.Api.Model/V3/Api/GetVisitedSearchesRequest';
import { GetVisitedSearchesResponse } from '../OneShelf.Frontend.Api.Model/V3/Api/GetVisitedSearchesResponse';
import { IllustrationsRequest } from '../OneShelf.Frontend.Api.Model/V3/Api/IllustrationsRequest';
import { IllustrationsResponse } from '../OneShelf.Frontend.Api.Model/V3/Api/IllustrationsResponse';
import { UpdateLikeCategoryRequest } from '../OneShelf.Frontend.Api.Model/V3/Api/UpdateLikeCategoryRequest';
import { UpdateLikeCategoryResponse } from '../OneShelf.Frontend.Api.Model/V3/Api/UpdateLikeCategoryResponse';
import type { LikeCategory } from '../OneShelf.Frontend.Api.Model/V3/Databasish/LikeCategory';

/** Registered by AddFrontendApiClient: the options come from "FrontendApiClientOptions". */
export class FrontendApiClient extends ApiClientBase {
    constructor(options: ApiClientOptions, httpFetch?: HttpFetch) {
        super(options, httpFetch);
    }

    /** AddFrontendApiClient(configuration) */
    static create(configuration: ConfigurationGetter, httpFetch?: HttpFetch): FrontendApiClient {
        return new FrontendApiClient(ApiClientOptions.bind(configuration, 'FrontendApiClient'), httpFetch);
    }

    async getChords(identity: Identity, externalIds: readonly string[], unauthorized: (() => void) | null, cancellationToken?: AbortSignal): Promise<GetSongsResponse> {
        return await this.httpPost(ApiUrls.V3GetChords, new GetSongsRequest({ externalIds, identity }), GetSongsRequest.json, GetSongsResponse.json, unauthorized, cancellationToken);
    }

    async formatPreview(identity: Identity, content: string, unauthorized: (() => void) | null, cancellationToken?: AbortSignal): Promise<FormatPreviewResponse> {
        return await this.httpPost(ApiUrls.V3FormatPreview, new FormatPreviewRequest({ content, identity }), FormatPreviewRequest.json, FormatPreviewResponse.json, unauthorized, cancellationToken);
    }

    async getPdfs(identity: Identity, includeData: boolean, versions: GetPdfsRequestVersion[], unauthorized: (() => void) | null, includeInspiration = false, caption: string | null = null, reindex = false): Promise<GetPdfsResponse> {
        return await this.httpPost(ApiUrls.V3GetPdfs, new GetPdfsRequest({ includeData, identity, versions, caption, includeInspiration, reindex }), GetPdfsRequest.json, GetPdfsResponse.json, unauthorized);
    }

    async search(identity: Identity, query: string, unauthorized: (() => void) | null): Promise<SearchResponse> {
        return await this.httpPost(ApiUrls.V3Search, new SearchRequest({ query, identity }), SearchRequest.json, SearchResponse.json, unauthorized);
    }

    async getVisitedChords(identity: Identity, unauthorized: (() => void) | null, pageIndex = 0): Promise<GetVisitedChordsResponse> {
        return await this.httpPost(ApiUrls.V3GetVisitedChords, new GetVisitedChordsRequest({ pageIndex, identity }), GetVisitedChordsRequest.json, GetVisitedChordsResponse.json, unauthorized);
    }

    async getVisitedSearches(identity: Identity, unauthorized: (() => void) | null, pageIndex = 0): Promise<GetVisitedSearchesResponse> {
        return await this.httpPost(ApiUrls.V3GetVisitedSearches, new GetVisitedSearchesRequest({ pageIndex, identity }), GetVisitedSearchesRequest.json, GetVisitedSearchesResponse.json, unauthorized);
    }

    async apply(applyRequest: ApplyRequest, unauthorized: (() => void) | null): Promise<ApplyResponse> {
        return await this.httpPost(ApiUrls.V3Apply, applyRequest, ApplyRequest.json, ApplyResponse.json, unauthorized);
    }

    async deleteLikeCategory(identity: Identity, id: number, unauthorized: (() => void) | null): Promise<DeleteLikeCategoryResponse> {
        return await this.httpPost(ApiUrls.V3DeleteLikeCategory, new DeleteLikeCategoryRequest({
            identity,
            likeCategoryId: id,
        }), DeleteLikeCategoryRequest.json, DeleteLikeCategoryResponse.json, unauthorized);
    }

    async createLikeCategory(identity: Identity, likeCategory: LikeCategory, unauthorized: (() => void) | null): Promise<CreateLikeCategoryResponse> {
        return await this.httpPost(ApiUrls.V3CreateLikeCategory, new CreateLikeCategoryRequest({
            identity,
            likeCategory,
        }), CreateLikeCategoryRequest.json, CreateLikeCategoryResponse.json, unauthorized);
    }

    async updateLikeCategory(identity: Identity, likeCategory: LikeCategory, unauthorized: (() => void) | null): Promise<UpdateLikeCategoryResponse> {
        return await this.httpPost(ApiUrls.V3UpdateLikeCategory, new UpdateLikeCategoryRequest({
            identity,
            likeCategory,
        }), UpdateLikeCategoryRequest.json, UpdateLikeCategoryResponse.json, unauthorized);
    }

    async getIllustrations(identity: Identity, eTag: number | null, allVersions: boolean, unauthorized: (() => void) | null): Promise<IllustrationsResponse> {
        return await this.httpPost(ApiUrls.V3Illustrations, new IllustrationsRequest({
            identity,
            etag: eTag,
            allVersions,
        }), IllustrationsRequest.json, IllustrationsResponse.json, unauthorized);
    }

    async getIllustrationsAnonymous(): Promise<IllustrationsResponse | null> {
        return await this.httpGet(ApiUrls.V3IllustrationsAnonymous, IllustrationsResponse.json);
    }
}
