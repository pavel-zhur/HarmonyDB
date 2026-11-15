// Seeded, deterministic cases for the comparison with .NET: broad and nasty inputs for every suite of dispatch.ts.
// The same seed gives the same cases.

import { generateUriCases } from './generateUri';

export interface Case {
    readonly id: string;
    readonly suite: string;
    readonly input: unknown;
}

/** mulberry32 */
export class Random {
    private state: number;

    constructor(seed: number) {
        this.state = seed >>> 0;
    }

    next(): number {
        let t = (this.state = (this.state + 0x6d2b79f5) >>> 0);
        t = Math.imul(t ^ (t >>> 15), t | 1);
        t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
        return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
    }

    int(min: number, maxExclusive: number): number {
        return min + Math.floor(this.next() * (maxExclusive - min));
    }

    chance(p: number): boolean {
        return this.next() < p;
    }

    pick<T>(items: readonly T[]): T {
        return items[this.int(0, items.length)]!;
    }
}

const suffixes = [
    '', 'm', '5', '7', '9', '11', '13', '79', '913', 'add9', '2add9', 'madd9', 'm2add11', 'sus', 'sus2', 'sus4', '7sus4', '9sus', '7sus2',
    'm7', 'm9', 'm13', 'm79', 'maj', 'maj7', 'maj9', 'maj13', 'majx', 'mmaj', 'mmaj7', 'mmajor', 'm+7', 'm7+', '+7', '7+', '+', 'm+',
    'aug', 'dim', 'dim7', 'm7-5', 'm75-', 'm-5', 'm5-', 'm5', '7#5', '7+5', '75+', '755+', '#5', '6', 'm6', '69', 'm69', 'x5+',
    'xyz', 'm(add9)', '7b9', 'sus2sus4', 'M7', 'Maj7', 'mi', 'min', '°', 'ø', 'Δ', '\n', 'm\n', '\n\n', ' ', '\r', 'msus', 'm7sus4',
    '٣add٣', '𝟕', 'add', 'sus44', '7/9', '/', 'm/',
];

const alterations = ['', '#', 'b'];

function noteData(random: Random): string {
    const value = random.chance(0.85)
        ? String(random.int(0, 12))
        : random.pick(['12', '99', '255', '256', '999', '007', '٣', '𝟑', '', '1٣', '00000000000000000001']);
    const alteration = random.chance(0.97) ? random.pick(alterations) : random.pick(['##', 'x', '♯']);
    return `[!${value}${alteration}!]`;
}

/** Chord data as the node tree gives it (AsChords): "[!0!]m7/[!7!]". */
export function chordDataString(random: Random): string {
    if (random.chance(0.04)) return random.pick(['', '?', 'N.C.', 'Am', '[!1!', '[!!]', 'Кириллица', '\n', '[!1!]/', '/[!1!]', '[!1!]/[!2!]/[!3!]', '[!1#!][!2!]']);
    let result = noteData(random) + random.pick(suffixes);
    if (random.chance(0.25)) result += '/' + (random.chance(0.9) ? noteData(random) : random.pick(['X', '', '[!x!]', 'H']));
    return result;
}

const letters = ['A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'С', 'a', 'c', 'X'];
const humanAlterations = ['', '', '', '#', 'b', '♯', '♭', 'is', 'es'];
const humanSuffixes = ['', 'm', '7', 'm7', 'maj7', 'sus4', 'sus2', 'dim', 'aug', '+', '6', 'm6', '9', 'add9', '5', 'mmaj7', '7sus4', 'm7b5', '7/9', '(add9)', 'M', 'min', 'maj', '13', '11', '7#5', '°', '*', 'x2', '..'];

/** A chord as people write it: "C#m7/G#", "Hm", "Cb". */
export function humanChord(random: Random): string {
    if (random.chance(0.05)) return random.pick(['N.C.', '|', '-', '(Am)', '[Am]', 'Am Dm', 'x2', '', ' ', 'Am/', '/E', 'A/B/C', '%', 'Ам']);
    let result = random.pick(letters) + random.pick(humanAlterations) + random.pick(humanSuffixes);
    if (random.chance(0.2)) result += '/' + random.pick(letters) + random.pick(humanAlterations);
    return result;
}

const nastyStrings = [
    '', ' ', '  a  ', '\t', '\n', '\r\n', '\u00a0', '\u2028', '\u0085', '\ufeff', '\u200b', '\u3000', 'a\u0000b', 'ё', 'Ё', 'Ёлка', 'ЁЖ',
    'İ', 'ı', 'ß', 'ǅ', 'ǆ', 'Ω', 'µ', 'ﬃ', 'ſ', 'K', 'Å', 'Σ', 'ς', 'ΐ', 'ŉ', '😀', '𐐀', '𐐨', 'Ǉ', 'ǈ', 'ǉ',
    '<b>', '&amp;', '"\'', '`+', '\\', '/', 'é', 'ÿ', '\u00ad', '©', '¼', '١٢٣', '١', '٠٠١', '𝟏𝟐', '+5', '-5', ' -0 ', '+-1', '2147483647',
    '2147483648', '-2147483648', '-2147483649', '00012', '1 2', '12\u0000', '\u00a012', '1e3', '0x10', '١٢', '−5', '255', '256', '0', '1',
    'Flat', 'Sharp', 'flat', ' Flat ', 'Flat,Sharp', '0', '1', '2', '-1', '5', 'Flat, Sharp', 'AQID', 'AQI=', 'AQ==', 'AQ', 'A Q I D', 'AQID\n', '====',
    'AQ=D', '////', '+/+/', '-_-_', 'AAEC/w==', '0f8fad5b-d9cb-469f-a165-70867728950e', '0F8FAD5B-D9CB-469F-A165-70867728950E',
    '{0f8fad5b-d9cb-469f-a165-70867728950e}', '0f8fad5bd9cb469fa16570867728950e', '2024-05-06T07:08:09', '2024-05-06T07:08:09.1234567Z',
    '2024-05-06T07:08:09.12345678Z', '2024-05-06T07:08:09+03:00', '2024-05-06T07:08:09-00:30', '2024-05-06', '2024-05-06T07:08',
    '2024-05-06T07:08:09.5+14:00', '2024-05-06T07:08:09+15:00', '2024-02-30T00:00:00', '2024-02-29T23:59:59.9999999', '0001-01-01T00:00:00',
    '9999-12-31T23:59:59.9999999', '9999-12-31T23:59:59.9999999-01:00', '0001-01-01T00:00:00+01:00', '2024-05-06 07:08:09', '2024-05-06t07:08:09z',
    '2024-03-31T02:30:00', '2024-10-27T02:30:00', '9999-12-31T23:30:00+00:00', '0001-01-01T00:30:00-00:00', '2024-10-27T00:30:00Z', '2024-10-27T02:30:00+02:00', '2024-10-27T02:30:00+01:00', '1890-06-01T12:00:00', '1950-06-01T12:00:00Z', '2024-03-31T02:30:00Z', '2024-10-27T01:30:00Z', '2024-05-06T24:00:00', '2024-5-6',
    'abc', 'ABC', 'aBC', 'ABc', 'URLValue', 'IOStream', 'A B', 'AB CD', 'A', 'a', 'ÉCOLE', 'Ab1', 'HTMLElement', 'X1Y',
    'кино', 'КИНО', "rock'n'roll", 'Rock-n-roll', 'a,b', "it's", "''", "a'b'c",
];

function nastyString(random: Random, maxParts = 4): string {
    let result = '';
    const parts = random.int(1, maxParts + 1);
    for (let i = 0; i < parts; i++) {
        result += random.chance(0.6) ? random.pick(nastyStrings) : String.fromCharCode(random.int(0x20, 0x7f));
    }

    return result;
}

function settings(random: Random): unknown {
    const result: Record<string, unknown> = {
        t: random.chance(0.4) ? 0 : random.int(-15, 16),
        a: random.chance(0.6) ? null : random.chance(0.97) ? random.int(0, 2) : 2,
        x: random.chance(0.05),
        vw: random.chance(0.3),
        s: random.chance(0.5) ? 0 : random.chance(0.8) ? random.pick([1, 3, 4, 8, 16, 31, 5, 12, 19]) : random.int(0, 32),
    };
    if (random.chance(0.3)) {
        const ca: [number, string][] = [];
        const used = new Set<number>();
        for (let i = random.int(0, 5); i > 0; i--) {
            const key = random.int(-1, 30);
            if (used.has(key)) continue;
            used.add(key);
            ca.push([key, random.pick(['search', 'search-first', '', '<"&\'>', 'кино', '😀'])]);
        }
        result.ca = ca;
    }
    if (random.chance(0.15)) result.rel = [random.int(0, 12), random.chance(0.5)];
    if (random.chance(0.2)) result.ops = Array.from({ length: random.int(1, 4) }, () => random.pick(['u', 'd'])).join('');
    return result;
}

// Node trees (the JSON the site stores: C# names).

function text(random: Random): string {
    return random.pick([
        'Ой, то не вечер, то не вечер,', '\n', 'Мне малым-мало спалось', ' ', '  ', '\n\n', '\r\n', 'la la', '<i>&amp;</i>', 'it\'s',
        'Ёжик в тумане', '\t', 'Припев:', 'x2', '😀', 'a\u00a0b', '|', ' - ', '(', ')', 'Am', 'C/G', '\u2028', '[', ']', '>', '...',
    ]) + (random.chance(0.2) ? nastyString(random, 2) : '');
}

function nodeText(value: string): unknown {
    return { NodeText: { Text: value } };
}

function chordNode(random: Random): unknown {
    if (random.chance(0.1)) {
        // A note node inside a chord, as Fix1_ExtractChordNotes makes them.
        const children: unknown[] = [{ NodeNote: { Note: { Value: random.int(0, 12), Alteration: random.pick([null, 0, 1]) } } }, nodeText(random.pick(['', 'm', '7', 'm7', 'sus4']))];
        if (random.chance(0.3)) children.push(nodeText('/'), { NodeNote: { Note: { Value: random.int(0, 12), Alteration: null } } });
        return { NodeChord: { Children: children } };
    }

    const children: unknown[] = [nodeText(humanChord(random))];
    if (random.chance(0.05)) children.push(nodeText(humanChord(random)));
    return { NodeChord: { Children: children } };
}

function lineContent(random: Random, depth: number): unknown[] {
    const result: unknown[] = [];
    const count = random.int(0, 9);
    for (let i = 0; i < count; i++) {
        const r = random.next();
        if (r < 0.45) result.push(chordNode(random));
        else if (r < 0.85) result.push(nodeText(text(random)));
        else if (r < 0.92 && depth < 3) result.push({ NodeHtml: { Name: random.pick(['b', 'i', 'span', 'div', 'sup']), Classes: random.pick([null, 'bold-div', '', 'x y']), Children: lineContent(random, depth + 1) } });
        else if (r < 0.95 && depth < 3) result.push({ NodeLine: { Children: lineContent(random, depth + 1) } });
        else result.push({ NodeNote: { Note: { Value: random.int(0, 12), Alteration: random.pick([null, 0, 1]) } } });
    }

    return result;
}

/** A song: lines of chords and words, sometimes repeated, as NodeHtml JSON. */
export function songJson(random: Random): string {
    const children: unknown[] = [];
    const lines: unknown[][] = [];
    const lineCount = random.int(0, 14);
    for (let i = 0; i < lineCount; i++) {
        let line: unknown[];
        if (lines.length > 0 && random.chance(0.35)) {
            line = random.pick(lines);
        } else if (random.chance(0.5)) {
            line = [];
            for (let j = random.int(1, 6); j > 0; j--) {
                line.push(chordNode(random));
                if (random.chance(0.7)) line.push(nodeText(random.pick([' ', '  ', '   ', ' | '])));
            }
        } else {
            line = lineContent(random, 0);
        }

        lines.push(line);
        children.push(...line, nodeText(random.chance(0.9) ? '\n' : '\r\n'));
    }

    return JSON.stringify({ Name: 'pre', Classes: random.chance(0.2) ? 'pre-variable-width' : null, Children: children });
}

function movement(random: Random): [number, number, number] {
    const type = () => random.chance(0.97) ? random.pick([0, 0, 1, 1, 2, 3, 4, 5, 6, 7]) : random.pick([8, 200]);
    return [random.chance(0.98) ? random.int(0, 12) : random.pick([12, 255]), type(), type()];
}

/** Movements with loops in them: repeated patterns, mutated a little. */
function movementsWithLoops(random: Random, maxLength: number): [number, number, number][] {
    const result: [number, number, number][] = [];
    while (result.length < maxLength) {
        if (random.chance(0.3)) {
            result.push(movement(random));
            continue;
        }

        const loopLength = random.int(1, 6);
        const pattern = Array.from({ length: loopLength }, () => movement(random));
        const repeats = random.int(1, 5);
        for (let r = 0; r < repeats; r++) {
            for (const m of pattern) result.push(random.chance(0.05) ? movement(random) : m);
        }
    }

    return result.slice(0, maxLength);
}

function float(random: Random): number | string {
    const r = random.next();
    if (r < 0.3) return random.int(0, 5) / 4;
    if (r < 0.5) return Math.fround(random.next());
    if (r < 0.55) return random.pick(['NaN', 'Infinity', '-Infinity', '-0', 0, 1e-45, 3.4028234663852886e38, 1e39]);
    return Math.round(random.next() * 1000) / 1000;
}

function serializeLoopOf(items: [number, number, number][]): string {
    let binary = '';
    for (const [a, b, c] of items) binary += String.fromCharCode(a & 0xff, b & 0xff, c & 0xff);
    return btoa(binary);
}

const tonalityStrings = ['Am', 'C', 'C#', 'Db', 'Dbm', 'C#m', 'H', 'Hm', 'Сm', 'С', 'X', '', 'm', '#', 'b', 'A#m7', 'Ab', 'Abm', 'E#', 'Fb', 'B', 'Bb', 'Gm', 'G#m', 'a', 'Am ', 'Amm', '#m', 'bm'];

const formatValues = [
    0, -0, 0.5, 1.5, 2.5, -2.5, 0.125, 0.375, 0.005, 0.015, 0.025, 1.005, 1.115, 1.125, 2.675, 0.045, 0.0045, 1e-7, 123456789.125, 1234567.891,
    1e15, 1e16, 1e17, 1e21, 1e22, 1.7976931348623157e308, 5e-324, 0.1, 0.2, 0.3, 1 / 3, 2 / 3, 0.995, 0.9995, 9.995, 99.995, 999.5, 0.0005,
    0.33333334, 0.6666667, 1.3333334, 16777217, 0.1 + 0.2, 100, 1000, 999999.5, 0.49999999999999994, 4.35, 4.345, 1.45, 1.55, 0.0125, 3.4028235e38,
    'NaN', 'Infinity', '-Infinity', '-0', -1e-10, 1e-5, 0.0001, 0.00001, 123456789012345680000, 1.1e-5, 100000000000000000000, 1.2345678901234567e-7, 1e8, 1e9, 1e10, 123456789012, 1e17, 1e18, 99999999, 999999999, 4294967296,
];

const numberTexts = [
    '0', '-0', '-0.0', '-0e5', '-1e-400', '1e-400', '1', '1.0', '1.5', '1e2', '1E2', '1.5E2', '1e-2', '1E+2', '-1', '2147483647', '2147483648', '-2147483648', '-2147483649',
    '9223372036854775807', '9223372036854775808', '9007199254740993', '255', '256', '-1', '0.1', '0.30000001192092896', '3.4028235e38',
    '3.4028236e38', '3.5e38', '1e39', '1e309', '-1e309', '1e-46', '1e-50', '4.9e-324', '2e-324', '1.0000000596046448', '16777217', '0.1e1',
    '00', '01', '-', '+1', '.5', '5.', 'NaN', '"1"', 'null', 'true', ' 1 ', '1 ', '[1]', '1.7976931348623157e308', '0.000001', '1e0',
    '7.006492321624085e-46', '1.401298464324817e-45', '2.802596928649634e-45', '8.589973e9', '0.1000000000000000055511151231257827',
    '123456789012345678901234567890', '1.00000005', '1.0000000894069672', '0.500000059604644775390625',
];

const jsonCases: [string, string, 'camel' | 'pascal'][] = [
    ['HarmonyDB.Common.Representations.OneShelf.Note', '{"Value":1,"Alteration":0}', 'pascal'],
    ['HarmonyDB.Common.Representations.OneShelf.Note', '{"value":1,"alteration":0}', 'pascal'],
    ['HarmonyDB.Common.Representations.OneShelf.Note', '{"value":1,"alteration":2}', 'camel'],
    ['HarmonyDB.Common.Representations.OneShelf.Note', '{"value":1,"alteration":"Flat"}', 'camel'],
    ['HarmonyDB.Common.Representations.OneShelf.Note', '{"value":"1"}', 'camel'],
    ['HarmonyDB.Common.Representations.OneShelf.Note', '{"value":1.0}', 'camel'],
    ['HarmonyDB.Common.Representations.OneShelf.Note', '{"value":-0}', 'camel'],
    ['HarmonyDB.Common.Representations.OneShelf.Note', '{"value":1e0}', 'camel'],
    ['HarmonyDB.Common.Representations.OneShelf.Note', '{"value":256}', 'camel'],
    ['HarmonyDB.Common.Representations.OneShelf.Note', '{"value":1,"value":2}', 'camel'],
    ['HarmonyDB.Common.Representations.OneShelf.Note', ' \t\r\n{ "value" : 3 } \n', 'camel'],
    ['HarmonyDB.Common.Representations.OneShelf.Note', '{"value":3,}', 'camel'],
    ['HarmonyDB.Common.Representations.OneShelf.Note', '{"value":3}/*c*/', 'camel'],
    ['HarmonyDB.Common.Representations.OneShelf.Note', '\ufeff{"value":3}', 'camel'],
    ['HarmonyDB.Common.Representations.OneShelf.Note', '{"Value":3}', 'camel'],
    ['HarmonyDB.Common.Representations.OneShelf.Note', '{"VALUE":3}', 'pascal'],
    ['HarmonyDB.Common.Representations.OneShelf.Note', 'null', 'pascal'],
    ['HarmonyDB.Common.Representations.OneShelf.Note', '[]', 'pascal'],
    ['HarmonyDB.Common.Representations.OneShelf.Note', '', 'pascal'],
    ['HarmonyDB.Common.Representations.OneShelf.Note', '{"value":3} {"value":4}', 'camel'],
    ['HarmonyDB.Common.Representations.OneShelf.Note', '{"\\u0076alue":3}', 'camel'],
    ['HarmonyDB.Common.Representations.OneShelf.NodeText', '{"Text":"a\\u0000b\\ud800\\udc00\\ud800x\\/"}', 'pascal'],
    ['HarmonyDB.Common.Representations.OneShelf.NodeText', '{"Text":"\\uDE00"}', 'pascal'],
    ['HarmonyDB.Common.Representations.OneShelf.NodeText', '{"Text":"\u0001"}', 'pascal'],
    ['HarmonyDB.Common.Representations.OneShelf.NodeText', '{"Text":"\\x"}', 'pascal'],
    ['HarmonyDB.Common.Representations.OneShelf.NodeText', '{"Text":null}', 'pascal'],
    ['HarmonyDB.Common.Representations.OneShelf.NodeText', '{}', 'pascal'],
    ['HarmonyDB.Common.Representations.OneShelf.NodeText', '{"Text":"\u2028\u2029\u0085\u00a0\ufeff"}', 'pascal'],
    ['HarmonyDB.Common.Representations.OneShelf.NodeHtml', '{"Name":"pre","Children":null}', 'pascal'],
    ['HarmonyDB.Common.Representations.OneShelf.NodeHtml', '{"Name":"pre","Children":[{}]}', 'pascal'],
    ['HarmonyDB.Common.Representations.OneShelf.NodeHtml', '{"Name":"pre","Children":[{"NodeText":{"Text":"a"},"NodeChord":{"Children":[]}}]}', 'pascal'],
    ['HarmonyDB.Common.Representations.OneShelf.NodeHtml', '{"Name":"pre","Children":[{"NodeText":null}]}', 'pascal'],
    ['HarmonyDB.Common.Representations.OneShelf.NodeHtml', '{"Name":"pre","IsBlock":true,"IsVariableWidth":true}', 'pascal'],
    ['HarmonyDB.Common.Representations.OneShelf.NodeHtml', `{"Name":"pre","Children":${'['.repeat(70)}${']'.repeat(70)}}`, 'pascal'],
    ['OneShelf.Frontend.Api.Model.V3.Api.IllustrationsResponse', '{"illustrations":null,"titles":{"10":"a","2":"b","10":"c"}}', 'camel'],
    ['OneShelf.Frontend.Api.Model.V3.Api.IllustrationsResponse', '{"illustrations":null,"titles":{"-0":"a"}}', 'camel'],
    ['OneShelf.Frontend.Api.Model.V3.Api.IllustrationsResponse', '{"illustrations":null,"titles":{"01":"a"}}', 'camel'],
    ['OneShelf.Frontend.Api.Model.V3.Api.IllustrationsResponse', '{"illustrations":null,"titles":{" 1":"a"}}', 'camel'],
    ['OneShelf.Frontend.Api.Model.V3.Api.IllustrationsResponse', '{"illustrations":null,"titles":{"+1":"a"}}', 'camel'],
    ['OneShelf.Frontend.Api.Model.V3.Api.IllustrationsResponse', '{"illustrations":null,"titles":{"1.0":"a"}}', 'camel'],
    ['OneShelf.Frontend.Api.Model.V3.Api.IllustrationsResponse', '{"illustrations":null,"titles":{"2147483648":"a"}}', 'camel'],
    ['OneShelf.Frontend.Api.Model.V3.Api.GetPdfsResponse', '{"pdfs":{"a":{"pdfData":"AAEC/w=","pageCount":2,"previewLink":null}},"volume":null}', 'camel'],
    ['OneShelf.Frontend.Api.Model.V3.Api.GetPdfsResponse', '{"pdfs":{"a":{"pdfData":"AAEC /w==","pageCount":2,"previewLink":null}},"volume":null}', 'camel'],
    ['OneShelf.Frontend.Api.Model.V3.Api.GetPdfsResponse', '{"pdfs":{"a":{"pdfData":"AAEC\\/w==","pageCount":2,"previewLink":null}},"volume":null}', 'camel'],
    ['OneShelf.Frontend.Api.Model.V3.Api.GetPdfsResponse', '{"pdfs":{"a":{"pdfData":"","pageCount":2,"previewLink":null}},"volume":null}', 'camel'],
    ['OneShelf.Frontend.Api.Model.V3.Api.GetPdfsResponse', '{"pdfs":{"a":{"pdfData":"AAE","pageCount":2,"previewLink":null}},"volume":null}', 'camel'],
    ['OneShelf.Frontend.Api.Model.V3.Api.GetPdfsResponse', '{"pdfs":{"a":{"pdfData":[1,2],"pageCount":2,"previewLink":null}},"volume":null}', 'camel'],
    ['OneShelf.Frontend.Api.Model.V3.Databasish.Like', '{"userId":1,"level":3,"createdOn":"2023-01-02T03:04:05.1234567890","transpose":null,"versionId":null,"likeCategoryId":null}', 'camel'],
    ['OneShelf.Frontend.Api.Model.V3.Databasish.Like', '{"userId":1,"level":3,"createdOn":"2023-01-02T03:04:05.12345678901234567","transpose":null,"versionId":null,"likeCategoryId":null}', 'camel'],
    ['OneShelf.Frontend.Api.Model.V3.Databasish.Like', '{"userId":1,"level":3,"createdOn":"2023-01-02T03:04:05.99999999","transpose":null,"versionId":null,"likeCategoryId":null}', 'camel'],
    ['OneShelf.Frontend.Api.Model.V3.Databasish.Like', '{"userId":1,"level":3,"createdOn":"2023-01-02T03:04","transpose":null,"versionId":null,"likeCategoryId":null}', 'camel'],
    ['OneShelf.Frontend.Api.Model.V3.Databasish.Like', '{"userId":1,"level":3,"createdOn":"2023-01-02","transpose":null,"versionId":null,"likeCategoryId":null}', 'camel'],
    ['OneShelf.Frontend.Api.Model.V3.Databasish.Like', '{"userId":1,"level":3,"createdOn":"2023-01-02T03:04:05+0300","transpose":null,"versionId":null,"likeCategoryId":null}', 'camel'],
    ['OneShelf.Frontend.Api.Model.V3.Databasish.Like', '{"userId":1,"level":3,"createdOn":"2023-01-02T03:04:05+03","transpose":null,"versionId":null,"likeCategoryId":null}', 'camel'],
    ['OneShelf.Frontend.Api.Model.V3.Databasish.Like', '{"userId":1,"level":3,"createdOn":"2023-01-02T03:04:05.000Z","transpose":null,"versionId":null,"likeCategoryId":null}', 'camel'],
    ['OneShelf.Frontend.Api.Model.V3.Databasish.Like', '{"userId":1,"level":3,"createdOn":"2023-01-02 03:04:05","transpose":null,"versionId":null,"likeCategoryId":null}', 'camel'],
    ['OneShelf.Frontend.Api.Model.V3.Databasish.Like', '{"userId":1,"level":3,"createdOn":"2023-01-02T03:04:05z","transpose":null,"versionId":null,"likeCategoryId":null}', 'camel'],
    ['OneShelf.Frontend.Api.Model.V3.Databasish.Like', '{"userId":1,"level":3,"createdOn":"2023-01-02T03:04:60","transpose":null,"versionId":null,"likeCategoryId":null}', 'camel'],
    ['OneShelf.Frontend.Api.Model.V3.Databasish.Like', '{"userId":1,"level":3,"createdOn":"2023-01-02T03:04:05\\u002B03:00","transpose":null,"versionId":null,"likeCategoryId":null}', 'camel'],
    ['OneShelf.Frontend.Api.Model.V3.Databasish.Like', '{"userId":9007199254740993,"level":3,"createdOn":"2023-01-02T03:04:05","transpose":null,"versionId":null,"likeCategoryId":null}', 'camel'],
    ['OneShelf.Frontend.Api.Model.V3.Databasish.Like', '{"userId":1,"level":3,"createdOn":"2023-01-02T03:04:05","transpose":null,"versionId":null}', 'camel'],
    ['OneShelf.Frontend.Api.Model.V3.Instant.VisitedSearch', '{"query":"q","actionId":"0F8FAD5B-D9CB-469F-A165-70867728950E","happenedOn":"2024-01-01T00:00:00"}', 'camel'],
    ['OneShelf.Frontend.Api.Model.V3.Instant.VisitedSearch', '{"query":"q","actionId":"{0f8fad5b-d9cb-469f-a165-70867728950e}","happenedOn":"2024-01-01T00:00:00"}', 'camel'],
    ['OneShelf.Frontend.Api.Model.V3.Instant.VisitedSearch', '{"query":"q","actionId":"0f8fad5bd9cb469fa16570867728950e","happenedOn":"2024-01-01T00:00:00"}', 'camel'],
    ['OneShelf.Frontend.Api.Model.V3.Instant.VisitedSearch', '{"query":"q","actionId":"(0f8fad5b-d9cb-469f-a165-70867728950e)","happenedOn":"2024-01-01T00:00:00"}', 'camel'],
    ['OneShelf.Frontend.Api.Model.V3.Instant.VisitedSearch', '{"query":"q","actionId":" 0f8fad5b-d9cb-469f-a165-70867728950e","happenedOn":"2024-01-01T00:00:00"}', 'camel'],
    ['OneShelf.Frontend.Api.Model.V3.Databasish.LikeCategory', '{"id":1,"userId":1,"name":"n","cssColor":"c","order":0,"privateWeight":0.1,"cssIcon":"i","access":0}', 'camel'],
    ['OneShelf.Frontend.Api.Model.V3.Databasish.LikeCategory', '{"id":1,"userId":1,"name":"n","cssColor":"c","order":0,"privateWeight":1e39,"cssIcon":"i","access":0}', 'camel'],
    ['OneShelf.Frontend.Api.Model.V3.Databasish.LikeCategory', '{"id":1,"userId":1,"name":"n","cssColor":"c","order":0,"privateWeight":1.00000005,"cssIcon":"i","access":0}', 'camel'],
    ['OneShelf.Frontend.Api.Model.V3.Databasish.LikeCategory', '{"id":1,"userId":1,"name":"n","cssColor":"c","order":0,"privateWeight":-0.0,"cssIcon":"i","access":0}', 'camel'],
    ['OneShelf.Frontend.Api.Model.V3.Databasish.LikeCategory', '{"id":1,"userId":1,"name":"n","cssColor":"c","order":0,"privateWeight":1E-45,"cssIcon":"i","access":0}', 'camel'],
    ['OneShelf.Frontend.Api.Model.V3.Databasish.LikeCategory', '{"id":1,"userId":1,"name":"n","cssColor":"c","order":0,"privateWeight":123456789,"cssIcon":"i","access":0}', 'camel'],
    ['OneShelf.Frontend.Api.Model.V3.Databasish.LikeCategory', '{"id":1,"userId":1,"name":"n","cssColor":"c","order":0,"privateWeight":0.00001,"cssIcon":"i","access":0}', 'camel'],
    ['OneShelf.Frontend.Api.Model.V3.Databasish.LikeCategory', '{"id":1,"userId":1,"name":"n","cssColor":"c","order":0,"privateWeight":1e7,"cssIcon":"i","access":0}', 'camel'],
    ['OneShelf.Frontend.Api.Model.V3.Databasish.LikeCategory', '{"id":1,"userId":1,"name":"n","cssColor":"c","order":0,"privateWeight":1e8,"cssIcon":"i","access":0}', 'camel'],
    ['HarmonyDB.Source.Api.Model.V1.Chords', '{"output":{"name":"pre","classes":null,"children":[]},"bestTonality":"Am","isStable":true,"isPublic":false,"unstableErrorMessage":null,"sourceUri":"https://example.com/a","type":null,"specificAttributes":{"SourceColor":"Primary","n":1.50,"o":{"a":[1,2.0e0,-0.0]},"s":"\\u00e9"},"externalId":"123","source":"S","rating":4.5,"title":"T","artists":["A"]}', 'camel'],
    ['HarmonyDB.Source.Api.Model.V1.Chords', '{"output":null,"specificAttributes":{"a":null,"a":1}}', 'camel'],
    ['OneShelf.Authorization.Api.Model.Identity', '{"id":5,"first_name":"B","hash":"h","auth_date":1}', 'camel'],
    ['OneShelf.Authorization.Api.Model.Identity', '{"id":5,"First_Name":"B","hash":"h","auth_date":1}', 'camel'],
    ['OneShelf.Authorization.Api.Model.Identity', '{"id":5,"firstName":"B","hash":"h","auth_date":1}', 'camel'],
];

export function generateCases(seed: number, scale = 1): Case[] {
    const random = new Random(seed);
    const cases: Case[] = [];
    let n = 0;
    const add = (suite: string, input: unknown) => cases.push({ id: `${suite}-${n++}`, suite, input });

    // Chords: every suffix with every alteration on a few roots, then random ones, then the human spellings.
    for (const suffix of suffixes) {
        for (const alteration of alterations) {
            add('chord', { s: `[!${random.int(0, 12)}${alteration}!]${suffix}` });
        }
        add('chord', { s: `[!3!]${suffix}/[!${random.int(0, 12)}${random.pick(alterations)}!]` });
    }
    for (let i = 0; i < 600 * scale; i++) add('chord', { s: chordDataString(random) });
    for (let i = 0; i < 400 * scale; i++) add('chord', { s: humanChord(random) });

    for (let i = 0; i < 300 * scale; i++) {
        add('note', { v: random.chance(0.9) ? random.int(0, 12) : random.pick([12, 13, 255]), a: random.pick([null, 0, 1]), st: settings(random), d: random.int(-30, 31) });
    }

    for (let i = 0; i < 160 * scale; i++) {
        const st = Array.from({ length: random.int(1, 4) }, () => settings(random));
        add('html', {
            json: songJson(random),
            st,
            pipeline: random.chance(0.85),
            ex: Array.from({ length: random.int(0, 6) }, () => [random.int(-1, 30), random.int(-1, 40)]),
        });
    }

    for (let i = 0; i < 60 * scale; i++) {
        const songs: string[][] = [];
        const base = Array.from({ length: random.int(2, 6) }, () => chordDataString(random));
        for (let s = random.int(1, 6); s > 0; s--) {
            const song: string[] = [];
            for (let k = random.int(0, 40); k > 0; k--) {
                song.push(random.chance(0.7) ? random.pick(base) : chordDataString(random));
            }
            songs.push(song);
        }
        add('search', {
            songs,
            dup: random.chance(0.2),
            top: random.pick([null, 1, 2, 0, 2147483647]),
            ex: Array.from({ length: random.int(1, 6) }, () => [random.int(0, songs.length), random.int(-1, 20), random.int(0, 30)]),
        });
    }

    const blockTypeSets = [
        [0, 1, 4, 5],
        [0, 1, 4, 5, 7, 8],
        [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11],
        [0, 1, 3, 4, 5, 6, 7, 8, 9, 10, 11],
        [9, 1, 4, 5],
        [0, 9, 1, 4, 5],
        [0, 10, 1, 4, 5],
        [0, 2, 3],
        [0, 1],
    ];
    for (let i = 0; i < 120 * scale; i++) {
        const length = random.chance(0.9) ? random.int(0, 40) : random.int(40, 120);
        add('loop', {
            m: movementsWithLoops(random, length),
            fr: random.chance(0.97) ? random.int(0, 12) : 255,
            types: Array.from({ length: random.int(0, 3) }, () => random.pick(blockTypeSets)),
            pp: random.chance(0.3),
        });
    }

    for (let i = 0; i < 80 * scale; i++) {
        const seqs = Array.from({ length: random.int(0, 4) }, () => ({ fr: random.int(0, 12), fi: random.int(0, 50), m: movementsWithLoops(random, random.int(0, 30)) }));
        add('links', {
            len: random.int(0, 100),
            id: random.pick(['x', '', 'кино', '123']),
            seqs,
            phrase: Array.from({ length: random.int(1, 5) }, () => random.pick(['Am', 'C', 'G', 'F', 'Dm', 'E7', 'Hm', 'C#', 'Bb'])).join(' '),
            top: random.pick([null, 1, 2]),
        });
    }

    for (let i = 0; i < 150 * scale; i++) {
        const tieHeavy = random.chance(0.4);
        add('tonal', {
            p: Array.from({ length: random.chance(0.95) ? 24 : random.int(0, 30) }, () => tieHeavy ? random.pick([0, 0.25, 0.5]) : float(random)),
            pred: random.chance(0.8) ? [random.int(0, 12), random.chance(0.5)] : null,
            ton: [random.chance(0.95) ? random.int(0, 12) : random.int(12, 20), random.chance(0.5)],
            bn: random.int(0, 12),
            nr: random.chance(0.7) ? random.int(0, 12) : null,
            idx: Array.from({ length: random.int(0, 5) }, () => random.int(-3, 50)),
            strs: Array.from({ length: random.int(0, 5) }, () => random.pick(tonalityStrings)),
            chords: Array.from({ length: random.int(0, 6) }, () => [random.chance(0.95) ? random.int(0, 12) : random.int(12, 30), random.chance(0.95) ? random.int(0, 8) : 9]),
            w: Array.from({ length: random.int(0, 3) }, () => [float(random), float(random), random.int(0, 6), random.chance(0.5)]),
            loops: Array.from({ length: random.int(0, 4) }, () => random.chance(0.9)
                ? serializeLoopOf(movementsWithLoops(random, random.int(0, 8)))
                : random.pick(['', 'AQID', 'AQ==', 'A', '====', 'AQIDBA==', 'AAAA', 'AwkL', '!!!!', 'AQID AQID'])),
        });
    }

    const words = ['кино', 'Кино', 'Ёлка', 'ёлка', 'елка', "rock'n'roll", 'rock-n-roll', 'Rock', 'the', 'The Beatles', 'Битлз', 'Аквариум', 'город', 'Город золотой',
        'a', 'A', 'love', 'Love me do', 'İstanbul', 'straße', 'STRASSE', 'ǅemal', 'ΣΊΣΥΦΟΣ', 'x2', '12', '007', "it's", 'Мама-Анархия', 'дДт', 'ДДТ',
        'Хвост', 'хвостик', '', ' ', 'Ты,', ',', "'", "''"];
    for (let i = 0; i < 60 * scale; i++) {
        const songs = Array.from({ length: random.int(0, 12) }, (_, k) => ({
            title: Array.from({ length: random.int(1, 4) }, () => random.pick(words)).join(random.pick([' ', '-', ', ', "'"])),
            kw: random.chance(0.3) ? Array.from({ length: random.int(1, 3) }, () => random.pick(words)).join(' ') : null,
            index: random.chance(0.9) ? k + 1 : random.int(0, 5),
            artists: Array.from({ length: random.int(0, 3) }, () => ({ name: random.pick(words), syn: Array.from({ length: random.int(0, 3) }, () => random.pick(words)) })),
            likes: Array.from({ length: random.int(0, 5) }, () => [random.int(1, 5), random.int(0, 5)]),
        }));
        const queries = Array.from({ length: random.int(1, 5) }, () => random.chance(0.15)
            ? random.pick(['', ' ', '12', ' 3 ', '1', '0', '+2', '2147483648', "'", ',', 'ё', '--', '1\u0000'])
            : Array.from({ length: random.int(1, 3) }, () => {
                const w = random.pick(words);
                return random.chance(0.3) ? w.slice(0, random.int(1, Math.max(2, w.length))) : w;
            }).join(random.pick([' ', ',', '-'])));
        add('fts', {
            songs,
            queries,
            users: [random.int(1, 5), 999],
            frag: Array.from({ length: random.int(1, 6) }, () => [
                random.chance(0.1) ? null : random.pick(queries),
                random.chance(0.8) ? random.pick(songs.map(s => s.title).concat(['x'])) : nastyString(random, 3),
            ]),
        });
    }

    // The .NET runtime behaviour itself.
    add('dotnet', { f: 'chars' });
    for (let i = 0; i < 120 * scale; i++) {
        const length = random.pick([0, 1, 2, 3, 5, 15, 16, 17, 18, 31, 32, 33, 50, 100, 300, 1000]);
        const distinct = random.pick([1, 2, 3, 5, 100]);
        const pattern = random.int(0, 4);
        add('dotnet', {
            f: 'sort',
            keys: Array.from({ length }, (_, k) => pattern === 0 ? random.int(0, distinct) : pattern === 1 ? k % distinct : pattern === 2 ? length - k : Math.trunc(k / 3)),
        });
    }
    for (let i = 0; i < 40 * scale; i++) {
        add('dotnet', { f: 'sortStrings', keys: Array.from({ length: random.pick([3, 17, 40]) }, () => random.pick(['a', 'A', 'b', 'B', 'ё', 'е', 'Ё', 'é', 'é', '', ' ', 'a b', 'ab', '😀', '￿', 'ß', 'ss', 'z', 'Z', '1', '10', '2', '-', "'", 'co-op', 'coop'])) });
    }
    for (let i = 0; i < 80 * scale; i++) {
        const ops: unknown[] = [];
        for (let k = random.int(0, 200); k > 0; k--) ops.push(random.chance(0.7) ? random.int(0, random.pick([1, 2, 3, 10, 1000])) : 'd');
        add('dotnet', { f: 'pq', ops });
        const floatOps: unknown[] = [];
        for (let k = random.int(0, 100); k > 0; k--) floatOps.push(random.chance(0.7) ? [random.pick([0, 1, 1.5, 3.4028234663852886e38, 'NaN', 'Infinity', 'Infinity', '-0', 0.1])] : 'd');
        add('dotnet', { f: 'pqFloat', ops: floatOps });
    }
    for (const v of formatValues) {
        for (const d of [0, 1, 2, 3]) add('dotnet', { f: 'format', v, d });
    }
    for (let i = 0; i < 200 * scale; i++) {
        const v = random.chance(0.5) ? random.int(0, 100000) / random.pick([8, 16, 1000, 200, 3, 7]) : (random.next() - 0.3) * Math.pow(10, random.int(-12, 25));
        add('dotnet', { f: 'format', v, d: random.int(0, 4) });
    }
    for (const txt of numberTexts) add('dotnet', { f: 'readNumber', txt });
    for (const s of nastyStrings) add('dotnet', { f: 'str', s, t: random.pick(nastyStrings) });
    for (let i = 0; i < 300 * scale; i++) add('dotnet', { f: 'str', s: nastyString(random), t: random.chance(0.5) ? nastyString(random, 1) : '' });
    for (let i = 0; i < 20 * scale; i++) add('dotnet', { f: 'bytes', b: Array.from({ length: random.int(0, 10) }, () => random.int(0, 256)) });
    for (let i = 0; i < 20 * scale; i++) add('dotnet', { f: 'hash', v: Array.from({ length: random.int(0, 20) }, () => random.pick([0, 1, -1, 2147483647, -2147483648, random.int(-1000000, 1000000)])) });

    for (const [type, json, naming] of jsonCases) add('json', { type, json, naming });

    cases.push(...generateUriCases(new Random(seed + 0x5552), scale));

    return cases;
}
