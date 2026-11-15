// The JSON the C# models produce and accept, written out by hand from the C# types and the options in force:
// the API clients use `new JsonSerializerOptions { PropertyNamingPolicy = CamelCase }` (ApiClientBase);
// the site's IndexedDB cache uses JsonSerializer's defaults (DataProvider: JsonSerializer.Serialize(chords)).

import { describe, expect, it } from 'vitest';
import { DateTime, DateTimeKind } from '../common/dotnet/DateTime';
import { JsonException } from '../common/dotnet/exceptions';
import {
    camelCaseJsonSerializerOptions as camel,
    defaultJsonSerializerOptions as pascal,
    deserialize,
    serialize,
} from '../common/dotnet/json/JsonSerializer';
import { NoteAlteration } from '../common/HarmonyDB.Common/NoteAlteration';
import { NodeHtml } from '../representations/NodeHtml';
import { Note } from '../representations/Note';
import { RepresentationSettings } from '../representations/RepresentationSettings';
import { GetSongsResponse } from './HarmonyDB.Source.Api.Model/V1/Api/GetSongsResponse';
import { SearchResponse } from './HarmonyDB.Source.Api.Model/V1/Api/SearchResponse';
import { Chords } from './HarmonyDB.Source.Api.Model/V1/Chords';
import { Identity } from './OneShelf.Authorization.Api.Model/Identity';
import { CollectiveVersion } from './OneShelf.Collectives.Api.Model/V2/Sub/CollectiveVersion';
import { CollectiveVisibility } from './OneShelf.Collectives.Api.Model/V2/Sub/CollectiveVisibility';
import { ApplyRequest } from './OneShelf.Frontend.Api.Model/V3/Api/ApplyRequest';
import { GetPdfsRequest } from './OneShelf.Frontend.Api.Model/V3/Api/GetPdfsRequest';
import { GetPdfsRequestVersion } from './OneShelf.Frontend.Api.Model/V3/Api/GetPdfsRequestVersion';
import { GetPdfsResponse } from './OneShelf.Frontend.Api.Model/V3/Api/GetPdfsResponse';
import { IllustrationsResponse } from './OneShelf.Frontend.Api.Model/V3/Api/IllustrationsResponse';
import { Collection } from './OneShelf.Frontend.Api.Model/V3/Databasish/Collection';
import { LikeCategoryAccess } from './OneShelf.Frontend.Api.Model/V3/Databasish/LikeCategoryAccess';
import { UpdatedLike } from './OneShelf.Frontend.Api.Model/V3/Instant/UpdatedLike';
import { VisitedSearch } from './OneShelf.Frontend.Api.Model/V3/Instant/VisitedSearch';

const identity = new Identity({ id: 123, firstName: 'Ann', authDate: 1700000000, hash: 'abc' });
const identityJson = '{"id":123,"first_name":"Ann","last_name":null,"username":null,"photo_url":null,"auth_date":1700000000,"hash":"abc"}';

describe('Identity', () => {
    it('has the [JsonPropertyName] names under any naming policy', () => {
        expect(serialize(identity, Identity.json, camel)).toBe(identityJson);
        expect(serialize(identity, Identity.json, pascal)).toBe(identityJson);
    });

    it('reads the Telegram login data', () => {
        const read = deserialize('{"id":5,"first_name":"B","username":"b","photo_url":"https://t.me/i.jpg","auth_date":1,"hash":"h"}', Identity.json);
        expect(read.equals(new Identity({ id: 5, firstName: 'B', username: 'b', photoUrl: 'https://t.me/i.jpg', authDate: 1, hash: 'h' }))).toBe(true);
        expect(read.title).toBe('B');
    });

    it('requires the hash', () => {
        expect(() => deserialize('{"id":5}', Identity.json)).toThrow(JsonException);
    });
});

describe('requests', () => {
    it('ApplyRequest: camelCase, a derived record\'s own members before its base\'s, Guid lowercase, DateTime round-trip format, non-ASCII escaped', () => {
        const request = new ApplyRequest({
            identity,
            visitedSearches: [new VisitedSearch({
                actionId: '0f8fad5b-d9cb-469f-a165-70867728950e',
                happenedOn: DateTime.parseJson('2024-05-06T07:08:09.5Z')!,
                query: 'кино',
            })],
            updatedLikes: [new UpdatedLike({
                actionId: '00000000-0000-0000-0000-000000000001',
                happenedOn: DateTime.parseJson('2024-05-06T07:08:09')!,
                songId: 1,
                versionId: null,
                level: 3,
                transpose: -2,
                likeCategoryId: null,
            })],
            etag: 7,
        });
        expect(serialize(request, ApplyRequest.json, camel)).toBe(
            `{"identity":${identityJson},"visitedChords":[],`
            + '"visitedSearches":[{"query":"\\u043A\\u0438\\u043D\\u043E","actionId":"0f8fad5b-d9cb-469f-a165-70867728950e","happenedOn":"2024-05-06T07:08:09.5Z"}],'
            + '"importedVersions":[],"removedVersions":[],'
            + '"updatedLikes":[{"songId":1,"versionId":null,"level":3,"transpose":-2,"likeCategoryId":null,"actionId":"00000000-0000-0000-0000-000000000001","happenedOn":"2024-05-06T07:08:09"}],'
            + '"etag":7}');
    });

    it('ApplyRequest: a missing list keeps its initializer, a missing ActionId gets a new Guid', () => {
        const read = deserialize('{"Identity":null,"VisitedSearches":[{"Query":"q","HappenedOn":"2024-01-01T00:00:00"}]}', ApplyRequest.json, pascal);
        expect(read.identity).toBeNull();
        expect(read.visitedChords).toEqual([]);
        expect(read.etag).toBeNull();
        expect(read.visitedSearches[0]!.actionId).toMatch(/^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/);
        expect(read.getIds()).toEqual([read.visitedSearches[0]!.actionId]);
    });

    it('GetPdfsRequest: the ChunkSize initializer, nullable enum as its number', () => {
        const request = new GetPdfsRequest({
            identity,
            versions: [new GetPdfsRequestVersion({ versionId: 5, transpose: -1, alteration: NoteAlteration.Flat, twoColumns: true })],
            includeData: false,
        });
        expect(serialize(request, GetPdfsRequest.json, camel)).toBe(
            `{"identity":${identityJson},"versions":[{"versionId":5,"transpose":-1,"alteration":0,"twoColumns":true}],`
            + '"chunkSize":20,"includeData":false,"caption":null,"includeInspiration":false,"reindex":false}');
    });

    it('escapes strings as System.Text.Json\'s default encoder', () => {
        const request = new ApplyRequest({ identity: new Identity({ hash: 'a"b\\c<>&\'+`\n\u0001é😀' }) });
        expect(serialize(request, ApplyRequest.json, camel)).toContain('"hash":"a\\u0022b\\\\c\\u003C\\u003E\\u0026\\u0027\\u002B\\u0060\\n\\u0001\\u00E9\\uD83D\\uDE00"');
    });
});

const chordsCamel = '{"output":{"name":"pre","classes":null,"children":['
    + '{"nodeChord":{"children":[{"nodeText":{"text":"Am"}}]}},'
    + '{"nodeText":{"text":"\\nla"}}]},'
    + '"bestTonality":"Am","isStable":true,"isPublic":false,"unstableErrorMessage":null,'
    + '"sourceUri":"https://example.com/a","type":null,"specificAttributes":{"SourceColor":"Primary","n":1.50},'
    + '"externalId":"123","source":"S","rating":4.5,"title":"T","artists":["A"]}';

const chordsPascal = '{"Output":{"Name":"pre","Classes":null,"Children":['
    + '{"NodeChord":{"Children":[{"NodeText":{"Text":"Am"}}]}},'
    + '{"NodeText":{"Text":"\\nla"}}]},'
    + '"BestTonality":"Am","IsStable":true,"IsPublic":false,"UnstableErrorMessage":null,'
    + '"SourceUri":"https://example.com/a","Type":null,"SpecificAttributes":{"SourceColor":"Primary","n":1.50},'
    + '"ExternalId":"123","Source":"S","Rating":4.5,"Title":"T","Artists":["A"]}';

describe('chords', () => {
    it('reads the node tree from the API (camelCase, one member of NodeChild set)', () => {
        const response = deserialize(`{"songs":{"123":${chordsCamel}}}`, GetSongsResponse.json, camel);
        const chords = response.songs.get('123')!;
        expect(chords).toBeInstanceOf(Chords);
        expect(chords.output).toBeInstanceOf(NodeHtml);
        expect(chords.output.children[0]!.nodeChord).not.toBeNull();
        expect(chords.output.children[0]!.nodeText).toBeNull();
        expect(chords.output.asChords(new RepresentationSettings())).toEqual(['[!0!]m']);
        expect(chords.rating).toBe(4.5);
        expect(chords.artists).toEqual(['A']);
    });

    it('writes it for the IndexedDB cache with the default options (C# names), and reads it back', () => {
        const chords = deserialize(chordsCamel, Chords.json, camel);
        expect(serialize(chords, Chords.json, pascal)).toBe(chordsPascal);
        expect(serialize(deserialize(chordsPascal, Chords.json, pascal), Chords.json, camel)).toBe(chordsCamel);
    });

    it('a note as NodeNote { Note { Value, Alteration } }', () => {
        const html = deserialize('{"Name":"b","Children":[{"NodeNote":{"Note":{"Value":1,"Alteration":0}}}]}', NodeHtml.json, pascal);
        expect(html.children[0]!.nodeNote!.note.equals(new Note(1, NoteAlteration.Flat))).toBe(true);
        expect(serialize(html, NodeHtml.json, camel)).toBe('{"name":"b","classes":null,"children":[{"nodeNote":{"note":{"value":1,"alteration":0}}}]}');
    });

    it('property names are case-sensitive, unknown members are skipped, Name is required', () => {
        expect(deserialize('{"name":"b","extra":[1]}', NodeHtml.json, camel).name).toBe('b');
        expect(() => deserialize('{"Name":"b"}', NodeHtml.json, camel)).toThrow(JsonException);
    });

    it('enums and numbers only as JSON numbers', () => {
        expect(() => deserialize('{"value":1,"alteration":"Flat"}', Note.json, camel)).toThrow(JsonException);
        expect(() => deserialize('{"value":"1"}', Note.json, camel)).toThrow(JsonException);
        expect(() => deserialize('{"value":256}', Note.json, camel)).toThrow(JsonException);
        expect(() => deserialize('{"value":1.0}', Note.json, camel)).toThrow(JsonException);
    });

    it('search headers', () => {
        const json = '{"headers":[{"isSupported":true,"sourceUri":"u","type":"t","specificAttributes":{},"externalId":"e","source":"s","rating":null,"title":null,"artists":null}]}';
        const response = deserialize(json, SearchResponse.json, camel);
        expect(response.headers[0]!.isSupported).toBe(true);
        expect(response.headers[0]!.rating).toBeNull();
        expect(serialize(response, SearchResponse.json, camel)).toBe(json);
    });
});

describe('collection', () => {
    const json = '{"songs":[{"id":1,"index":5,"title":"T","templateRating":0.1,"artists":[2],"additionalKeywords":null,'
        + '"createdByUserId":123,"createdOn":"2023-01-02T03:04:05.123","versions":[{"id":10,"uri":"https://x/y","userId":123,"source":"s",'
        + '"externalId":"e","createdOn":"2023-01-02T03:04:05Z","comments":{"123":"c"},"collectiveId":"0f8fad5b-d9cb-469f-a165-70867728950e",'
        + '"collectiveSearchTag":null,"collectiveType":1}],"likes":[{"userId":123,"level":3,"createdOn":"2023-01-02T03:04:05",'
        + '"transpose":null,"versionId":10,"likeCategoryId":null}],"comments":{}}],'
        + '"users":[{"id":123,"title":"U"}],"artists":[{"id":2,"name":"A","synonyms":["B"]}],'
        + '"likeCategories":[{"id":1,"userId":123,"name":"N","cssColor":"red","order":0,"privateWeight":2,"cssIcon":"i","access":2}],"etag":9}';

    it('round-trips', () => {
        const collection = deserialize(json, Collection.json, camel);
        expect(collection.songs[0]!.versions[0]!.comments.get(123)).toBe('c');
        expect(collection.likeCategories[0]!.access).toBe(LikeCategoryAccess.SharedEditMulti);
        expect(serialize(collection, Collection.json, camel)).toBe(json);
    });

    it('DeepHash is equal for equal collections and changes with a change', () => {
        const a = deserialize(json, Collection.json, camel);
        const b = deserialize(json, Collection.json, camel);
        expect(a.deepHash()).toEqual(b.deepHash());
        const c = deserialize(json.replace('"title":"U"', '"title":"V"'), Collection.json, camel);
        expect(a.deepHash()).not.toEqual(c.deepHash());
    });
});

describe('dictionaries, dates, floats, bytes, guids', () => {
    it('Dictionary<int, string> keeps the JSON order (JSON.parse would not)', () => {
        const response = deserialize('{"illustrations":null,"titles":{"10":"a","2":"b"}}', IllustrationsResponse.json, camel);
        expect([...response.titles!.keys()]).toEqual([10, 2]);
        expect(serialize(response, IllustrationsResponse.json, camel)).toBe('{"illustrations":null,"titles":{"10":"a","2":"b"}}');
    });

    it('DateTime: kinds, trimmed fraction, comparison by ticks', () => {
        const utc = DateTime.parseJson('2024-05-06T07:08:09.1230000Z')!;
        expect(utc.kind).toBe(DateTimeKind.Utc);
        expect(utc.toJson()).toBe('2024-05-06T07:08:09.123Z');
        const unspecified = DateTime.parseJson('2024-05-06T07:08:09.123')!;
        expect(unspecified.kind).toBe(DateTimeKind.Unspecified);
        expect(unspecified.toJson()).toBe('2024-05-06T07:08:09.123');
        expect(unspecified.equals(utc)).toBe(true);
        expect(DateTime.parseJson('2024-05-06')!.toJson()).toBe('2024-05-06T00:00:00');
        expect(DateTime.parseJson('0001-01-01T00:00:00')!.ticks).toBe(0n);
        expect(DateTime.parseJson('2024-02-30T00:00:00')).toBeNull();
        const local = DateTime.parseJson('2024-05-06T07:08:09+03:00')!;
        expect(local.kind).toBe(DateTimeKind.Local);
        expect(local.toUniversalTime().toJson()).toBe('2024-05-06T04:08:09Z');
        expect(DateTime.parseJson(local.toJson())!.equals(local)).toBe(true);
    });

    it('float members as System.Single writes them', () => {
        const pdfsRequestJson = (weight: string) => `{"id":1,"userId":1,"name":"n","cssColor":"c","order":0,"privateWeight":${weight},"cssIcon":"i","access":0}`;
        const collection = (weight: string) => `{"songs":[],"users":[],"artists":[],"likeCategories":[${pdfsRequestJson(weight)}],"etag":null}`;
        expect(serialize(deserialize(collection('0.30000001192092896'), Collection.json, camel), Collection.json, camel)).toBe(collection('0.3'));
        expect(serialize(deserialize(collection('2.0'), Collection.json, camel), Collection.json, camel)).toBe(collection('2'));
        expect(serialize(deserialize(collection('1.5E2'), Collection.json, camel), Collection.json, camel)).toBe(collection('150'));
    });

    it('byte[] as base64', () => {
        const json = '{"pdfs":{"a":{"pdfData":"AAEC/w==","pageCount":2,"previewLink":null}},"volume":null}';
        const response = deserialize(json, GetPdfsResponse.json, camel);
        expect([...response.pdfs!.get('a')!.pdfData!]).toEqual([0, 1, 2, 255]);
        expect(serialize(response, GetPdfsResponse.json, camel)).toBe(json);
    });

    it('Guid in any case, written lowercase; enum as number', () => {
        const json = '{"collectiveId":"0F8FAD5B-D9CB-469F-A165-70867728950E","collective":{"authors":[],"title":"t","contents":"c","visibility":2,'
            + '"description":null,"tonality":null,"bpm":null,"versionComment":null},"versionNumber":1,"createdOn":"2024-01-01T00:00:00Z",'
            + '"searchTag":0,"createdByUserId":1,"updatedOn":"2024-01-01T00:00:00Z","uri":"https://x"}';
        const version = deserialize(json, CollectiveVersion.json, camel);
        expect(version.collective.visibility).toBe(CollectiveVisibility.Public);
        expect(serialize(version, CollectiveVersion.json, camel)).toBe(json.replace('0F8FAD5B-D9CB-469F-A165-70867728950E', '0f8fad5b-d9cb-469f-a165-70867728950e'));
    });

    it('malformed JSON is a JsonException', () => {
        expect(() => deserialize('{"titles":{},}', IllustrationsResponse.json, camel)).toThrow(JsonException);
        expect(() => deserialize('', IllustrationsResponse.json, camel)).toThrow(JsonException);
    });
});
