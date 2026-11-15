import { jsonBytes, jsonClass, jsonDictionary, jsonInt32, jsonProperty, jsonString, type JsonContract } from '../../../../common/dotnet/json/JsonSerializer';

/** GetPdfsResponse.Pdf (a nested C# class). */
export class GetPdfsResponsePdf {
    readonly pdfData: Uint8Array | null;
    readonly pageCount: number;
    /** C# `string` without an initializer: null until set. */
    previewLink: string | null;

    constructor(init: { pdfData?: Uint8Array | null; pageCount: number; previewLink?: string | null }) {
        this.pdfData = init.pdfData ?? null;
        this.pageCount = init.pageCount;
        this.previewLink = init.previewLink ?? null;
    }

    static readonly json: JsonContract<GetPdfsResponsePdf> = jsonClass<GetPdfsResponsePdf>('OneShelf.Frontend.Api.Model.V3.Api.GetPdfsResponse+Pdf', init => new GetPdfsResponsePdf(init as never), () => [
        jsonProperty('PdfData', jsonBytes),
        jsonProperty('PageCount', jsonInt32, { required: true }),
        jsonProperty('PreviewLink', jsonString),
    ]);
}

export class GetPdfsResponse {
    /** C# `Dictionary` without an initializer: null when the JSON has none. */
    readonly pdfs: Map<string, GetPdfsResponsePdf> | null;
    readonly volume: Uint8Array | null;

    constructor(init: { pdfs?: Map<string, GetPdfsResponsePdf> | null; volume?: Uint8Array | null }) {
        this.pdfs = init.pdfs ?? null;
        this.volume = init.volume ?? null;
    }

    static readonly json: JsonContract<GetPdfsResponse> = jsonClass<GetPdfsResponse>('OneShelf.Frontend.Api.Model.V3.Api.GetPdfsResponse', init => new GetPdfsResponse(init as never), () => [
        jsonProperty('Pdfs', jsonDictionary('string', GetPdfsResponsePdf.json)),
        jsonProperty('Volume', jsonBytes),
    ]);
}
