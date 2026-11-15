// Compiles the styles listed in compilerconfig.json the way the BuildWebCompiler2022 package (1.14.10) does on Windows.
// That package cannot run on Linux, so this repeats its steps with the same compilers it bundles
// (sass 1.56.1, less 4.1.3, node 16.6.0), the same arguments and the same handling of their output.
// The minified copies (*.min.css) it also writes are not used by the application and are not produced here.
//
// usage: node compile-styles.js <project directory> <node_modules directory>

const fs = require('fs');
const path = require('path');
const { spawnSync } = require('child_process');

const projectDirectory = path.resolve(process.argv[2]);
const modules = path.resolve(process.argv[3]);

const sass = path.join(modules, 'sass', 'sass.js');
const less = path.join(modules, 'less', 'bin', 'lessc');

function run(script, args, cwd) {
    const result = spawnSync(process.execPath, [script, ...args], { cwd, encoding: 'utf8', maxBuffer: 1 << 28 });
    if (result.status !== 0 || result.stderr.trim() !== '') {
        throw new Error(`${path.basename(script)} ${args.join(' ')}\n${result.stderr}`);
    }

    return result.stdout;
}

// The package rewrites every relative url(...) to be relative to the output file.
function adjustRelativePaths(content, inputFile, outputFile) {
    const inputDirectory = path.dirname(inputFile);
    let text = content;
    for (const match of content.matchAll(/url\s*\(\s*(["']?)([^:)]+)\1\s*\)/gi)) {
        const [whole, quote, value] = match;
        if (value.startsWith('/')) continue;

        const parts = value.split('?').filter(x => x !== '');
        const query = parts.length > 1 ? parts.slice(1).join('?') : '';
        const absolute = path.resolve(inputDirectory, parts[0]);
        let relative = path.relative(path.dirname(outputFile), absolute).split(path.sep).join('/');
        if (query !== '') relative += '?' + query;

        text = text.split(whole).join(`url(${quote}${relative}${quote})`);
    }

    return text;
}

const configs = JSON.parse(fs.readFileSync(path.join(projectDirectory, 'compilerconfig.json'), 'utf8').replace(/^﻿/, ''));

for (const config of configs) {
    const inputFile = path.join(projectDirectory, config.inputFile);
    const outputFile = path.join(projectDirectory, config.outputFile);

    let content;
    switch (path.extname(inputFile).toLowerCase()) {
        case '.scss':
            content = run(sass, ['--no-source-map', '--quiet', '--quiet-deps', '--style=expanded', inputFile], projectDirectory);
            break;
        case '.less':
            content = run(less, ['--no-color', '--js', '--rewrite-urls=all', inputFile], path.dirname(inputFile)).trim();
            break;
        default:
            throw new Error(`Unexpected input file: ${config.inputFile}`);
    }

    content = adjustRelativePaths(content, inputFile, outputFile);

    // written as UTF-8 with the byte order mark, as the package does
    fs.writeFileSync(outputFile, '﻿' + content, 'utf8');
    console.log(`Compiled ${config.outputFile}`);
}
