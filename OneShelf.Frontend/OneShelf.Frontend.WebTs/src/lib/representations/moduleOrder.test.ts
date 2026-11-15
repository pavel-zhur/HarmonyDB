// The node classes import each other; whichever node module is loaded first, all of them must work (see internal.ts).

import { describe, expect, it, vi } from 'vitest';

const entries = {
    NodeBase: () => import('./NodeBase'),
    NodeCollectionBase: () => import('./NodeCollectionBase'),
    NodeChild: () => import('./NodeChild'),
    NodeText: () => import('./NodeText'),
    NodeNote: () => import('./NodeNote'),
    NodeChord: () => import('./NodeChord'),
    NodeLine: () => import('./NodeLine'),
    NodeHtml: () => import('./NodeHtml'),
    internal: () => import('./internal'),
    index: () => import('./index'),
};

describe('module order', () => {
    for (const [name, load] of Object.entries(entries)) {
        it(`${name} first`, async () => {
            vi.resetModules();
            await load();
            const { NodeHtml, NodeChord, NodeText, RepresentationSettings } = await import('./index');
            const html = new NodeHtml({ name: 'pre', children: [new NodeChord({ children: [new NodeText('Am').asChild()] }).asChild(), new NodeText(' \nla').asChild()] });
            expect(html.asChords(new RepresentationSettings())).toEqual(['[!0!]m']);
            expect(html.asHtml(new RepresentationSettings())).toContain('>Am</b>');
        });
    }
});
