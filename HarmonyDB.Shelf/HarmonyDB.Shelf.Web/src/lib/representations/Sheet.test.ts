// The widened sheet (keys, tempo, time signatures, degrees, times), as HarmonyDB.Common.Representations.OneShelf.Tests/SheetTests.cs
// checks it on the .NET side: the same cases, the same expectations.

import { describe, it } from 'node:test';
import assert from 'node:assert/strict';
import { defaultJsonSerializerOptions, deserialize, serialize } from '../common/dotnet/json/JsonSerializer';
import { NoteAlteration } from '../common/HarmonyDB.Common/NoteAlteration';
import { NodeBase, NodeChord, NodeHtml, NodeMark, NodeText, RepresentationSettings, SheetKey, SheetTempo, SheetTimeSignature, SimplificationMode } from './index';

const chord = (text: string, degree: string | null = null, bassDegree: string | null = null, time: number | null = null) =>
    new NodeChord({ children: [new NodeText(text).asChild()], degree, bassDegree, time });

const sheet = (...nodes: NodeBase[]) => new NodeHtml({ name: 'pre', children: nodes.map(x => x.asChild()) });

const visible = (html: string) => html.replace(/<[^>]+>/g, '')
    .replace(/&lt;/g, '<').replace(/&gt;/g, '>').replace(/&quot;/g, '"').replace(/&#39;/g, '\'').replace(/&amp;/g, '&');

const settings = (args: ConstructorParameters<typeof RepresentationSettings>[0] = {}, init: ConstructorParameters<typeof RepresentationSettings>[1] = {}) =>
    new RepresentationSettings(args, init);

describe('Sheet', () => {
    it('a plain sheet renders as before', () => {
        assert.equal(sheet(chord('Am7'), new NodeText('\nla la\n'), chord('C')).asHtml(settings()),
            '<pre><div class=\'chords-double-line\'><span class=\'chords-line chords-double-line\'><span class=\'flying\'><span class=\'flying2\'><b class=\'chord\' chord-index=\'0\' chord-custom=\'\' chord-data=\'[!0!]m7\'>Am<sub>7</sub></b></span></span>la la</span></div><span class=\'chords-line \'><b class=\'chord\' chord-index=\'1\' chord-custom=\'\' chord-data=\'[!3!]\'>C</b></span>\n</pre>');
    });

    it('old JSON reads and writes the same', () => {
        const json = '{"Name":"pre","Classes":null,"Children":[{"NodeChord":{"Children":[{"NodeText":{"Text":"Am"}}]}},{"NodeText":{"Text":" la"}}]}';
        assert.equal(serialize(deserialize(json, NodeHtml.json, defaultJsonSerializerOptions), NodeHtml.json), json);
    });

    it('new data round-trips', () => {
        const value = new NodeHtml({
            name: 'pre',
            keys: [new SheetKey('F#', true), new SheetKey('A', false)],
            children: [
                new NodeMark({ key: new SheetKey('F#', true), tempo: new SheetTempo(116), timeSignature: new SheetTimeSignature(4, 4), time: 0 }).asChild(),
                new NodeText('\n').asChild(),
                chord('C#7/E#', 'V', '#VII', 1.5).asChild(),
                new NodeText('\n').asChild(),
                new NodeText('word', 1.52).asChild(),
            ],
        });

        const json = serialize(value, NodeHtml.json);
        const back = deserialize(json, NodeHtml.json, defaultJsonSerializerOptions);
        assert.equal(serialize(back, NodeHtml.json), json);
        assert.equal(back.asHtml(settings()), value.asHtml(settings()));
        assert.deepEqual(back.keys!.map(x => x.name), ['F#m', 'A']);
    });

    it('degrees', () => {
        const value = sheet(chord('Am7/G', 'VI', 'V'));
        assert.ok(value.asHtml(settings()).includes('<sup class=\'chord-degree\'>VI/V</sup>'));
        assert.ok(value.asHtml(settings({ simplification: SimplificationMode.RemoveBass })).includes('<sup class=\'chord-degree\'>VI</sup>'));
        assert.ok(!value.asHtml(settings({}, { showDegrees: false })).includes('chord-degree'));

        // Transposed, the degrees stay.
        assert.ok(value.asHtml(settings({ transpose: 3 })).includes('<sup class=\'chord-degree\'>VI/V</sup>'));
        assert.ok(!sheet(chord('Am')).asHtml(settings()).includes('chord-degree'));
    });

    it('superscripts', () => {
        const value = sheet(chord('C7b9'), new NodeText(' '), chord('Dm7b5'));
        const html = value.asHtml(settings());
        assert.ok(html.includes('C<sub>7</sub><sup>b9</sup>'));
        const plain = value.asHtml(settings({}, { chordSuperscripts: false }));
        assert.ok(!plain.includes('<sup>'));
        assert.ok(!plain.includes('<sub>'));
        assert.equal(visible(plain), 'C7b9 Dm7b5\n');
        assert.equal(visible(html), visible(plain));
    });

    const spelled: [string, number, boolean, string][] = [
        // shown as written
        ['E#m', 0, true, 'E#m'], ['Cb', 0, true, 'Cb'], ['Am/Fb', 0, true, 'Am/Fb'], ['D#/F##', 0, true, 'D#/F##'],
        ['Bbb7', 0, true, 'Bbb7'], ['C#', 0, true, 'C#'], ['Db', 0, true, 'Db'],
        // named by the pitch, as always
        ['E#m', 0, false, 'Fm'], ['Am/Fb', 0, false, 'Am/E'], ['D#/F##', 0, false, 'D#/F##'], ['F##m', 0, false, 'F##m'],
        ['Bbb7', 0, false, 'Bbb7'], ['Abba', 0, false, 'Abba'], ['C#', 0, false, 'C#'], ['Db', 0, false, 'Db'],
        // transposed
        ['E#m', 1, true, 'F#m'], ['D#/F##', 1, true, 'E/G#'], ['F##m', 1, false, 'G#m'], ['Bbb7', 2, false, 'B7'], ['C##dim', -2, false, 'Cdim'],
        // the last of two signs before 5, 9, 11 or 13 alters that degree
        ['Bbb5', 0, true, 'Bbb5'], ['Bbb5', 1, true, 'Bb5'], ['Abb9', 1, true, 'Ab9'], ['Bbb13', 2, true, 'Cb13'], ['C##5', 1, true, 'D#5'], ['F##9', 1, true, 'G#9'],
    ];
    for (const [value, transpose, asWritten, expected] of spelled) {
        it(`spelled notes: ${value} ${transpose} ${asWritten}`, () => {
            assert.equal(visible(sheet(chord(value)).asHtml(settings({ transpose }, { chordSuperscripts: false, showNotesAsWritten: asWritten }))), expected + '\n');
        });
    }

    for (const [value, data] of [['Bbb5', '[!1b!]b5'], ['Abb9', '[!11b!]b9'], ['C##5', '[!4#!]#5'], ['F##9', '[!9#!]#9'], ['F##m', '[!10#!]m'], ['Bbb7', '[!0b!]7']] as const) {
        it(`double signs: ${value}`, () => {
            assert.deepEqual(sheet(chord(value)).asChords(settings()), [data]);
        });
    }

    for (const [value, expected] of [['Abb9', 'Ab'], ['Bbb13', 'Bb'], ['F##9', 'F#'], ['Bbb7', 'Bbb']] as const) {
        it(`double signs simplified: ${value}`, () => {
            assert.equal(visible(sheet(chord(value)).asHtml(settings({ simplification: SimplificationMode.All }, { showNotesAsWritten: true }))).trim(), expected);
        });
    }

    it('times', () => {
        const html = sheet(chord('Am', null, null, 1.25), new NodeText('\n'), new NodeText('hello ', 1.3), new NodeText('world', 2)).asHtml(settings());
        assert.ok(html.includes('chord-data=\'[!0!]m\' data-time=\'1.25\'>'));
        assert.ok(html.includes('<span data-time=\'1.3\'>hello </span>'));
        assert.ok(html.includes('<span data-time=\'2\'>world</span>'));
        assert.ok(html.includes('<span class=\'chords-line chords-double-line\' data-time=\'1.25\'>'));
    });

    it('compress keeps timed words', () => {
        const value = sheet(new NodeText('a '), new NodeText('b', 1), new NodeText(' c'), new NodeText('d', 2));
        value.fix0_Compress();
        assert.deepEqual(value.children.map(x => [x.nodeText!.text, x.nodeText!.time]), [['a ', null], ['b c', 1], ['d', 2]]);

        const plain = sheet(new NodeText('a '), new NodeText('b'));
        plain.fix0_Compress();
        assert.equal(plain.children.length, 1);
        assert.equal(plain.children[0]!.nodeText!.text, 'a b');
    });

    it('marks', () => {
        const value = sheet(
            new NodeMark({ key: new SheetKey('A', true), timeSignature: new SheetTimeSignature(3, 4), tempo: new SheetTempo(72), time: 0 }),
            new NodeText('\n'),
            chord('Am'),
            new NodeText('\nla la\n'),
            new NodeMark({ key: new SheetKey('C', false), time: 10 }),
            new NodeText('\n'),
            chord('C'),
            new NodeText('\nla la'));

        const html = value.asHtml(settings());
        // the .NET expectation with its two inline styles taken out (the port leaves them out: NodeMark.ts)
        assert.ok(html.includes('<span class=\'sheet-mark\' style=\'white-space: nowrap\' data-time=\'0\'>'.replace(' style=\'white-space: nowrap\'', '')));
        assert.ok(!html.includes('style='));
        assert.ok(html.includes('data-key=\'Am\''));
        assert.ok(html.includes('aria-label=\'3/4\''));
        assert.ok(html.includes(' = 72</span>'));
        assert.ok(html.includes('data-key=\'C\''));

        // The chords still stand over their words.
        assert.equal(html.match(/<div class='chords-double-line'>/g)!.length, 2);
        assert.equal(value.asLyrics(), 'la la\nla la');

        // Transposed, the marks' keys move with the chords.
        const moved = value.asHtml(settings({ transpose: 1, alteration: NoteAlteration.Flat }));
        assert.ok(moved.includes('data-key=\'Bbm\''));
        assert.ok(moved.includes('data-key=\'Db\''));
    });

    it('marks leave no trace in the text', () => {
        const value = sheet(
            new NodeMark({ key: new SheetKey('A', true) }),
            new NodeText('\n'),
            chord('Am'),
            new NodeText('\nla '),
            new NodeMark({ tempo: new SheetTempo(90) }),
            new NodeText(' la\n'),
            new NodeMark({ timeSignature: new SheetTimeSignature(3, 4) }),
            new NodeText('\nend'));

        const withoutMarks = sheet(chord('Am'), new NodeText('\nla la\nend'));
        assert.equal(value.asOneShelf(settings()), withoutMarks.asOneShelf(settings()));
    });

    for (const key of ['Em', 'C', 'Bb', 'F#m', 'Cb', 'E#m', 'G#']) {
        it(`keys agree with chords: ${key}`, () => {
            const value = sheet(new NodeMark({ key: SheetKey.parse(key) }), new NodeText('\n'), chord(key));
            for (const asWritten of [false, true])
                for (const alteration of [null, NoteAlteration.Flat, NoteAlteration.Sharp])
                    for (let transpose = -5; transpose <= 6; transpose++) {
                        const html = value.asHtml(settings({ transpose, alteration }, { showNotesAsWritten: asWritten, chordSuperscripts: false }));
                        const shown = /<b class='chord'[^>]*>([^<]*)<\/b>/.exec(html)![1];
                        assert.ok(html.includes(`data-key='${shown}'`), `${key} ${transpose} ${alteration} ${asWritten}`);
                    }
        });
    }

    const signatures: [string, NoteAlteration | null, number][] = [
        ['G#', null, 8], ['D#', null, 9], ['A#', null, 10], ['Fb', null, -8], ['Dbm', null, -8], ['F##', null, 13],
        ['F#m', NoteAlteration.Flat, -9], ['C#', null, 7], ['Cb', null, -7], ['F#m', null, 3],
    ];
    for (const [key, alteration, signature] of signatures) {
        it(`a key signature is drawn only when it exists: ${key}`, () => {
            const html = sheet(new NodeMark({ key: SheetKey.parse(key) })).asHtml(settings({ alteration }, { showNotesAsWritten: true }));
            const shown = /data-key='([^']+)'/.exec(html)![1]!;
            assert.equal(SheetKey.parse(shown).signature, signature);
            assert.equal(html.includes('<svg'), Math.abs(signature) <= 7);
            assert.ok(html.includes(`<span class='sheet-mark-key-name'>${shown}</span>`));
        });
    }

    for (const [key, signature] of [['C', 0], ['A', 3], ['F#m', 3], ['Bb', -2], ['Ebm', -6], ['C#', 7], ['Cb', -7]] as const) {
        it(`key signatures: ${key}`, () => {
            assert.equal(SheetKey.parse(key).signature, signature);
        });
    }
});
