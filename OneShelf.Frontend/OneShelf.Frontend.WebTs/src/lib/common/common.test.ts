import { describe, expect, it } from 'vitest';
import { enumParse } from './dotnet/Enum';
import { Dictionary } from './dotnet/Collections';
import { ArgumentException, ArgumentOutOfRangeException, EndOfStreamException, IOException } from './dotnet/exceptions';
import { htmlAttributeEncode, htmlEncode } from './dotnet/html';
import { writeJsonDouble, writeJsonSingle } from './dotnet/json/JsonWriter';
import { isNullOrWhiteSpace, split, StringSplitOptions, toLowerInvariant, trim, tryParseInt32 } from './dotnet/text';
import { getFragments } from './HarmonyDB.Common.FullTextSearch/SearchHighlightingTools';
import { transposition } from './HarmonyDB.Common/Transposition/TranspositionExtensions';
import { addRange, anyDuplicates, asPairs, toChunks, toChunksByShouldStartNew, withIsFirstLast, withPrevious } from './OneShelf.Common/Extensions';
import { buildCache, find } from './OneShelf.Common.Songs/FullTextSearch/FullTextSearchLogic';
import { calculateHash } from './OneShelf.Common.Songs/Hashing/MyHashExtensions';
import { getRating } from './OneShelf.Common.Songs/SongsConstants';

describe('.NET semantics', () => {
    it('white space is char.IsWhiteSpace', () => {
        expect(trim('\u0085 a ﻿')).toBe('a ﻿'.replace(' ', '').length === 2 ? 'a ﻿' : '');
        expect(isNullOrWhiteSpace(' \t')).toBe(true);
        expect(isNullOrWhiteSpace('﻿')).toBe(false);
    });

    it('Split with options', () => {
        expect(split(' a, ,b ', [','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).toEqual(['a', 'b']);
        expect(split('a,,b', [','], StringSplitOptions.RemoveEmptyEntries)).toEqual(['a', 'b']);
        expect(split(',a', [','])).toEqual(['', 'a']);
    });

    it('ToLowerInvariant keeps one char per char', () => {
        expect(toLowerInvariant('ÀЁİ')).toBe('àёİ');
    });

    it('int.TryParse', () => {
        expect(tryParseInt32(' +12 ')).toBe(12);
        expect(tryParseInt32('-0')).toBe(0);
        expect(tryParseInt32('2147483648')).toBeNull();
        expect(tryParseInt32('1.0')).toBeNull();
        expect(tryParseInt32('١')).toBeNull();
    });

    it('HttpUtility encoders', () => {
        expect(htmlEncode('é<\'"&>😀ф')).toBe('&#233;&lt;&#39;&quot;&amp;&gt;&#128512;ф');
        expect(htmlAttributeEncode('é<\'"&>')).toBe('é&lt;&#39;&quot;&amp;>');
        expect(htmlAttributeEncode(null)).toBeNull();
    });

    it('numbers as .NET writes them', () => {
        expect(writeJsonDouble(1e-5)).toBe('1E-05');
        expect(writeJsonDouble(1e15)).toBe('1000000000000000'); // Utf8JsonWriter, unlike double.ToString(), keeps 17 digits before the exponent form
        expect(writeJsonDouble(1e17)).toBe('1E+17');
        expect(writeJsonDouble(123456789012345)).toBe('123456789012345');
        expect(writeJsonDouble(-0.5)).toBe('-0.5');
        expect(writeJsonSingle(0.1)).toBe('0.1');
        expect(writeJsonSingle(1.4)).toBe('1.4');
        expect(writeJsonSingle(16777217)).toBe('16777216');
    });

    it('Enum.Parse', () => {
        enum E { A = 0, B = 1, C = 2 }
        expect(enumParse(E, ' B ')).toBe(E.B);
        expect(enumParse(E, 'A, C')).toBe(2);
        expect(enumParse(E, '7')).toBe(7);
        expect(() => enumParse(E, 'b')).toThrow(ArgumentException);
    });

    it('exceptions keep .NET hierarchy', () => {
        expect(new ArgumentOutOfRangeException('x')).toBeInstanceOf(ArgumentException);
        expect(new ArgumentOutOfRangeException('x').message).toBe("Specified argument was out of the range of valid values. (Parameter 'x')");
        expect(new EndOfStreamException()).toBeInstanceOf(IOException);
    });
});

describe('OneShelf.Common.Extensions', () => {
    it('ToChunks', () => {
        expect(toChunks([1, 1, 2, 2, 1], x => x)).toEqual([
            { chunk: [1, 1], criterium: 1 },
            { chunk: [2, 2], criterium: 2 },
            { chunk: [1], criterium: 1 },
        ]);
        expect(toChunks([null, null, 'a'], x => x)).toEqual([{ chunk: [null, null], criterium: null }, { chunk: ['a'], criterium: 'a' }]);
    });

    it('ToChunksByShouldStartNew', () => {
        expect(toChunksByShouldStartNew([1, 2, 4, 5, 7], x => x === 4 || x === 7)).toEqual([[1, 2], [4, 5], [7]]);
        expect(toChunksByShouldStartNew([1, 2], () => true)).toEqual([[1], [2]]);
    });

    it('AnyDuplicates', () => {
        const out: { firstDuplicate?: number | null } = {};
        expect(anyDuplicates([3, 1, 2, 1, 3], undefined, out)).toBe(true);
        expect(out.firstDuplicate).toBe(3);
        expect(anyDuplicates([1, 2, 3])).toBe(false);
    });

    // GroupBy and Equals use the default equality: value tuples (here objects) are equal by their elements.
    it('AnyDuplicates and ToChunks compare value tuples by value', () => {
        const out: { firstDuplicate?: { a: number; b: string } | null } = {};
        expect(anyDuplicates([{ a: 1, b: 'x' }, { a: 2, b: 'x' }, { a: 1, b: 'x' }], undefined, out)).toBe(true);
        expect(out.firstDuplicate).toEqual({ a: 1, b: 'x' });
        expect(anyDuplicates([[1], [1]])).toBe(false); // arrays (lists) are references
        expect(toChunks([1, 2, 3, 4], x => ({ odd: x % 2 === 1, small: x < 3 })).map(x => x.chunk)).toEqual([[1], [2], [3], [4]]);
        expect(toChunks([1, 3, 2], x => ({ odd: x % 2 === 1 })).map(x => x.chunk)).toEqual([[1, 3], [2]]);
    });

    it('AddRange to a Dictionary with value tuple keys', () => {
        const dictionary = new Dictionary<{ x: number; i: number }, string>();
        addRange(dictionary, [{ key: { x: 1, i: 2 }, value: 'a' }], false);
        expect(dictionary.get({ x: 1, i: 2 })).toBe('a');
        expect(() => addRange(dictionary, [{ key: { x: 1, i: 2 }, value: 'b' }], false)).toThrow(ArgumentException);
        addRange(dictionary, [{ key: { x: 1, i: 2 }, value: 'b' }], true);
        expect(dictionary.get({ x: 1, i: 2 })).toBe('b');
    });

    it('WithPrevious, AsPairs, WithIsFirstLast', () => {
        expect(withPrevious(['a', 'b'])).toEqual([{ previous: null, current: 'a' }, { previous: 'a', current: 'b' }]);
        expect(asPairs([1, 2, 3])).toEqual([{ previous: 1, current: 2 }, { previous: 2, current: 3 }]);
        expect(withIsFirstLast(['a', 'b'])).toEqual([{ x: 'a', isFirst: true, isLast: false }, { x: 'b', isFirst: false, isLast: true }]);
    });
});

describe('HarmonyDB.Common', () => {
    it('Transposition', () => {
        expect(transposition(3)).toBe('+3');
        expect(transposition(0)).toBe('0');
        expect(transposition(-3)).toBe('−3');
    });
});

describe('search', () => {
    const like = (userId: number, level: number) => ({ userId, level });
    const song = (index: number, title: string, likes: { userId: number; level: number }[] = [], additionalKeywords: string | null = null) =>
        ({ index, title, likes, additionalKeywords });
    const songs = [
        song(5, 'Группа крови', [like(1, 2), like(2, 3)]),
        song(6, 'Кровь', [like(1, 3)]),
        song(7, 'Звезда по имени Солнце', [], 'солнце ёлка'),
    ];
    const cache = buildCache(songs, x => x.index === 7 ? [] : [{ name: 'Кино', synonyms: ['Kino'] }]);

    it('BuildCache: artists, synonyms, title and keywords as search words', () => {
        expect([...cache[0]!.words]).toEqual(['кино', 'kino', 'группа', 'крови']);
        expect([...cache[2]!.words]).toEqual(['звезда', 'по', 'имени', 'солнце', 'елка']);
    });

    it('Find: whole words first, then beginnings, then by users, then by index', () => {
        expect(find(cache, 'кров', 1).found.map(x => x.index)).toEqual([5, 6]);
        expect(find(cache, 'КИНО', 1).found.map(x => x.index)).toEqual([5, 6]);
        expect(find(cache, 'ЁЛКА', 1).found.map(x => x.index)).toEqual([7]);
        expect(find(cache, 'kino zzz', 1).found).toEqual([]);
    });

    it('Find: a number finds the song with that index first', () => {
        expect(find(cache, ' 7 ', 1)).toEqual({ found: [songs[2]], isUserSpecific: false });
    });

    it('Find: no terms — the user\'s own likes first, then the rating, then the index', () => {
        const result = find(cache, ' - ', 1);
        expect(result.isUserSpecific).toBe(true);
        expect(result.found.map(x => x.index)).toEqual([6, 5, 7]);
        expect(find(cache, '', 3).found.map(x => x.index)).toEqual([5, 6, 7]);
    });

    it('GetRating', () => {
        expect(getRating([like(1, 1), like(1, 3), like(2, 2), like(3, 0)])).toBe(1.8 + 1.4);
    });

    it('GetFragments', () => {
        expect(getFragments('кров', 'Группа крови')).toEqual([
            { fragment: 'Группа ', isHighlighted: false },
            { fragment: 'кров', isHighlighted: true },
            { fragment: 'и', isHighlighted: false },
        ]);
        expect(getFragments('rockn', 'Rock\'n\'roll')).toEqual([
            { fragment: 'Rock\'n', isHighlighted: true },
            { fragment: 'roll', isHighlighted: false },
        ]);
        expect(getFragments(null, 'Text')).toEqual([{ fragment: 'Text', isHighlighted: false }]);
        expect(getFragments('x', ' ')).toEqual([]);
    });

    it('CalculateHash wraps as int32', () => {
        expect(calculateHash([])).toBe(269);
        expect(calculateHash([1, 2])).toBe((269 * 47 + 1) * 47 + 2);
        expect(calculateHash([2147483647])).toBe(-2147471006);
    });
});
