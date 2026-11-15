// HarmonyDB.Index.Analysis.Tests/LoopNormalizationTests.cs

import { describe, expect, it } from 'vitest';
import { range, repeat } from '../common/dotnet/Linq';
import { Out } from '../common/dotnet/Out';
import type { ILogger } from '../common/dotnet/Logging';
import type { ChordType } from './Models/ChordType';
import { CompactHarmonyMovement } from './Models/CompactV1/CompactHarmonyMovement';
import { IndexExtractor } from './Services/IndexExtractor';
import { PathsSearcher } from './Services/PathsSearcher';
import { serializeLoop } from './Tools/LoopSerializationExtensions';

const output: string[] = [];
const logger: ILogger = {
    logInformation: message => output.push(message),
    logWarning: message => output.push(message),
    logError: (_, message) => output.push(message),
};

const indexExtractor = new IndexExtractor(new PathsSearcher());

/** Random.Shared.Next(minValue, maxValue). */
function next(minValue: number, maxValue: number): number {
    return minValue + Math.floor(Math.random() * (maxValue - minValue));
}

describe('LoopNormalizationTests', () => {
    it.each([2, 3])('NormalizationIsCorrectForMultipleOptions(%i)', repetitions => {
        let minNormalizationShift = 2147483647;
        let maxNormalizationShift = -2147483648;
        let minInvertedShift = 2147483647;
        let maxInvertedShift = -2147483648;
        const length = 3;
        for (let i = 0; i < 100; i++) {
            const x = range(0, length)
                .map(() => new CompactHarmonyMovement({
                    toType: next(0, 8) as ChordType,
                    fromType: next(0, 8) as ChordType,
                    rootDelta: next(0, 12),
                }));
            const movements = range(0, repetitions - 1)
                .map(() => x)
                .reduce((x, y) => [...x, ...y], x);

            const loop = movements;

            const normalizationShift = new Out<number>();
            const invariants = new Out<number>();
            indexExtractor.getNormalizedProgression(loop, normalizationShift, invariants);
            const invertedShift = indexExtractor.invertNormalizationShift(normalizationShift.value, loop.length);
            minNormalizationShift = Math.min(minNormalizationShift, normalizationShift.value);
            maxNormalizationShift = Math.max(maxNormalizationShift, normalizationShift.value);
            minInvertedShift = Math.min(minInvertedShift, invertedShift);
            maxInvertedShift = Math.max(maxInvertedShift, invertedShift);
            expect(invariants.value).toBe(repetitions);
        }

        // these assertions may very rarely fail, when an edge case never happens in 1000 repetitions. Almost impossible.
        expect(minNormalizationShift).toBe(0);
        expect(minInvertedShift).toBe(0);
        expect(maxNormalizationShift).toBe(length - 1);
        expect(maxInvertedShift).toBe(length * repetitions - 1);
    });

    it('NormalizedIsStable1000', () => {
        const length = 10;
        const newLoop = (): { loop1: readonly CompactHarmonyMovement[]; loop2: readonly CompactHarmonyMovement[] } => {
            const x = range(0, length).map(() => new CompactHarmonyMovement({
                toType: next(0, 8) as ChordType,
                fromType: next(0, 8) as ChordType,
                rootDelta: next(0, 12),
            }));
            const movements = [...x, ...x];

            const loop = movements.slice(0, length);

            const start = next(0, length);
            return { loop1: loop, loop2: movements.slice(start, start + length) };
        };

        const loops = repeat(0, 1000).map(() => newLoop());

        let minNormalizationShift = 2147483647;
        let maxNormalizationShift = -2147483648;
        let minInvertedShift = 2147483647;
        let maxInvertedShift = -2147483648;
        for (const { loop1, loop2 } of loops) {
            const normalizationShift = new Out<number>();
            const progression1 = indexExtractor.getNormalizedProgression(loop1, normalizationShift);
            let invertedShift = indexExtractor.invertNormalizationShift(normalizationShift.value, progression1.length);
            minNormalizationShift = Math.min(minNormalizationShift, normalizationShift.value);
            maxNormalizationShift = Math.max(maxNormalizationShift, normalizationShift.value);
            minInvertedShift = Math.min(minInvertedShift, invertedShift);
            maxInvertedShift = Math.max(maxInvertedShift, invertedShift);

            const progression2 = indexExtractor.getNormalizedProgression(loop2, normalizationShift);
            invertedShift = indexExtractor.invertNormalizationShift(normalizationShift.value, progression1.length);
            minNormalizationShift = Math.min(minNormalizationShift, normalizationShift.value);
            maxNormalizationShift = Math.max(maxNormalizationShift, normalizationShift.value);
            minInvertedShift = Math.min(minInvertedShift, invertedShift);
            maxInvertedShift = Math.max(maxInvertedShift, invertedShift);
            const serialized1 = serializeLoop(progression1);
            const serialized2 = serializeLoop(progression2);
            if (serialized1 !== serialized2) {
                logger.logInformation(loop1.map(x => x.rootDelta).join(', '));
                logger.logInformation(loop2.map(x => x.rootDelta).join(', '));
                logger.logInformation(progression1.map(x => x.rootDelta).join(', '));
                logger.logInformation(progression2.map(x => x.rootDelta).join(', '));
                expect.fail();
            }
        }

        // these assertions may very rarely fail, when an edge case never happens in 1000 repetitions. Almost impossible.
        expect(minNormalizationShift).toBe(0);
        expect(minInvertedShift).toBe(0);
        expect(maxNormalizationShift).toBe(length - 1);
        expect(maxInvertedShift).toBe(length - 1);
    });
});
