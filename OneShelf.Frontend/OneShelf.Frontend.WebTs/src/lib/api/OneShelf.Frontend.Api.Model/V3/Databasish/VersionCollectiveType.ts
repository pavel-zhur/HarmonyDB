import { jsonEnum } from '../../../../common/dotnet/json/JsonSerializer';

export enum VersionCollectiveType {
    Private = 0,
    Public = 1,
}

export const VersionCollectiveTypeJson = jsonEnum<VersionCollectiveType>('OneShelf.Frontend.Api.Model.V3.Databasish.VersionCollectiveType');
