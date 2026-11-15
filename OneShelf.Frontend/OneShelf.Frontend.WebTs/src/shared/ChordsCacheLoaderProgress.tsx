// The original: Shared/ChordsCacheLoaderProgress.razor.

import { useRef } from 'react';
import { useEvent } from '../hooks/useEvent';
import { useStateHasChanged } from '../hooks/useStateHasChanged';
import { services } from '../services/registry';

export function ChordsCacheLoaderProgress() {
    const chordsCacheLoader = services.chordsCacheLoader;
    const progressionsCacheLoader = services.progressionsCacheLoader;
    const stateHasChanged = useStateHasChanged();

    const fields = useRef({
        progress: null as number | null,
        failedAt: null as number | null,
    }).current;

    const onProgressChanged = (progress: number | null): void => {
        // `_progress < 89` with an int?: false when _progress is null.
        if (progress === null && fields.progress !== null && fields.progress < 89) {
            fields.failedAt = fields.progress;
        } else {
            fields.failedAt = null;
        }

        fields.progress = progress;
        stateHasChanged();
    };

    // OnInitializedAsync: ChordsCacheLoader.ProgressChanged += OnProgressChanged; ProgressionsCacheLoader.ProgressChanged += OnProgressChanged;
    useEvent(chordsCacheLoader.progressChanged, onProgressChanged);
    useEvent(progressionsCacheLoader.progressChanged, onProgressChanged);

    if (fields.progress !== null) {
        return <a href="/cache"><small className="text-success">{fields.progress}<span>%…</span></small></a>;
    } else if (fields.failedAt !== null) {
        return <a href="/cache"><small className="text-warning">{fields.failedAt}<span>%💀</span></small></a>;
    }

    return null;
}
