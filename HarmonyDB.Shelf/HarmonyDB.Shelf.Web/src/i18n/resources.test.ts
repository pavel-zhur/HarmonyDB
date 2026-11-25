// Every language has every text: the same keys as English, and the plural forms its language needs.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { en } from './en/index.ts';
import { ru } from './ru/index.ts';

type Tree = { [key: string]: string | Tree };

const pluralForms: Record<string, string[]> = { en: ['one', 'other'], ru: ['one', 'few', 'many', 'other'] };
const suffix = /_(zero|one|two|few|many|other)$/;

/** The texts by their full key; a plural's forms gathered under its base key. */
function flatten(tree: Tree, prefix = '', out = new Map<string, Set<string>>()): Map<string, Set<string>> {
    for (const [key, value] of Object.entries(tree)) {
        const full = prefix + key;
        if (typeof value !== 'string') flatten(value, full + '.', out);
        else {
            const match = suffix.exec(full);
            const base = match ? full.slice(0, match.index) : full;
            if (!out.has(base)) out.set(base, new Set());
            if (match) out.get(base)!.add(match[1]!);
        }
    }
    return out;
}

test('ru has the keys of en, en has the keys of ru', () => {
    const a = flatten(en), b = flatten(ru);
    assert.deepEqual([...a.keys()].filter(k => !b.has(k)), []);
    assert.deepEqual([...b.keys()].filter(k => !a.has(k)), []);
});

for (const [name, tree] of [['en', en], ['ru', ru]] as const) {
    test(`${name}: each plural has the forms of the language`, () => {
        for (const [key, forms] of flatten(tree)) {
            if (forms.size === 0) continue;
            assert.deepEqual([...forms].sort(), [...pluralForms[name]!].sort(), key);
        }
    });
}
