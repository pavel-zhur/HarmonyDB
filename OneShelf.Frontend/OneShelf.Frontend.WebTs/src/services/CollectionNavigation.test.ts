import { beforeAll, describe, expect, it, vi } from 'vitest';
import { InvalidOperationException } from '../lib/common/dotnet/exceptions';

// The modules read the browser's address when they load.
let CollectionNavigation: typeof import('./CollectionNavigation').CollectionNavigation;

beforeAll(async () => {
    vi.stubGlobal('location', { href: 'http://localhost/collection' });
    vi.stubGlobal('document', { baseURI: 'http://localhost/' });
    ({ CollectionNavigation } = await import('./CollectionNavigation'));
});

describe('SearchResultsLocalLink', () => {
    const navigation = () => new CollectionNavigation({} as never, { requiredIdentity: { id: 7 } } as never);
    const version = (id: number, collectiveId: number | null, userId: number) => ({ id, collectiveId, userId, externalId: `e${id}`, createdOn: { compareTo: () => 0 } });
    const like = (versionId: number, level: number) => ({ versionId, userId: 7, likeCategoryId: null, level });

    it('picks the version by the ordering', () => {
        const song = { id: 1, versions: [version(10, null, 7), version(11, 3, 7)], likes: [like(10, 3)] };
        expect(navigation().searchResultsLocalLink(song as never)).toBe('http://localhost/collection?SongId=1&IsOpen=True&IsOpen2=True&ExternalId=e11&VersionId=11');
    });

    // In the browser .NET's LINQ sorts the whole sequence for First(), every key of every version computed: a losing
    // version with two uncategorised likes of the user fails the ordering (established with a Blazor WebAssembly 9.0
    // application; the server runtime would not compute that key and would return the link).
    it('fails when any version has two uncategorised likes of the user, as the original in the browser', () => {
        const song = { id: 1, versions: [version(10, null, 7), version(11, 3, 7)], likes: [like(10, 3), like(10, 4)] };
        expect(() => navigation().searchResultsLocalLink(song as never)).toThrow(InvalidOperationException);
    });
});
