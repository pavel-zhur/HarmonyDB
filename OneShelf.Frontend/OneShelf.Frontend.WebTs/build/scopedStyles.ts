// Blazor's CSS isolation, done at build time.
//
// In the original every component X.razor with a style file beside it (X.razor.scss / X.razor.less, compiled to
// X.razor.css) gets a scope "b-xxxxxxxxxx":
// - the Razor compiler adds the attribute b-xxxxxxxxxx="" (as the last attribute) to every HTML element written in
//   X.razor — in its markup and in render fragments written there, but not to child components, and so not to the
//   elements a child component renders;
// - the SDK rewrites X.razor.css so that its rules apply to elements with that attribute (build/rewriteCss.ts) and
//   joins all the rewritten files into OneShelf.Frontend.Web.styles.css, which index.html links.
//
// Here the style file stays beside the component, unchanged, as X.scoped.scss / X.scoped.less, and the component
// X.tsx imports it:
//
//     import './X.scoped.scss';
//
// - scopedJsx (a Babel plugin) adds the attribute to every HTML element (lowercase JSX tag) written in X.tsx, as the
//   last attribute; components (capitalized tags) are left alone;
// - the import itself brings nothing into the JavaScript;
// - scopedStyles (a Vite plugin) serves and emits OneShelf.Frontend.Web.styles.css with the same content as the
//   original's: every scoped style file of src/pages and src/shared, compiled as the original compiles it, rewritten
//   with the scope the original's file has, in the original's order and form.

import fs from 'node:fs';
import path from 'node:path';
import type { PluginObj, types as BabelTypes } from '@babel/core';
import type { Plugin } from 'vite';
import { compileStyle } from './compileStyle';
import { computeCssScope } from './cssScope';
import { addScopeToSelectors } from './rewriteCss';

export const scopedStylesBundleName = 'OneShelf.Frontend.Web.styles.css';

const scopedStyleFilePattern = /\.scoped\.(scss|less)$/;

// The folders of the original whose components have scoped styles, and their places here.
const folders: Record<string, string> = {
    pages: 'Pages',
    shared: 'Shared',
};

export interface ScopedStyleFile {
    file: string;
    /** The original's compiled style file as MSBuild names it, e.g. "Pages\Artists.razor.css". */
    itemSpec: string;
    scope: string;
}

// The style file's place in the original, from its place here: src/pages/X.scoped.scss -> Pages\X.razor.css.
export function describeScopedStyleFile(root: string, file: string): ScopedStyleFile {
    const relative = path.relative(path.join(root, 'src'), file).split(path.sep);
    const folder = relative.length === 2 ? folders[relative[0]!] : undefined;
    const match = /^(.+)\.scoped\.(scss|less)$/.exec(relative[relative.length - 1]!);
    if (folder === undefined || match === null) {
        throw new Error(`${file}: a scoped style file belongs directly in src/pages or src/shared and is named X.scoped.scss or X.scoped.less.`);
    }

    const itemSpec = `${folder}\\${match[1]}.razor.css`;
    return { file, itemSpec, scope: computeCssScope(itemSpec) };
}

export function findScopedStyleFiles(root: string): ScopedStyleFile[] {
    const result: ScopedStyleFile[] = [];
    for (const folder of Object.keys(folders)) {
        const directory = path.join(root, 'src', folder);
        if (!fs.existsSync(directory)) continue;
        for (const name of fs.readdirSync(directory)) {
            if (scopedStyleFilePattern.test(name)) {
                result.push(describeScopedStyleFile(root, path.join(directory, name)));
            }
        }
    }

    return result;
}

// ConcatenateCssFiles: the rewritten files ordered by their full path (ordinal, ignoring case), each preceded by a
// comment with its path, line by line with Windows line breaks (the original is published on Windows).
export async function buildScopedStylesBundle(root: string): Promise<string> {
    const files = findScopedStyleFiles(root).map(x => ({
        ...x,
        relativePath: x.itemSpec.replace(/\.css$/, '.rz.scp.css'),
    }));
    files.sort((a, b) => compareOrdinalIgnoreCase(a.relativePath, b.relativePath));

    let bundle = '';
    for (const file of files) {
        // The compiled file is X.razor.css beside the source.
        const outputFile = file.file.replace(scopedStyleFilePattern, '.razor.css');
        const compiled = await compileStyle(file.file, outputFile);
        const rewritten = addScopeToSelectors(file.file, compiled, file.scope);

        bundle += `/* /${file.relativePath.split('\\').join('/')} */\r\n`;
        for (const line of readLines(rewritten)) {
            bundle += line + '\r\n';
        }
    }

    return bundle;
}

// File.ReadLines: lines ended by "\r\n", "\n" or "\r"; no empty line after a final line break.
function readLines(text: string): string[] {
    const lines = text.split(/\r\n|\n|\r/);
    if (lines[lines.length - 1] === '') lines.pop();
    return lines;
}

function compareOrdinalIgnoreCase(a: string, b: string): number {
    const au = a.toUpperCase();
    const bu = b.toUpperCase();
    return au < bu ? -1 : au > bu ? 1 : 0;
}

export function scopedStyles(): Plugin {
    let root = process.cwd();
    const importPrefix = '\0scoped-style:';
    const importSuffix = '.js';

    return {
        name: 'oneshelf-scoped-styles',
        // Before Vite resolves the import to the style file itself.
        enforce: 'pre',

        configResolved(config) {
            root = config.root;
        },

        resolveId(source, importer) {
            if (!scopedStyleFilePattern.test(source) || importer === undefined) return null;
            const file = path.resolve(path.dirname(importer), source);
            if (!fs.existsSync(file)) {
                this.error(`${importer}: the scoped style file ${source} does not exist.`);
            }

            // Not ending in .scss/.less, so that Vite's own CSS handling leaves it alone.
            return importPrefix + file + importSuffix;
        },

        load(id) {
            if (!id.startsWith(importPrefix)) return null;
            this.addWatchFile(id.substring(importPrefix.length, id.length - importSuffix.length));
            return 'export {};';
        },

        configureServer(server) {
            server.watcher.on('all', (_event, file) => {
                if (scopedStyleFilePattern.test(file)) {
                    server.ws.send({ type: 'full-reload' });
                }
            });

            server.middlewares.use((request, response, next) => {
                if (request.url?.split('?')[0] !== '/' + scopedStylesBundleName) {
                    next();
                    return;
                }

                buildScopedStylesBundle(root).then(css => {
                    response.setHeader('Content-Type', 'text/css; charset=utf-8');
                    response.end(css);
                }, next);
            });
        },

        async generateBundle() {
            this.emitFile({ type: 'asset', fileName: scopedStylesBundleName, source: await buildScopedStylesBundle(root) });
        },
    };
}

// The Babel plugin: in a component file importing its scoped style file, the scope attribute on every HTML element.
export function scopedJsx(root: string) {
    return ({ types: t }: { types: typeof BabelTypes }): PluginObj => ({
        name: 'oneshelf-scoped-jsx',
        visitor: {
            Program(program, state) {
                const imports = program.get('body').filter(statement =>
                    statement.isImportDeclaration() && scopedStyleFilePattern.test(statement.node.source.value));
                if (imports.length === 0) return;

                const filename = state.filename;
                if (filename === undefined) {
                    throw program.buildCodeFrameError('A scoped style file is imported from code without a file name.');
                }

                const statement = imports[0]!;
                const declaration = statement.node as BabelTypes.ImportDeclaration;
                const expected = `./${path.basename(filename).replace(/\.tsx$/, '')}.scoped.`;
                if (imports.length > 1 || declaration.specifiers.length > 0 || !filename.endsWith('.tsx')
                    || !declaration.source.value.startsWith(expected)) {
                    throw statement.buildCodeFrameError(
                        'A component X.tsx imports its own scoped style file, and only it, as: import \'./X.scoped.scss\'; (or .less).');
                }

                const { scope } = describeScopedStyleFile(root, path.resolve(path.dirname(filename), declaration.source.value));

                program.traverse({
                    JSXOpeningElement(element) {
                        const name = element.node.name;
                        if (t.isJSXIdentifier(name) && /^[a-z]/.test(name.name)) {
                            element.pushContainer('attributes', t.jsxAttribute(t.jsxIdentifier(scope), t.stringLiteral('')));
                        }
                    },
                });
            },
        },
    });
}
