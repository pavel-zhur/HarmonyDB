// The rewriting of a component's style sheet that Blazor's CSS isolation does at build time:
// the RewriteCss task of the .NET SDK (src/StaticWebAssetsSdk/Tasks/ScopedCss/RewriteCss.cs, .NET 9).
//
// - In every selector the scope attribute "[b-xxxxxxxxxx]" is inserted after the last compound selector, before its
//   first pseudo-element if it has one (including the single-colon :before, :after, :first-letter, :first-line).
// - The first "::deep" of a selector ends the part that is looked at and is removed; when nothing comes before it,
//   the scope attribute takes its place.
// - The names of @keyframes are suffixed with "-b-xxxxxxxxxx", and so are those names where "animation" and
//   "animation-name" declarations use them.
// - @import is an error.
//
// The SDK parses the file with Microsoft.Css.Parser and edits the original text at the positions it finds, leaving
// everything else (whitespace, comments) as it is; this does the same. Constructs that the style files of the original
// do not use and whose treatment by that parser is not certain (other at-rules, nested rules) are reported as errors
// rather than guessed.

type Edit =
    | { kind: 'scope'; position: number }
    | { kind: 'keyframes'; position: number }
    | { kind: 'delete'; position: number; length: number };

interface Part {
    start: number;
    end: number;
}

interface Compound {
    start: number;
    end: number;
    parts: Part[];
}

const deepCombinatorText = '::deep';

export const importNotAllowedErrorMessage =
    '{0}({1},{2}): @import rules are not supported within scoped CSS files because the loading order would be undefined. ' +
    '@import may only be placed in non-scoped CSS files.';

export function addScopeToSelectors(filePath: string, text: string, cssScope: string): string {
    const rewriter = new Rewriter(filePath, text);

    // Before anything else, the full set of keyframe names declared in the document.
    rewriter.parseRules(0, text.length, false);
    if (rewriter.importErrors.length > 0) {
        throw new Error(rewriter.importErrors.join('\n'));
    }

    rewriter.parseRules(0, text.length, true);

    let result = '';
    let previous = 0;
    for (const edit of rewriter.edits) {
        if (edit.position < previous) {
            throw new Error(`${filePath}: internal error, the edits are out of order.`);
        }

        result += text.substring(previous, edit.position);
        previous = edit.position;
        switch (edit.kind) {
            case 'scope':
                result += `[${cssScope}]`;
                break;
            case 'keyframes':
                result += `-${cssScope}`;
                break;
            case 'delete':
                previous += edit.length;
                break;
        }
    }

    return result + text.substring(previous);
}

class Rewriter {
    readonly edits: Edit[] = [];
    readonly importErrors: string[] = [];
    private readonly keyframes = new Set<string>();

    constructor(private readonly filePath: string, private readonly text: string) {
    }

    parseRules(start: number, end: number, edit: boolean): void {
        const text = this.text;
        let i = start;
        while (true) {
            i = this.skipWhitespaceAndComments(i, end);
            if (i >= end) return;

            if (text[i] === '@') {
                i = this.parseAtRule(i, end, edit);
                continue;
            }

            const open = this.findTopLevel(i, end, '{;}');
            if (open === -1 || text[open] !== '{') {
                this.unsupported(i, 'a rule without a block');
            }

            if (edit) this.parseSelectors(i, open);
            const close = this.matchingClose(open, end);
            this.parseDeclarations(open + 1, close, edit);
            i = close + 1;
        }
    }

    private parseAtRule(start: number, end: number, edit: boolean): number {
        const text = this.text;
        const name = /^@(-?[A-Za-z_][\w-]*)/.exec(text.substring(start, Math.min(end, start + 100)))?.[1];
        if (name === undefined) {
            this.unsupported(start, 'an at-rule without a name');
        }

        const lower = name.toLowerCase();
        const nameEnd = start + 1 + name.length;
        if (lower === 'import') {
            if (!edit) {
                const location = this.getLocation(start);
                this.importErrors.push(importNotAllowedErrorMessage
                    .replace('{0}', this.filePath)
                    .replace('{1}', String(location.line))
                    .replace('{2}', String(location.character)));
            }
        }

        // A statement at-rule (@charset, ...) has no selectors and stays as it is.
        const stop = this.findTopLevel(nameEnd, end, '{;}');
        if (stop === -1 || text[stop] !== '{') {
            return stop === -1 || text[stop] === '}' ? (stop === -1 ? end : stop) : stop + 1;
        }

        const close = this.matchingClose(stop, end);
        switch (lower) {
            case 'keyframes': {
                const identifier = this.skipWhitespaceAndComments(nameEnd, stop);
                const identifierEnd = this.readIdentifier(identifier, stop);
                if (identifierEnd === identifier) {
                    this.unsupported(identifier, '@keyframes without an identifier name');
                }

                if (edit) {
                    this.edits.push({ kind: 'keyframes', position: identifierEnd });
                } else {
                    this.keyframes.add(text.substring(identifier, identifierEnd));
                }

                break;
            }
            case 'media':
            case 'supports':
                this.parseRules(stop + 1, close, edit);
                break;
            case 'font-face':
            case 'page':
                this.parseDeclarations(stop + 1, close, edit);
                break;
            default:
                this.unsupported(start, `the at-rule @${name}`);
        }

        return close + 1;
    }

    private parseDeclarations(start: number, end: number, edit: boolean): void {
        const text = this.text;
        let i = start;
        while (i < end) {
            const stop = this.findTopLevel(i, end, ';{');
            if (stop !== -1 && text[stop] === '{') {
                this.unsupported(stop, 'a nested rule');
            }

            const declarationEnd = stop === -1 ? end : stop;
            if (edit) this.parseDeclaration(i, declarationEnd);
            i = declarationEnd + 1;
        }
    }

    private parseDeclaration(start: number, end: number): void {
        const colon = this.findTopLevel(start, end, ':');
        if (colon === -1) return;

        const nameStart = this.skipWhitespaceAndComments(start, colon);
        const nameEnd = this.readIdentifier(nameStart, colon);
        const propertyName = this.text.substring(nameStart, nameEnd);
        if (propertyName !== 'animation' && propertyName !== 'animation-name') return;

        // Any identifier among the value's tokens may be the animation name; only the names of the keyframes
        // declared in this document are rewritten.
        const text = this.text;
        let i = colon + 1;
        while (true) {
            i = this.skipWhitespaceAndComments(i, end);
            if (i >= end) return;

            const c = text[i]!;
            if (c === '"' || c === '\'') {
                i = this.skipString(i, end);
            } else if (/[0-9]/.test(c) || ((c === '.' || c === '+' || c === '-') && /[0-9]/.test(text[i + 1] ?? '')) || (c === '-' && text[i + 1] === '.' && /[0-9]/.test(text[i + 2] ?? ''))) {
                i++;
                while (i < end && /[\w.%-]/.test(text[i]!)) i++;
            } else {
                const identifierEnd = this.readIdentifier(i, end);
                if (identifierEnd === i) {
                    i++;
                } else if (text[identifierEnd] === '(') {
                    i = this.matchingClose(identifierEnd, end) + 1;
                } else {
                    if (this.keyframes.has(text.substring(i, identifierEnd))) {
                        this.edits.push({ kind: 'keyframes', position: identifierEnd });
                    }

                    i = identifierEnd;
                }
            }
        }
    }

    private parseSelectors(start: number, end: number): void {
        let i = start;
        while (true) {
            const comma = this.findTopLevel(i, end, ',');
            this.parseSelector(i, comma === -1 ? end : comma);
            if (comma === -1) return;
            i = comma + 1;
        }
    }

    private parseSelector(start: number, end: number): void {
        const text = this.text;
        const compounds: Compound[] = [];
        let combinatorPending = false;
        let i = start;
        while (true) {
            i = this.skipWhitespaceAndComments(i, end);
            if (i >= end) break;

            const c = text[i]!;
            if (c === '>' || c === '+' || c === '~') {
                if (compounds.length === 0 || combinatorPending) {
                    this.unsupported(i, 'a selector with a leading or doubled combinator');
                }

                combinatorPending = true;
                i++;
                continue;
            }

            const compound = this.readCompound(i, end);
            compounds.push(compound);
            combinatorPending = false;
            i = compound.end;
        }

        if (compounds.length === 0) {
            this.unsupported(start, 'an empty selector');
        }

        if (combinatorPending) {
            this.unsupported(start, 'a selector ending with a combinator');
        }

        const deep = compounds.findIndex(x => text.startsWith(deepCombinatorText, x.start));
        const last = deep === -1 ? compounds[compounds.length - 1] : compounds[deep - 1];
        if (last !== undefined) {
            this.edits.push({ kind: 'scope', position: this.findPositionToInsert(last) });
        } else if (deep !== -1) {
            this.edits.push({ kind: 'scope', position: compounds[deep]!.start });
        }

        if (deep !== -1) {
            this.edits.push({ kind: 'delete', position: compounds[deep]!.start, length: deepCombinatorText.length });
        }
    }

    // Before the first pseudo-element of the compound selector, otherwise after it.
    private findPositionToInsert(compound: Compound): number {
        for (const part of compound.parts) {
            const partText = this.text.substring(part.start, part.end);
            if (partText.startsWith('::') || isSingleColonPseudoElement(partText)) {
                return part.start;
            }
        }

        return compound.end;
    }

    private readCompound(start: number, end: number): Compound {
        const text = this.text;
        const parts: Part[] = [];
        let i = start;
        while (i < end) {
            const c = text[i]!;
            if (/\s/.test(c) || c === '>' || c === '+' || c === '~' || text.startsWith('/*', i)) break;

            const partStart = i;
            if (c === '[') {
                i = this.matchingClose(i, end) + 1;
            } else if (c === ':') {
                i += text[i + 1] === ':' ? 2 : 1;
                const nameEnd = this.readIdentifier(i, end);
                if (nameEnd === i) this.unsupported(partStart, 'a pseudo selector without a name');
                i = text[nameEnd] === '(' ? this.matchingClose(nameEnd, end) + 1 : nameEnd;
            } else if (c === '.' || c === '#') {
                const nameEnd = this.readIdentifier(i + 1, end);
                if (nameEnd === i + 1) this.unsupported(partStart, `"${c}" without a name`);
                i = nameEnd;
            } else if (c === '*') {
                i++;
            } else {
                const nameEnd = this.readIdentifier(i, end);
                if (nameEnd === i) this.unsupported(i, `the character "${c}" in a selector`);
                i = nameEnd;
            }

            parts.push({ start: partStart, end: i });
        }

        return { start, end: i, parts };
    }

    private readIdentifier(start: number, end: number): number {
        const text = this.text;
        let i = start;
        while (i < end) {
            const c = text[i]!;
            if (c === '\\') {
                i += 2;
            } else if (/[\w-]/.test(c) || c.charCodeAt(0) > 0x7f) {
                i++;
            } else {
                break;
            }
        }

        return Math.min(i, end);
    }

    private skipWhitespaceAndComments(start: number, end: number): number {
        const text = this.text;
        let i = start;
        while (i < end) {
            if (/\s/.test(text[i]!)) {
                i++;
            } else if (text.startsWith('/*', i)) {
                i = this.skipComment(i, end);
            } else {
                break;
            }
        }

        return i;
    }

    private skipComment(start: number, end: number): number {
        const close = this.text.indexOf('*/', start + 2);
        return close === -1 || close + 2 > end ? end : close + 2;
    }

    private skipString(start: number, end: number): number {
        const text = this.text;
        const quote = text[start];
        let i = start + 1;
        while (i < end) {
            const c = text[i];
            if (c === '\\') {
                i += 2;
            } else if (c === quote) {
                return i + 1;
            } else if (c === '\n') {
                return i;
            } else {
                i++;
            }
        }

        return end;
    }

    // The first of the characters at the top level (not in a comment, a string or brackets), or -1.
    private findTopLevel(start: number, end: number, characters: string): number {
        const text = this.text;
        let i = start;
        while (i < end) {
            const c = text[i]!;
            if (text.startsWith('/*', i)) {
                i = this.skipComment(i, end);
            } else if (c === '"' || c === '\'') {
                i = this.skipString(i, end);
            } else if (c === '\\') {
                i += 2;
            } else if (characters.includes(c)) {
                return i;
            } else if (c === '(' || c === '[') {
                i = this.matchingClose(i, end) + 1;
            } else {
                i++;
            }
        }

        return -1;
    }

    // The index of the bracket closing the one at the start.
    private matchingClose(start: number, end: number): number {
        const text = this.text;
        const close = ({ '{': '}', '(': ')', '[': ']' } as Record<string, string>)[text[start]!];
        let i = start + 1;
        while (i < end) {
            const c = text[i]!;
            if (text.startsWith('/*', i)) {
                i = this.skipComment(i, end);
            } else if (c === '"' || c === '\'') {
                i = this.skipString(i, end);
            } else if (c === '\\') {
                i += 2;
            } else if (c === close) {
                return i;
            } else if (c === '{' || c === '(' || c === '[') {
                i = this.matchingClose(i, end) + 1;
            } else {
                i++;
            }
        }

        this.unsupported(start, `an unclosed "${text[start]}"`);
    }

    private getLocation(index: number): { line: number; character: number } {
        const before = this.text.substring(0, index);
        const lineStart = before.lastIndexOf('\n') + 1;
        return { line: before.split('\n').length, character: index - lineStart + 1 };
    }

    private unsupported(index: number, what: string): never {
        const location = this.getLocation(index);
        throw new Error(`${this.filePath}(${location.line},${location.character}): scoped CSS: ${what} is not supported here, ` +
            'because it is not certain how Blazor\'s CSS isolation treats it.');
    }
}

// Pseudo-elements of the "original set" which may be written with a single colon.
function isSingleColonPseudoElement(partText: string): boolean {
    const lower = partText.toLowerCase();
    return lower === ':after' || lower === ':before' || lower === ':first-letter' || lower === ':first-line';
}
