// The fingerings' model (the old frontend's resources/fingerings.json, 7.5 MB: the chords' types, each with the pictures
// of its fingerings on every root) split at build time into what one window needs: a piece per type and root (a few
// pictures, 10–25 KB), and a small index of them. The app imports `virtual:shelf-fingerings` — the index's address —
// and fetches the index, then the one piece of the chord tapped (chords/fingerings.ts). Every file is named by its
// content (assets/fingerings/…), so it never changes under its name; the worker keeps each on first use
// (serviceWorker.ts, `onDemand`). The dev server serves the same files from memory.

import crypto from 'node:crypto';
import fs from 'node:fs';
import type { Plugin } from 'vite';

interface Model {
    Types: { ChordText: string; Notes: Record<string, string[]> }[];
    Pictures: Record<string, string>;
}

const moduleId = 'virtual:shelf-fingerings';
const resolvedId = '\0' + moduleId;

/** The split: file name → content, and the index's name. */
function split(source: string): { files: Map<string, string>; index: string } {
    const model = JSON.parse(fs.readFileSync(source, 'utf8').replace(/^﻿/, '')) as Model;
    const files = new Map<string, string>();
    const name = (content: string, prefix = '') => `assets/fingerings/${prefix}${crypto.createHash('sha256').update(content).digest('hex').substring(0, 16)}.json`;

    // chord's type on C ("Cm7") → root's name in the model ("Cw") → its piece
    const index: Record<string, Record<string, string>> = {};
    for (const type of model.Types) {
        const roots: Record<string, string> = {};
        for (const [root, pictures] of Object.entries(type.Notes)) {
            const content = JSON.stringify(pictures.map(x => model.Pictures[x]).filter(x => x !== undefined));
            const file = name(content);
            files.set(file, content);
            roots[root] = file;
        }
        index[type.ChordText] = roots;
    }

    const content = JSON.stringify(index);
    const file = name(content, 'index-');
    files.set(file, content);
    return { files, index: file };
}

export function fingerings(source: string): Plugin {
    let made: ReturnType<typeof split> | null = null;
    const get = () => (made ??= split(source));

    return {
        name: 'shelf-fingerings',

        resolveId(id) {
            return id === moduleId ? resolvedId : null;
        },

        load(id) {
            if (id !== resolvedId) return null;
            this.addWatchFile(source);
            return `export default ${JSON.stringify('/' + get().index)};`;
        },

        generateBundle() {
            for (const [fileName, content] of get().files) this.emitFile({ type: 'asset', fileName, source: content });
        },

        configureServer(server) {
            server.middlewares.use((request, response, next) => {
                const path = (request.url ?? '').split('?')[0]!.replace(/^\//, '');
                const content = path.startsWith('assets/fingerings/') ? get().files.get(path) : undefined;
                if (content === undefined) return next();
                response.setHeader('Content-Type', 'application/json');
                response.end(content);
            });
        },
    };
}
