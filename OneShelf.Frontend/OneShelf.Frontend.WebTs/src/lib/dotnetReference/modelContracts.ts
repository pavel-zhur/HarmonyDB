// The JSON contracts of the ported models, by the .NET full type name each contract states (the name the reference
// program looks the C# type up by).

import { jsonBytes, type JsonContract } from '../common/dotnet/json/JsonSerializer';

const modules = import.meta.glob(['../api/**/*.ts', '../representations/*.ts', '!../**/*.test.ts'], { eager: true });

function isContract(value: unknown): value is JsonContract<unknown> {
    return typeof value === 'object' && value !== null
        && typeof (value as JsonContract<unknown>).typeName === 'string'
        && typeof (value as JsonContract<unknown>).read === 'function'
        && typeof (value as JsonContract<unknown>).write === 'function';
}

const types = new Map<string, JsonContract<unknown>>();
for (const module of Object.values(modules)) {
    for (const exported of Object.values(module as Record<string, unknown>)) {
        const contract = typeof exported === 'function' ? (exported as { json?: unknown }).json : exported;
        if (isContract(contract) && !types.has(contract.typeName)) types.set(contract.typeName, contract);
    }
}

export const modelContracts = {
    types: types as ReadonlyMap<string, JsonContract<any>>, // eslint-disable-line @typescript-eslint/no-explicit-any
    bytes: jsonBytes,
};
