// StateHasChanged(): a function that makes the component render again. Stable for the component's life; after the
// component is gone it does nothing (as StateHasChanged on a disposed component).

import { useCallback, useReducer } from 'react';

export function useStateHasChanged(): () => void {
    const [, forceRender] = useReducer((x: number) => x + 1, 0);
    return useCallback(() => forceRender(), []);
}
