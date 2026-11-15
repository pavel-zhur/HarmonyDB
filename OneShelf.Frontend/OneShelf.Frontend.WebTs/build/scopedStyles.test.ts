import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { transformSync } from '@babel/core';
import { describe, expect, it } from 'vitest';
import { computeCssScope } from './cssScope';
import { buildScopedStylesBundle, describeScopedStyleFile, findScopedStyleFiles, scopedJsx } from './scopedStyles';

const root = fileURLToPath(new URL('..', import.meta.url));
const original = path.join(root, '..', 'OneShelf.Frontend.Web');

describe('scope', () => {
    it('is "b-" and ten base-36 characters, from the original file\'s path, ignoring its case', () => {
        const scope = computeCssScope('Pages\\Artists.razor.css');
        expect(scope).toMatch(/^b-[0-9a-z]{10}$/);
        expect(computeCssScope('pages\\artists.razor.css')).toBe(scope);
        expect(computeCssScope('Shared\\Artists.razor.css')).not.toBe(scope);
    });

    it('maps the files here to the original\'s', () => {
        const file = describeScopedStyleFile(root, path.join(root, 'src', 'shared', 'CircleOfFifths.scoped.less'));
        expect(file.itemSpec).toBe('Shared\\CircleOfFifths.razor.css');
        expect(() => describeScopedStyleFile(root, path.join(root, 'src', 'components', 'X.scoped.scss'))).toThrow();
    });
});

describe('the scoped style files', () => {
    it('are the original\'s, every one, unchanged', () => {
        const originals = ['Pages', 'Shared'].flatMap(folder => fs.readdirSync(path.join(original, folder))
            .filter(name => /\.razor\.(scss|less)$/.test(name))
            .map(name => ({ folder, name })));
        const here = findScopedStyleFiles(root);

        expect(here.map(x => x.itemSpec).sort()).toEqual(
            originals.map(x => `${x.folder}\\${x.name.replace(/\.(scss|less)$/, '.css')}`).sort());

        for (const { folder, name } of originals) {
            const copy = path.join(root, 'src', folder.toLowerCase(), name.replace('.razor.', '.scoped.'));
            expect(fs.readFileSync(copy), copy).toEqual(fs.readFileSync(path.join(original, folder, name)));
        }
    });
});

describe('the bundle', async () => {
    const bundle = await buildScopedStylesBundle(root);
    const files = findScopedStyleFiles(root);
    const scopeOf = (itemSpec: string) => files.find(x => x.itemSpec === itemSpec)!.scope;

    it('has every file, in the original\'s order, under its comment, with Windows line breaks', () => {
        const comments = [...bundle.matchAll(/^\/\* (.*) \*\/$/gm)].map(x => x[1]);
        expect(comments).toEqual([
            '/Pages/Artists.razor.rz.scp.css',
            '/Pages/Collection.razor.rz.scp.css',
            '/Pages/Collective.razor.rz.scp.css',
            '/Pages/Fingerings.razor.rz.scp.css',
            '/Pages/HowToInstall.razor.rz.scp.css',
            '/Pages/Illustrations.razor.rz.scp.css',
            '/Pages/Index.razor.rz.scp.css',
            '/Pages/Print.razor.rz.scp.css',
            '/Pages/Progressions.razor.rz.scp.css',
            '/Pages/Tuner.razor.rz.scp.css',
            '/Pages/VisitedChords.razor.rz.scp.css',
            '/Pages/VisitedSearch.razor.rz.scp.css',
            '/Shared/ChordsList.razor.rz.scp.css',
            '/Shared/ChordsView.razor.rz.scp.css',
            '/Shared/CircleOfFifths.razor.rz.scp.css',
            '/Shared/FingeringIndicator.razor.rz.scp.css',
            '/Shared/IllustrationsView.razor.rz.scp.css',
            '/Shared/MainLayout.razor.rz.scp.css',
            '/Shared/MenuTop.razor.rz.scp.css',
            '/Shared/SearchHighlighting.razor.rz.scp.css',
            '/Shared/SearchResultsInternet.razor.rz.scp.css',
            '/Shared/SearchResultsLocal.razor.rz.scp.css',
        ]);
        expect(bundle.replace(/\r\n/g, '')).not.toMatch(/[\r\n]/);
        expect(bundle.endsWith('}\r\n')).toBe(true);
    });

    it('scopes every selector of every rule', () => {
        const css = bundle.replace(/\/\*[\s\S]*?\*\//g, '');
        for (const match of css.matchAll(/([^{}]+)\{[^{}]*\}/g)) {
            for (const selector of match[1]!.split(',')) {
                expect(selector.trim(), selector).toMatch(/\[b-[0-9a-z]{10}\]/);
            }
        }
    });

    it('scopes as Blazor does', () => {
        const fingerings = scopeOf('Pages\\Fingerings.razor.css');
        expect(bundle).toContain(`div.pic[${fingerings}] {\r\n  padding: 1em 0.5em;`);
        expect(bundle).toContain(`div.pic > span[${fingerings}] {`);

        const chordsView = scopeOf('Shared\\ChordsView.razor.css');
        expect(bundle).toContain(`.songsconstants .btn-check:checked + .btn-outline-secondary[${chordsView}], .songsconstants .btn-outline-secondary:hover[${chordsView}] {`);

        const circle = scopeOf('Shared\\CircleOfFifths.razor.css');
        expect(bundle).toContain(`.circle-of-fifths[${circle}]::after {`);
        expect(bundle).toContain(`.key > *[${circle}] {`);
        expect(bundle).toContain(`.key:nth-child(12)[${circle}]::before {`);

        const print = scopeOf('Pages\\Print.razor.css');
        expect(bundle).toContain(`a:link.success[${print}], a:visited.success[${print}], a.success[${print}] {`);

        const chordsList = scopeOf('Shared\\ChordsList.razor.css');
        expect(bundle).toContain(`  /* ReSharper disable once InvalidValue */\r\n  opacity: 28%;`);
        expect(bundle).toContain(`.chordslist .heart-0[${chordsList}], .chordslist heart-1[${chordsList}],`);
    });
});

describe('the scope attribute on elements', () => {
    const transform = (file: string, code: string) => transformSync(code, {
        filename: path.join(root, file),
        babelrc: false,
        configFile: false,
        parserOpts: { plugins: ['jsx', 'typescript'] },
        plugins: [scopedJsx(root)],
    })!.code!;

    it('is added, last, to every HTML element of a component importing its scoped style file, and not to components', () => {
        const scope = computeCssScope('Pages\\Artists.razor.css');
        const code = transform('src/pages/Artists.tsx', `
import './Artists.scoped.scss';
import { Child } from '../shared/Child';
export function Artists({ x }: { x: string }) {
    const fragment = <span>{x}</span>;
    return <div className="a" {...{ id: x }}><Child body={<b>bold</b>}>{fragment}<svg><path d="" /></svg></Child></div>;
}`);
        expect(code).toContain(`<span ${scope}="">{x}</span>`);
        expect(code).toContain(`<div className="a" {...{
    id: x
  }} ${scope}="">`);
        expect(code).toContain(`<Child body={<b ${scope}="">bold</b>}>`);
        expect(code).toContain(`<svg ${scope}=""><path d="" ${scope}="" /></svg>`);
    });

    it('is not added without the import', () => {
        const code = transform('src/pages/Cache.tsx', 'export function Cache() { return <div />; }');
        expect(code).toContain('<div />');
    });

    it('needs the component\'s own style file', () => {
        expect(() => transform('src/pages/Cache.tsx', `import './Artists.scoped.scss'; export function Cache() { return <div />; }`))
            .toThrow(/imports its own scoped style file/);
    });
});
