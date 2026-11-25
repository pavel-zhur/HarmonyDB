// The kinds of marks, as the server gives them (its public configuration): the key that is stored and sent, the sign
// that is drawn, in the order they are offered. What a mark means, people agree among themselves.

import { useMemo } from 'react';
import type { Schemas } from '../api/client';
import { useConfiguration } from './session';

export type MarkKind = Schemas['MarkKind'];

export function useMarkKinds(): MarkKind[] {
    const { data } = useConfiguration();
    return data?.markKinds ?? [];
}

/** The sign of a mark's kind (a kind not known yet: nothing). */
export function useMarkSign(): (kind: string) => string {
    const kinds = useMarkKinds();
    return useMemo(() => {
        const signs = new Map(kinds.map(k => [k.key, k.sign]));
        return (kind: string) => signs.get(kind) ?? '';
    }, [kinds]);
}
