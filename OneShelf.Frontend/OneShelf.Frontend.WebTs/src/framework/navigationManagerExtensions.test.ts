import { beforeAll, describe, expect, it, vi } from 'vitest';

// The module reads the browser's address when it loads.
let extensions: typeof import('./navigationManagerExtensions');

beforeAll(async () => {
    vi.stubGlobal('location', { href: 'http://localhost/collection' });
    extensions = await import('./navigationManagerExtensions');
});

describe('GetUriWithQueryParameters', () => {
    it('replaces in place (any case of the name), removes nulls, appends the new ones in order, keeps the hash', () => {
        const uri = 'http://localhost/collection?query=abc&IsOpen=False&SongId=5&other=%20x&isopen=True#frag';
        expect(extensions.getUriWithQueryParametersOf(uri, [
            ['IsOpen', true],
            ['SongId', null],
            ['Query', 'a b+c&d\'(x)*'],
            ['ExternalId', 'ex-1'],
            ['VersionId', 17],
        ])).toBe('http://localhost/collection?Query=a%20b%2Bc%26d%27%28x%29%2A&IsOpen=True&other=%20x&ExternalId=ex-1&VersionId=17#frag');
    });

    it('without a query: the non-null ones appended', () => {
        expect(extensions.getUriWithQueryParametersOf('http://localhost/collection', [['IsOpen2', false], ['SongId', null], ['ModeLocal', 'Title']]))
            .toBe('http://localhost/collection?IsOpen2=False&ModeLocal=Title');
    });

    it('a query left empty disappears', () => {
        expect(extensions.getUriWithQueryParametersOf('http://localhost/collection?SongId=1', [['SongId', null]])).toBe('http://localhost/collection');
    });

    it('Uri.EscapeDataString', () => {
        expect(extensions.escapeDataString('Кино ~-_.!')).toBe('%D0%9A%D0%B8%D0%BD%D0%BE%20~-_.%21');
    });
});
