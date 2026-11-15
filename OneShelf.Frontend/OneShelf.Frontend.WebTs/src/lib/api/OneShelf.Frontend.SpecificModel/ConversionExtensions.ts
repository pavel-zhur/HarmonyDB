// The frontend's view of a header's SpecificAttributes (IReadOnlyDictionary<string, object?>). After
// deserialization the values are JsonElement; the keys are the C# property names (dictionary keys are never renamed).

import { enumParse, enumToString } from '../../common/dotnet/Enum';
import { InvalidCastException, KeyNotFoundException } from '../../common/dotnet/exceptions';
import { JsonElement } from '../../common/dotnet/json/JsonNode';
import { FrontendAttributesV1 } from './FrontendAttributesV1';
import { SourceColor } from './SourceColor';

/** (JsonElement?)value */
function asJsonElement(value: unknown): JsonElement | null {
    if (value == null) return null;
    if (!(value instanceof JsonElement)) throw new InvalidCastException();
    return value;
}

export function toFrontendAttributesV1(attributes: ReadonlyMap<string, unknown>): FrontendAttributesV1 {
    if (!attributes.has('SourceColor')) throw new KeyNotFoundException('SourceColor');
    const sourceColor = asJsonElement(attributes.get('SourceColor'));
    if (sourceColor == null) throw new InvalidCastException('Nullable object must have a value.');
    return new FrontendAttributesV1({
        badgeText: asJsonElement(attributes.get('BadgeText'))?.getString() ?? null,
        sourceColor: enumParse<SourceColor>(SourceColor, sourceColor.getString()!),
    });
}

export function toDictionary(attributes: FrontendAttributesV1, dictionary: Map<string, unknown>): void {
    dictionary.set('SourceColor', enumToString(SourceColor, attributes.sourceColor));
    if (attributes.badgeText != null) {
        dictionary.set('BadgeText', attributes.badgeText);
    }
}
