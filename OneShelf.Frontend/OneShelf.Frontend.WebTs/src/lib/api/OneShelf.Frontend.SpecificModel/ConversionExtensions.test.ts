import { describe, expect, it } from 'vitest';
import { deserialize, jsonDictionary, jsonObjectAny, serialize } from '../../common/dotnet/json/JsonSerializer';
import { toDictionary, toFrontendAttributesV1 } from './ConversionExtensions';
import { FrontendAttributesV1 } from './FrontendAttributesV1';
import { SourceColor } from './SourceColor';

const dictionaryContract = jsonDictionary<string, unknown>('string', jsonObjectAny);

// OneShelf.Frontend.Web.Tests.SpecificAttributesSerializationTests
describe('SpecificAttributesSerializationTests', () => {
    it('Test1', () => {
        test(new FrontendAttributesV1({
            badgeText: 'a',
            sourceColor: SourceColor.Primary,
        }));
        test(new FrontendAttributesV1({
            badgeText: null,
            sourceColor: SourceColor.Primary,
        }));

        function test(a: FrontendAttributesV1) {
            let dictionary = new Map<string, unknown>();
            toDictionary(a, dictionary);
            const serialized = serialize(dictionary, dictionaryContract);
            dictionary = deserialize(serialized, dictionaryContract);
            const b = toFrontendAttributesV1(dictionary);

            expect(b).toEqual(a);
        }
    });
});

describe('specific attributes on the wire', () => {
    it('keeps the C# names as dictionary keys and the enum as its name', () => {
        const dictionary = new Map<string, unknown>();
        toDictionary(new FrontendAttributesV1({ badgeText: 'a', sourceColor: SourceColor.Warning }), dictionary);
        expect(serialize(dictionary, dictionaryContract)).toBe('{"SourceColor":"Warning","BadgeText":"a"}');
    });

    it('reads them from a header as the server sends it', () => {
        const attributes = deserialize('{"SourceColor":"Success","BadgeText":null,"Other":[1,{"x":2}]}', dictionaryContract);
        expect(toFrontendAttributesV1(attributes)).toEqual(new FrontendAttributesV1({ badgeText: null, sourceColor: SourceColor.Success }));
    });
});
