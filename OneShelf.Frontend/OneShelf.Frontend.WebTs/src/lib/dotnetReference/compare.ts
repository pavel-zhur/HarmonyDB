// Compares what dispatch.ts returns with what the reference program wrote: exact, including the sign of zero;
// a missing member is the same as null (System.Text.Json.Nodes writes null members, JavaScript may leave them out).

export interface Difference {
    readonly path: string;
    readonly expected: unknown;
    readonly actual: unknown;
}

export function compareOutputs(expected: unknown, actual: unknown, path = '$', differences: Difference[] = [], limit = 20): Difference[] {
    if (differences.length >= limit) return differences;
    expected = expected === undefined ? null : expected;
    actual = actual === undefined ? null : actual;

    if (typeof expected === 'number' && typeof actual === 'number') {
        if (!Object.is(expected, actual)) differences.push({ path, expected, actual });
        return differences;
    }

    if (Array.isArray(expected) && Array.isArray(actual)) {
        if (expected.length !== actual.length) {
            differences.push({ path: `${path}.length`, expected: expected.length, actual: actual.length });
        }

        for (let i = 0; i < Math.min(expected.length, actual.length); i++) compareOutputs(expected[i], actual[i], `${path}[${i}]`, differences, limit);
        return differences;
    }

    if (isObject(expected) && isObject(actual)) {
        for (const key of new Set([...Object.keys(expected), ...Object.keys(actual)])) {
            compareOutputs(expected[key], actual[key], `${path}.${key}`, differences, limit);
        }

        return differences;
    }

    if (expected !== actual) differences.push({ path, expected, actual });
    return differences;
}

function isObject(value: unknown): value is Record<string, unknown> {
    return typeof value === 'object' && value !== null && !Array.isArray(value);
}
