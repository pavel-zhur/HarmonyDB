// The ports against the real .NET: expected.jsonl holds generated cases with what the C# libraries returned for them
// (tools/dotnet-reference, run in Docker with .NET 9 and invariant globalization, as the site). Here the same cases run
// through the TypeScript ports and must give the same. To regenerate: tools/dotnet-reference/README.md.

process.env.TZ = 'Europe/Berlin';

import { describe, expect, it } from 'vitest';
import expectedText from './expected.jsonl?raw';
import { compareOutputs } from './compare';
import { runCase } from './dispatch';

interface ExpectedCase {
    readonly id: string;
    readonly suite: string;
    readonly input: Record<string, unknown>;
    readonly expected: unknown;
}

const cases = expectedText.split('\n').filter(x => x.trim() !== '').map(x => JSON.parse(x) as ExpectedCase);

/**
 * The differences that cannot be removed (see PORTING.md, Departures): the parts of a case's output left out of the
 * comparison, with the reason.
 */
function withoutDepartures(c: ExpectedCase, output: unknown): unknown {
    if (output === null || typeof output !== 'object') return output;
    const copy = { ...(output as Record<string, unknown>) };
    if (c.suite === 'dotnet' && c.input.f === 'chars') {
        // char.IsUpper follows the Unicode version of the browser (JavaScript's \p{Lu}); it is used only to camelCase
        // the C# property names, which are ASCII.
        delete copy.up;
    }

    if (c.suite === 'dotnet' && c.input.f === 'str' && /^(0001|1[0-8][0-9]{2})-/.test(String(c.input.s))) {
        // Local times before the time zones (local mean time, offsets with seconds): .NET and the browser round the offset differently.
        delete copy.date;
    }

    return copy;
}

describe('the ports give what .NET gives', () => {
    it('runs in the time zone the expected outputs were made in', () => {
        expect(new Date(2024, 5, 1).getTimezoneOffset()).toBe(-120);
        expect(new Date(2024, 0, 1).getTimezoneOffset()).toBe(-60);
    });

    const suites = [...new Set(cases.map(c => c.suite))];
    for (const suite of suites) {
        it(suite, () => {
            const failures: string[] = [];
            for (const c of cases.filter(x => x.suite === suite)) {
                if (c.suite === 'json' && String(c.input.json).includes('9007199254740993')) {
                    // A long beyond 2^53 loses precision in a JavaScript number.
                    continue;
                }

                const differences = compareOutputs(withoutDepartures(c, c.expected), withoutDepartures(c, runCase(c.suite, c.input)), '$', [], 3);
                if (differences.length > 0) {
                    failures.push(`${c.id} ${JSON.stringify(c.input).slice(0, 200)}\n${differences.map(d => `  ${d.path}: .NET ${JSON.stringify(d.expected)?.slice(0, 300)}, TS ${JSON.stringify(d.actual)?.slice(0, 300)}`).join('\n')}`);
                }
            }

            expect(failures.slice(0, 10).join('\n')).toBe('');
        });
    }
});
