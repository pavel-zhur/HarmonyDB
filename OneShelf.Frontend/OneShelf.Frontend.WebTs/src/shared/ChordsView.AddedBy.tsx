// The original: Shared/ChordsView.AddedBy.razor (component ChordsView_AddedBy).

import { getRequired } from '../dotnet/collections';
import { useCollectionComponent } from '../hooks/useCollectionComponent';

export interface ChordsView_AddedByProps {
    versionId?: number | null;
}

export function ChordsView_AddedBy({ versionId = null }: ChordsView_AddedByProps) {
    const { collectionIndex } = useCollectionComponent();

    const version = versionId !== null ? collectionIndex?.versionsById.get(versionId) : undefined;
    if (version === undefined) return null;

    return (
        <div>Добавлены: {getRequired(collectionIndex!.usersById, version.version.userId).title}</div>
    );
}
