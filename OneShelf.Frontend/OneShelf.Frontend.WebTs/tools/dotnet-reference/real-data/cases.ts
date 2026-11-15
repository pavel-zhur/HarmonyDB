// Makes comparison cases from a dump of the original site's library (dump.mjs): every song's stored JSON, its node
// tree under several settings with the whole progression pipeline, its chords, searches across songs, the full-text
// search over the titles and artists. The cases contain the real data: write them outside the repository.
//   npx vite-node tools/dotnet-reference/real-data/cases.ts -- <dump.json> <cases.jsonl>
// Then, with the data directory holding cases.jsonl:
//   tools/dotnet-reference/reference.ps1 -DataDir <dir> run cases.jsonl dotnet.jsonl
//   npx vite-node tools/dotnet-reference/compare.ts -- <dir>/cases.jsonl --dotnet <dir>/dotnet.jsonl --report <dir>/report.txt

import { readFileSync, writeFileSync } from 'node:fs';
import { defaultJsonSerializerOptions, deserialize } from '../../../src/lib/common/dotnet/json/JsonSerializer';
import { Chords } from '../../../src/lib/api/HarmonyDB.Source.Api.Model/V1/Chords';
import { RepresentationSettings } from '../../../src/lib/representations';

const [dumpPath, casesPath] = process.argv.slice(2).filter(x => x !== '--');
if (!casesPath) throw new Error('Usage: cases.ts <dump.json> <cases.jsonl>');

const dump = JSON.parse(readFileSync(dumpPath!, 'utf8')) as Record<string, { key: string; value: { contents: string } }[]>;
const chordsItems = Object.entries(dump).filter(([name]) => name.startsWith('IndexedItem-Chords')).flatMap(([, items]) => items);

const cases: string[] = [];
let n = 0;
const add = (suite: string, input: unknown) => cases.push(JSON.stringify({ id: `real-${suite}-${n++}`, suite, input }));

const settingsVariants = [
    { t: 0, a: null, x: false, vw: false, s: 0 },
    { t: 3, a: 0, x: false, vw: false, s: 0 },
    { t: -5, a: 1, x: false, vw: true, s: 31 },
    { t: 1, a: null, x: false, vw: false, s: 3, ops: 'dd' },
    { t: 7, a: null, x: false, vw: false, s: 4, rel: [0, false] },
];

const chordData = new Set<string>();
const songs: { title: string; artists: string[]; chords: string[] }[] = [];
for (const [index, item] of chordsItems.entries()) {
    add('json', { type: 'HarmonyDB.Source.Api.Model.V1.Chords', json: item.value.contents, naming: 'pascal' });
    const chords = deserialize(item.value.contents, Chords.json, defaultJsonSerializerOptions);
    const output = JSON.stringify(JSON.parse(item.value.contents).Output);
    const asChords = chords.output.asChords(new RepresentationSettings());
    for (const c of asChords) chordData.add(c);
    songs.push({ title: chords.title ?? '', artists: chords.artists ?? [], chords: asChords });
    add('html', {
        json: output,
        st: [settingsVariants[index % settingsVariants.length], settingsVariants[(index + 2) % settingsVariants.length]],
        pipeline: true,
        ex: [[0, Math.min(5, asChords.length - 1)], [2, Math.min(12, asChords.length - 1)], [Math.trunc(asChords.length / 2), asChords.length - 1]],
    });
}

for (const s of chordData) add('chord', { s });

// Searches: groups of songs, each searched for phrases taken from its songs.
for (let start = 0; start < songs.length; start += 40) {
    const group = songs.slice(start, start + 40).map(s => s.chords);
    add('search', {
        songs: group,
        top: null,
        ex: group.flatMap((g, i) => g.length > 4 ? [[i, 0, 2], [i, 1, Math.min(4, g.length - 1)]] : []).slice(0, 30),
    });
}

// The full-text search over the titles and artists (the site's library index).
const words = [...new Set(songs.flatMap(s => [s.title, ...s.artists]).flatMap(t => t.split(/[\s,'-]+/)).filter(x => x.length > 1))];
for (let start = 0; start < songs.length; start += 300) {
    const group = songs.slice(start, start + 300);
    add('fts', {
        songs: group.map((s, i) => ({ title: s.title, kw: null, index: i + 1, artists: s.artists.map(a => ({ name: a, syn: [] })), likes: [] })),
        queries: Array.from({ length: 30 }, (_, i) => {
            const w = words[(i * 7919 + start) % words.length] ?? '';
            return i % 3 === 0 ? w.slice(0, 3) : i % 3 === 1 ? w.toUpperCase() : `${w} ${words[(i * 31) % words.length] ?? ''}`;
        }).concat(['', '12', ' 7 ']),
        users: [1],
        frag: group.slice(0, 60).map((s, i) => [words[(i * 13) % words.length]?.slice(0, 4) ?? '', s.title]),
    });
}

writeFileSync(casesPath, cases.join('\n') + '\n');
console.log(`${cases.length} cases written (${chordsItems.length} songs, ${chordData.size} distinct chords).`);
