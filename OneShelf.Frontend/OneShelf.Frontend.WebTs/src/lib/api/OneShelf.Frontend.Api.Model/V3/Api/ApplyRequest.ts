import type { Guid } from '../../../../common/dotnet/Guid';
import { jsonClass, jsonInt32, jsonList, jsonNullable, jsonProperty, type JsonContract } from '../../../../common/dotnet/json/JsonSerializer';
import { Identity } from '../../../OneShelf.Authorization.Api.Model/Identity';
import type { ActionBase } from '../Instant/ActionBase';
import { ImportedVersion } from '../Instant/ImportedVersion';
import { RemovedVersion } from '../Instant/RemovedVersion';
import { UpdatedLike } from '../Instant/UpdatedLike';
import { VisitedChords } from '../Instant/VisitedChords';
import { VisitedSearch } from '../Instant/VisitedSearch';

export interface ApplyRequestInit {
    identity: Identity;
    visitedChords?: VisitedChords[];
    visitedSearches?: VisitedSearch[];
    importedVersions?: ImportedVersion[];
    removedVersions?: RemovedVersion[];
    updatedLikes?: UpdatedLike[];
    etag?: number | null;
}

/** Also what the site keeps in localStorage ("InstantActionsV1"), there with `Identity = null!`. */
export class ApplyRequest {
    identity: Identity;
    readonly visitedChords: VisitedChords[];
    readonly visitedSearches: VisitedSearch[];
    readonly importedVersions: ImportedVersion[];
    readonly removedVersions: RemovedVersion[];
    readonly updatedLikes: UpdatedLike[];
    etag: number | null;

    constructor(init: ApplyRequestInit) {
        this.identity = init.identity;
        this.visitedChords = init.visitedChords !== undefined ? init.visitedChords : [];
        this.visitedSearches = init.visitedSearches !== undefined ? init.visitedSearches : [];
        this.importedVersions = init.importedVersions !== undefined ? init.importedVersions : [];
        this.removedVersions = init.removedVersions !== undefined ? init.removedVersions : [];
        this.updatedLikes = init.updatedLikes !== undefined ? init.updatedLikes : [];
        this.etag = init.etag ?? null;
    }

    addFrom(another: ApplyRequest): void {
        this.importedVersions.push(...another.importedVersions);
        this.removedVersions.push(...another.removedVersions);
        this.updatedLikes.push(...another.updatedLikes);
        this.visitedChords.push(...another.visitedChords);
        this.visitedSearches.push(...another.visitedSearches);
    }

    getIds(): Guid[] {
        return ([] as ActionBase[])
            .concat(this.updatedLikes)
            .concat(this.importedVersions)
            .concat(this.removedVersions)
            .concat(this.visitedSearches)
            .concat(this.visitedChords)
            .map(x => x.actionId);
    }

    static readonly json: JsonContract<ApplyRequest> = jsonClass<ApplyRequest>('OneShelf.Frontend.Api.Model.V3.Api.ApplyRequest', init => new ApplyRequest(init as unknown as ApplyRequestInit), () => [
        jsonProperty('Identity', Identity.json, { required: true }),
        jsonProperty('VisitedChords', jsonList(VisitedChords.json)),
        jsonProperty('VisitedSearches', jsonList(VisitedSearch.json)),
        jsonProperty('ImportedVersions', jsonList(ImportedVersion.json)),
        jsonProperty('RemovedVersions', jsonList(RemovedVersion.json)),
        jsonProperty('UpdatedLikes', jsonList(UpdatedLike.json)),
        jsonProperty('Etag', jsonNullable(jsonInt32)),
    ]);
}
