import { jsonClass, jsonProperty, jsonUri, type JsonContract } from '../../../../common/dotnet/json/JsonSerializer';
import type { Uri } from '../../../../common/dotnet/Uri';

export class ImagePublicUrls {
    readonly url1024: Uri;
    readonly url512: Uri;
    readonly url256: Uri;
    readonly url128: Uri;

    constructor(init: { url1024: Uri; url512: Uri; url256: Uri; url128: Uri }) {
        this.url1024 = init.url1024;
        this.url512 = init.url512;
        this.url256 = init.url256;
        this.url128 = init.url128;
    }

    static readonly json: JsonContract<ImagePublicUrls> = jsonClass<ImagePublicUrls>('OneShelf.Frontend.Api.Model.V3.Illustrations.ImagePublicUrls', init => new ImagePublicUrls(init as never), () => [
        jsonProperty('Url1024', jsonUri, { required: true }),
        jsonProperty('Url512', jsonUri, { required: true }),
        jsonProperty('Url256', jsonUri, { required: true }),
        jsonProperty('Url128', jsonUri, { required: true }),
    ]);
}
