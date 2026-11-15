// Compiles a style file the way the original's build does: the BuildWebCompiler2022 package (1.14.10), driven by the
// original's compilerconfig.json, with the compilers it bundles (sass 1.56.1, less 4.1.3) and the same arguments
// (see docker/frontend-web/compile-styles.js, which repeats that package on Linux).
//
//   sass --no-source-map --quiet --quiet-deps --style=expanded <input>         (its standard output)
//   lessc --no-color --js --rewrite-urls=all <input>                          (its standard output, trimmed)
//
// The compilers are called through their JavaScript API, which gives the same text as their command line
// (the sass command line only adds a line break at the end). Then every relative url(...) is rewritten to be
// relative to the output file, as the package does.

import fs from 'node:fs';
import path from 'node:path';
import { createRequire } from 'node:module';

interface LessApi {
    render(input: string, options: { filename: string; javascriptEnabled: boolean; rewriteUrls: 'all' }): Promise<{ css: string }>;
}

// Both are CommonJS packages.
const require = createRequire(import.meta.url);
const sass = require('sass') as typeof import('sass');
const less = require('less') as LessApi;

export async function compileStyle(inputFile: string, outputFile: string): Promise<string> {
    let content: string;
    switch (path.extname(inputFile).toLowerCase()) {
        case '.scss':
            content = sass.compile(inputFile, { style: 'expanded', sourceMap: false, quietDeps: true, logger: sass.Logger.silent }).css + '\n';
            break;
        case '.less':
            content = (await less.render(fs.readFileSync(inputFile, 'utf8'), {
                filename: path.resolve(inputFile),
                javascriptEnabled: true,
                rewriteUrls: 'all',
            })).css.trim();
            break;
        default:
            throw new Error(`Unexpected style file: ${inputFile}`);
    }

    return adjustRelativePaths(content, inputFile, outputFile);
}

// The package rewrites every relative url(...) to be relative to the output file.
function adjustRelativePaths(content: string, inputFile: string, outputFile: string): string {
    const inputDirectory = path.dirname(inputFile);
    let text = content;
    for (const match of content.matchAll(/url\s*\(\s*(["']?)([^:)]+)\1\s*\)/gi)) {
        const whole = match[0];
        const quote = match[1] ?? '';
        const value = match[2] ?? '';
        if (value.startsWith('/')) continue;

        const parts = value.split('?').filter(x => x !== '');
        const query = parts.length > 1 ? parts.slice(1).join('?') : '';
        const absolute = path.resolve(inputDirectory, parts[0] ?? '');
        let relative = path.relative(path.dirname(outputFile), absolute).split(path.sep).join('/');
        if (query !== '') relative += '?' + query;

        text = text.split(whole).join(`url(${quote}${relative}${quote})`);
    }

    return text;
}
