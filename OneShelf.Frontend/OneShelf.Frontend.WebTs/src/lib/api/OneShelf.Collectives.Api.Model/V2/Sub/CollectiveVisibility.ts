import { jsonEnum } from '../../../../common/dotnet/json/JsonSerializer';

export enum CollectiveVisibility {
    Private = 0,
    Club = 1,
    Public = 2,
}

export const CollectiveVisibilityJson = jsonEnum<CollectiveVisibility>('OneShelf.Collectives.Api.Model.V2.Sub.CollectiveVisibility');
