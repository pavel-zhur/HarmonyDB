// The person's background jobs (an import of links, the pages of a book, a book, illustrations): the list of one kind
// (queued, running, finished lately), followed while any is unfinished; and what each kind ends with (JobInfo.result,
// told apart by its `kind`: resultOf).

import { useQuery } from '@tanstack/react-query';
import { api, call, type Schemas } from '../api/client';
import { i18n } from '../i18n';
import { action } from './actions';
import { useSession } from './session';

export type JobInfo = Schemas['JobInfo'];
export type JobKind = 'import' | 'book_pages' | 'book' | 'illustrations';

export const unfinished = (job: JobInfo) => job.status === 'queued' || job.status === 'running';

export function useJobs(kind: JobKind) {
    const { signedIn } = useSession();
    return useQuery({
        queryKey: ['jobs', kind],
        queryFn: () => call(api.GET('/api/jobs', { params: { query: { kind } } })),
        enabled: signedIn,
        refetchInterval: query => (query.state.data?.some(unfinished) ? 1500 : false),
    });
}

/** The address of a job's file (a book's PDF): the browser downloads it with the session's cookie. */
export const jobFileUrl = (id: string) => `/api/jobs/${encodeURIComponent(id)}/file`;

/** A finished job's result, when it is of this kind. */
export function resultOf<K extends NonNullable<JobInfo['result']>['kind']>(job: JobInfo | undefined, kind: K): Extract<NonNullable<JobInfo['result']>, { kind: K }> | null {
    const result = job?.status === 'succeeded' ? job.result : null;
    return result?.kind === kind ? result as Extract<NonNullable<JobInfo['result']>, { kind: K }> : null;
}

export type ImportResult = Schemas['ImportResult'];
export type BookPagesResult = Schemas['BookPagesResult'];
export type BookResult = Schemas['BookResult'];

// ---- import of links ----------------------------------------------------------------------------------------

const t = i18n.t.bind(i18n);

export const importLinks = action('importLinks', 'import',
    (links: string[]) => call(api.POST('/api/import/links', { body: { links } })),
    { done: (_, links) => t('importing.queued', { count: links.length }), invalidate: () => [['jobs', 'import']] });

// ---- the book -------------------------------------------------------------------------------------------------

export function countBookPages(body: Schemas['BookRequest']) {
    return call(api.POST('/api/book/pages', { body }));
}

export function makeBook(body: Schemas['BookRequest']) {
    return call(api.POST('/api/book', { body }));
}
