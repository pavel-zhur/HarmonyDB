// Writes the generated cases as JSON lines.
//   npx vite-node tools/dotnet-reference/cases.ts -- <cases.jsonl> [seed] [scale]

import { writeFileSync } from 'node:fs';
import { generateCases } from '../../src/lib/dotnetReference/generate';

const [path, seed = '1', scale = '1'] = process.argv.slice(2).filter(x => x !== '--');
if (!path) throw new Error('Usage: cases.ts <cases.jsonl> [seed] [scale]');

const cases = generateCases(Number(seed), Number(scale));
writeFileSync(path, cases.map(c => JSON.stringify(c)).join('\n') + '\n');
console.log(`${cases.length} cases written to ${path}.`);
