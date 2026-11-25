// Illustrations of chords: common to everyone the chords are open to, drawn on the server by a preset, a batch at a
// time, as a job (one at a time for the chords). Ordering needs the permission illustrations.generate.

import { queryOptions, useInfiniteQuery, useQueries, useQuery } from '@tanstack/react-query';
import { api, call } from '../api/client';
import { i18n } from '../i18n';
import { action } from './actions';
import { useSession } from './session';

export const drawPermission = 'illustrations.generate';

const drawnQuery = (chordsId: string | null) => queryOptions({
    queryKey: ['illustrations', chordsId],
    queryFn: () => call(api.GET('/api/illustrations/{chordsId}', { params: { path: { chordsId: chordsId! } } })),
    enabled: !!chordsId,
    staleTime: 5 * 60_000,
    refetchInterval: query => (query.state.data?.drawing ? 4000 : false),
});

/** The illustrations of chords, read again while a batch is drawn for them. */
export function useDrawnIllustrations(chordsId: string | null) {
    return useQuery(drawnQuery(chordsId));
}

/**
 * A song's illustrations: those of all its variants — they belong to the song page, whichever variant is open. The
 * open variant's come first when it has some.
 */
export function useSongIllustrations(chordsIds: readonly string[], open: string | null) {
    const answers = useQueries({ queries: chordsIds.map(id => drawnQuery(id)) });
    const each = chordsIds.map((id, i) => ({ id, data: answers[i]?.data }));
    const ordered = [...each.filter(x => x.id === open), ...each.filter(x => x.id !== open)];
    return {
        images: ordered.flatMap(x => x.data?.images ?? []),
        drawing: each.some(x => !!x.data?.drawing),
    };
}

export function usePresets() {
    const { session } = useSession();
    return useQuery({
        queryKey: ['illustration-presets'],
        queryFn: () => call(api.GET('/api/illustrations/presets')),
        enabled: !!session?.permissions.includes(drawPermission),
        staleTime: Infinity,
    });
}

/**
 * A preset's name in words. A preset is the server's key ("plain", "drama-2023.12.19": a mood and its version) and
 * comes with no words of its own: the mood is worded by the app; a mood it has no words for is "another mood", never
 * its key.
 */
export function presetName(preset: string | null | undefined): string {
    if (!preset) return '';
    const key = `ills.preset.${preset.split('-')[0]!}`;
    return i18n.exists(key) ? i18n.t(key as 'ills.preset.plain') : i18n.t('ills.presetOther');
}

export const drawIllustrations = action('drawIllustrations', 'change',
    ({ chordsId, preset }: { chordsId: string; preset: string }) =>
        call(api.POST('/api/illustrations/{chordsId}', { params: { path: { chordsId } }, body: { preset } })),
    { done: () => i18n.t('ills.ordered'), invalidate: (_, { chordsId }) => [['illustrations', chordsId]] });

const illustratedPage = 60;

/** The chords of my library that have illustrations, a page at a time (more as asked). */
export function useIllustratedLibrary() {
    const { signedIn } = useSession();
    return useInfiniteQuery({
        queryKey: ['illustrated'],
        queryFn: ({ pageParam }) => call(api.GET('/api/library/illustrated', { params: { query: { offset: pageParam, limit: illustratedPage } } })),
        initialPageParam: 0,
        getNextPageParam: last => (last.offset + last.items.length < last.total ? last.offset + last.items.length : undefined),
        enabled: signedIn,
    });
}
