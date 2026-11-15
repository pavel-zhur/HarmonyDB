// Runs the cases through the TypeScript ports and compares with what the reference program (.NET) wrote.
//   npx vite-node tools/dotnet-reference/compare.ts -- <cases.jsonl...> --dotnet <outputs.jsonl...> [--report <file>] [--expected <file>]
// Several case files are taken in order, as one run (the reference program is run on their concatenation); the
// outputs are read line by line, in the order of the cases (they can be large).
// --report writes every difference; --expected writes the cases with the .NET outputs, for the committed check.

process.env.TZ = 'Europe/Berlin';

import { createReadStream, readFileSync, writeFileSync } from 'node:fs';
import { createInterface } from 'node:readline';
import { compareOutputs, type Difference } from '../../src/lib/dotnetReference/compare';
import { runCase } from '../../src/lib/dotnetReference/dispatch';

const args = process.argv.slice(2).filter(x => x !== '--');
const caseFiles: string[] = [];
const outputFiles: string[] = [];
let report: string | null = null;
let expectedFile: string | null = null;
for (let i = 0, target = caseFiles; i < args.length; i++) {
    const arg = args[i]!;
    if (arg === '--dotnet') target = outputFiles;
    else if (arg === '--report') report = args[++i]!;
    else if (arg === '--expected') expectedFile = args[++i]!;
    else target.push(arg);
}

const cases = caseFiles.flatMap(f => readFileSync(f, 'utf8').split('\n').filter(x => x.trim() !== '').map(x => JSON.parse(x))) as { id: string; suite: string; input: unknown }[];

async function* outputLines(): AsyncGenerator<string> {
    for (const file of outputFiles) {
        for await (const line of createInterface({ input: createReadStream(file, 'utf8'), crlfDelay: Infinity })) {
            if (line.trim() !== '') yield line;
        }
    }
}

const bySuite = new Map<string, { total: number; failed: number }>();
const lines: string[] = [];
const expectedLines: string[] = [];
let failed = 0;
let index = 0;
const started = Date.now();
for await (const line of outputLines()) {
    const c = cases[index++];
    // The output text as written is kept for --expected (JSON.stringify would lose the sign of a negative zero).
    const prefix = /^\{"id":("(?:[^"\\]|\\.)*"),"out":/.exec(line);
    if (!c || !prefix || !line.endsWith('}') || JSON.parse(prefix[1]!) !== c.id) throw new Error(`The output line ${index} is not for the case ${c?.id}.`);
    const outputText = line.slice(prefix[0].length, -1);
    const expected: unknown = JSON.parse(outputText);
    if (process.env.TRACE_CASES) writeFileSync(process.env.TRACE_CASES, c.id);
    const actual = runCase(c.suite, c.input);
    const differences: Difference[] = compareOutputs(expected, actual);
    const stats = bySuite.get(c.suite) ?? { total: 0, failed: 0 };
    stats.total++;
    if (differences.length > 0) {
        stats.failed++;
        failed++;
        lines.push(`## ${c.id} ${JSON.stringify(c.input).slice(0, 400)}`);
        for (const d of differences) lines.push(`  ${d.path}\n    .NET: ${JSON.stringify(d.expected)?.slice(0, 600)}\n    TS:   ${JSON.stringify(d.actual)?.slice(0, 600)}`);
    }

    bySuite.set(c.suite, stats);
    if (expectedFile) expectedLines.push(`{"id":${JSON.stringify(c.id)},"suite":${JSON.stringify(c.suite)},"input":${JSON.stringify(c.input)},"expected":${outputText}}`);
}

if (index !== cases.length) throw new Error(`${cases.length} cases, ${index} outputs.`);
for (const [suite, stats] of bySuite) console.log(`${suite.padEnd(8)} ${String(stats.total).padStart(6)} cases, ${stats.failed} differ`);
console.log(`${cases.length} cases, ${failed} differ (${((Date.now() - started) / 1000).toFixed(1)} s).`);
if (report) writeFileSync(report, lines.join('\n') + '\n');
else console.log(lines.slice(0, 200).join('\n'));
if (expectedFile) writeFileSync(expectedFile, expectedLines.join('\n') + '\n');
