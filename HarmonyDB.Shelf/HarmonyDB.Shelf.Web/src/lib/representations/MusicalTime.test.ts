// Musical time in the sheet (bars, places, the metre of a bar), as HarmonyDB.Common.Representations.OneShelf.Tests/
// MusicalTimeTests.cs checks it on the .NET side: the same cases, the same expectations.

import { describe, it } from 'node:test';
import assert from 'node:assert/strict';
import { camelCaseJsonSerializerOptions, defaultJsonSerializerOptions, deserialize, serialize } from '../common/dotnet/json/JsonSerializer';
import { NodeBase, NodeChord, NodeHtml, NodeMark, NodeText, RepresentationSettings, SheetBar, SheetKey, SheetPlace, SheetTimes } from './index';

// Two bars of 4/4 at 60 to the minute from 1 s, a bar of 2+2+3/8, a free bar; the sheet ends at 14 s.
const times = new SheetTimes({
    end: 14,
    bars: [
        new SheetBar({ beats: [1, 2, 3, 4], timeSignature: '4/4' }),
        new SheetBar({ beats: [5, 6, 7, 8] }),
        new SheetBar({ beats: [9, 9.5, 10], timeSignature: '2+2+3/8' }),
        new SheetBar({ beats: [11, 13], timeSignature: 'free' }),
    ],
});

const chord = (text: string, place: SheetPlace | null = null, time: number | null = null) =>
    new NodeChord({ children: [new NodeText(text).asChild()], place, time });

const sheet = (sheetTimes: SheetTimes | null, ...nodes: NodeBase[]) => new NodeHtml({ name: 'pre', times: sheetTimes, children: nodes.map(x => x.asChild()) });

const settings = (transpose = 0) => new RepresentationSettings({ transpose });

describe('Musical time', () => {
    const moments: [number, number, string | null, number][] = [
        [1, 1, null, 1], [1, 3, null, 3], [1, 2, '1/2', 2.5], [1, 2, '2/4', 2.5],
        [1, 4, '1/4', 4.25], // the last beat of a bar: towards the next bar's first
        [3, 2, '1/2', 9.75],
        [3, 3, '1/2', 10.5], // towards the next bar's first, 11
        [4, 2, null, 13],
        [4, 2, '1/2', 13.5], // the last beat of the sheet: towards the end
        [4, 1, '0/3', 11],
    ];
    for (const [bar, beat, at, expected] of moments) {
        it(`moments: ${bar} ${beat} ${at}`, () => assert.equal(times.moment(new SheetPlace(bar, beat, at)), expected));
    }

    for (const [bar, beat] of [[5, 1], [1, 5], [4, 3]] as const) {
        it(`places outside the bars have no moment: ${bar} ${beat}`, () => {
            assert.equal(times.moment(new SheetPlace(bar, beat)), null);

            // and the sheet still renders, the node with no time
            const html = sheet(times, chord('Am', new SheetPlace(bar, beat)), new NodeText(' '), chord('C', new SheetPlace(1, 1))).asHtml(settings());
            assert.ok(html.includes('chord-data=\'[!0!]m\'>'));
            assert.ok(html.includes('chord-data=\'[!3!]\' data-time=\'1\'>'));
        });
    }

    it('a place without bars has no moment', () => {
        assert.ok(!sheet(null, chord('Am', new SheetPlace(1, 1))).asHtml(settings()).includes('data-time'));
    });

    // A place that cannot be resolved gives its node no time; the sheet is read, rendered and written back as it came.
    const bad: [number, number, string | null][] = [
        [0, 1, null], [1, 0, null], [-2, 1, null], [1, 1, '1'], [1, 1, '1/1'], [1, 1, '3/2'], [1, 1, '-1/2'], [1, 1, '1/0'], [1, 1, 'x'],
    ];
    for (const [bar, beat, at] of bad) {
        it(`bad places have no time: ${bar} ${beat} ${at}`, () => {
            const place = new SheetPlace(bar, beat, at);
            assert.equal(times.moment(place), null);

            const value = sheet(times, chord('Am', place), new NodeText(' '), new NodeMark({ meter: true, place }), new NodeText(' '), chord('C', new SheetPlace(1, 2)));
            const json = serialize(value, NodeHtml.json);
            const back = deserialize(json, NodeHtml.json, defaultJsonSerializerOptions);
            assert.equal(serialize(back, NodeHtml.json), json);
            assert.ok(place.equals(back.children[0]!.nodeChord!.place));

            const html = back.asHtml(settings());
            assert.ok(html.includes('chord-data=\'[!0!]m\'>'));
            assert.ok(html.includes('chord-data=\'[!3!]\' data-time=\'2\'>'));
            assert.ok(!html.includes('sheet-mark-time-signature'));
        });
    }

    it('a place beside a time has no time', () => {
        const html = sheet(times, chord('Am', new SheetPlace(1, 1), 1), new NodeText(' '), new NodeMark({ key: new SheetKey('C', false), place: new SheetPlace(1, 1), time: 1 })).asHtml(settings());
        assert.ok(!html.includes('data-time'));
    });

    // A sheet as it comes from elsewhere, with "+" and Cyrillic written as they are, read and written back with camel case
    // names (the .NET side uses the web defaults with the default encoder). Written back it means the same; the first
    // writing escapes those characters, then it stays.
    it('foreign text round trip', () => {
        const json = '{"name":"pre","classes":null,"times":{"end":4,"bars":[{"beats":[0,1,2,3],"time_sig":"2+2+3/8"}]},"children":[{"nodeText":{"text":"Привет, мир +","time":0.5}}]}';
        const value = deserialize(json, NodeHtml.json, camelCaseJsonSerializerOptions);
        const written = serialize(value, NodeHtml.json, camelCaseJsonSerializerOptions);
        assert.notEqual(written, json);
        assert.ok(written.includes('\\u002B'));
        assert.ok(written.includes('\\u041F'));
        const again = deserialize(written, NodeHtml.json, camelCaseJsonSerializerOptions);
        assert.equal(again.children[0]!.nodeText!.text, 'Привет, мир +');
        assert.equal(again.times!.bars[0]!.timeSignature, '2+2+3/8');
        assert.equal(serialize(again, NodeHtml.json, camelCaseJsonSerializerOptions), written);
    });

    it('a mixed sheet', () => {
        const value = sheet(
            times,
            new NodeMark({ key: new SheetKey('A', true), meter: true, place: new SheetPlace(1, 1) }),
            new NodeText('\n'),
            chord('Am', new SheetPlace(1, 1)),
            new NodeText('    '),
            chord('E7', new SheetPlace(2, 3, '1/2')),
            new NodeText('\n'),
            new NodeText('hello ', 1.1),
            new NodeText('world', 7.4));

        const html = value.asHtml(settings());
        assert.ok(html.includes('<span class=\'sheet-mark\' style=\'white-space: nowrap\' data-time=\'1\'>'));
        assert.ok(html.includes('chord-data=\'[!0!]m\' data-time=\'1\'>'));
        assert.ok(html.includes('data-time=\'7.5\'>E'));
        assert.ok(html.includes('<span data-time=\'1.1\'>hello </span>'));
        assert.ok(html.includes('<span data-time=\'7.4\'>world</span>'));

        // the line of the chords over the words takes the first moment in it
        assert.ok(html.includes('<span class=\'chords-line chords-double-line\' data-time=\'1\'>'));

        // transposed, the moments stay
        const moments = (x: string) => x.match(/data-time='[^']*'/g);
        assert.deepEqual(moments(html), moments(value.asHtml(settings(5))));
    });

    const meters: [number, string, string][] = [
        [1, 'aria-label=\'4/4\'', '>4</text>'],
        [2, 'aria-label=\'4/4\'', '>4</text>'], // held from the bar before
        [3, 'aria-label=\'2+2+3/8\'', '>2+2+3</text>'],
        [4, 'data-time-signature=\'free\'', '<i>senza misura</i>'],
    ];
    for (const [bar, label, drawn] of meters) {
        it(`the meter of the bar: ${bar}`, () => {
            const html = sheet(times, new NodeMark({ meter: true, place: new SheetPlace(bar, 1) })).asHtml(settings());
            assert.ok(html.includes(label));
            assert.ok(html.includes(drawn));
        });
    }

    it('the meter without bars shows nothing', () => {
        const html = sheet(null, new NodeMark({ meter: true, key: new SheetKey('C', false), time: 2 })).asHtml(settings());
        assert.ok(!html.includes('sheet-mark-time-signature'));
        assert.ok(html.includes('data-key=\'C\''));
    });

    it('the text form has no marks', () => {
        const value = sheet(times, new NodeMark({ meter: true, place: new SheetPlace(1, 1) }), new NodeText('\n'), chord('Am', new SheetPlace(1, 1)), new NodeText('\nla la'));
        assert.equal(value.asOneShelf(settings()), sheet(null, chord('Am'), new NodeText('\nla la')).asOneShelf(settings()));
    });

    it('round trip', () => {
        const value = sheet(
            times,
            new NodeMark({ meter: true, place: new SheetPlace(3, 1) }),
            new NodeText('\n'),
            chord('Am', new SheetPlace(1, 2, '1/2')),
            chord('C', new SheetPlace(1, 3)),
            new NodeText('\n'),
            new NodeText('word', 1.5));

        const json = serialize(value, NodeHtml.json);
        assert.ok(json.includes('"time_sig":"free"'));
        assert.ok(json.includes('"Place":{"Bar":1,"Beat":2,"At":"1/2"}'));
        assert.ok(json.includes('"Place":{"Bar":1,"Beat":3}'));
        assert.ok(json.includes('"Meter":true'));

        const back = deserialize(json, NodeHtml.json, defaultJsonSerializerOptions);
        assert.equal(serialize(back, NodeHtml.json), json);
        assert.equal(back.times!.bars[2]!.timeSignature, '2+2+3/8');
        assert.deepEqual(back.times!.bars[2]!.beats, [9, 9.5, 10]);
        assert.equal(back.times!.end, 14);
        assert.equal(back.asHtml(settings()), value.asHtml(settings()));

        // the names as the source contract writes them (camel case), "time_sig" as it is
        const camelJson = serialize(value, NodeHtml.json, camelCaseJsonSerializerOptions);
        assert.ok(camelJson.includes('"times":{"end":14,"bars":[{"beats":[1,2,3,4],"time_sig":"4/4"}'));
        assert.ok(camelJson.includes('"place":{"bar":1,"beat":2,"at":"1/2"}'));
        assert.ok(camelJson.includes('"meter":true'));
        assert.equal(serialize(deserialize(camelJson, NodeHtml.json, camelCaseJsonSerializerOptions), NodeHtml.json, camelCaseJsonSerializerOptions), camelJson);
    });

    it('cloning and splitting keep places', () => {
        // the chords over the words: the layout clones the chords and moves them into the words' line
        const value = sheet(times, chord('Am', new SheetPlace(2, 1)), new NodeText('\nla la'));
        assert.ok(value.asHtml(settings()).includes('data-time=\'5\'>A'));

        value.fix0_Compress();
        assert.ok(new SheetPlace(2, 1).equals(value.children[0]!.nodeChord!.place));
    });
});
