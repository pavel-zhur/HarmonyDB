// The original: Shared/CssNthOfType.razor.
//
// Called through @ref: <CssNthOfType ref={cssNthOfType} />, then cssNthOfType.current?.updated(songId).

import { forwardRef, useImperativeHandle, useRef } from 'react';
import { useStateHasChanged } from '../hooks/useStateHasChanged';

/** What the component's @ref gives. */
export interface CssNthOfTypeRef {
    updated(songId: number | null): void;
}

export const CssNthOfType = forwardRef<CssNthOfTypeRef>(function CssNthOfType(_props, ref) {
    const stateHasChanged = useStateHasChanged();
    const fields = useRef({ songId: null as number | null }).current;

    useImperativeHandle(ref, () => ({
        updated(songId: number | null): void {
            if (fields.songId === songId) return;
            fields.songId = songId;
            stateHasChanged();
        },
    }), [fields, stateHasChanged]);

    if (fields.songId === null) return null;

    return (
        <style>
            {`
        table.songs.table-one-column.local tr[songid="${fields.songId}"] {
            background-color: #ebf1fb !important;
        }
    `}
        </style>
    );
});
