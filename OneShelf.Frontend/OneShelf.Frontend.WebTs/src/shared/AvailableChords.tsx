// The original: Shared/AvailableChords.razor.

import type { SearchHeader } from '../lib/api/HarmonyDB.Source.Api.Model/V1/SearchHeader';
import { toFrontendAttributesV1 } from '../lib/api/OneShelf.Frontend.SpecificModel/ConversionExtensions';
import { SourceColor } from '../lib/api/OneShelf.Frontend.SpecificModel/SourceColor';
import { ArgumentOutOfRangeException } from '../lib/common/dotnet/exceptions';
import { groupBy } from '../lib/common/dotnet/Linq';

export interface AvailableChordsProps {
    headers: SearchHeader[];
}

function badgeClass(sourceColor: SourceColor): string {
    switch (sourceColor) {
        case SourceColor.Success: return 'bg-success';
        case SourceColor.Primary: return 'bg-primary';
        case SourceColor.Warning: return 'bg-warning';
        default: throw new ArgumentOutOfRangeException();
    }
}

export function AvailableChords({ headers }: AvailableChordsProps) {
    // Headers.GroupBy(x => (x.Source, x.SpecificAttributes.ToFrontendAttributesV1().SourceColor)).WithIndices()
    const groups = groupBy(headers, x => ({ source: x.source, sourceColor: toFrontendAttributesV1(x.specificAttributes).sourceColor }));

    return (
        <>
            {groups.map((group, i) => (
                <span key={i} className={`badge ${badgeClass(group.key.sourceColor)} ms-2 opacity-50`}>
                    {group.key.source}
                    {group.items.length > 1 && <span>{' ×'}{group.items.length}</span>}
                </span>
            ))}
        </>
    );
}
