// The score's engraver (opensheetmusicdisplay, with the music engraving it carries) writes inline styles as `style`
// attributes, which the page's content policy refuses (style-src without 'unsafe-inline'). This patch, applied to the
// library's file as it is bundled — the build's and the dev server's — makes those two writes set the same style
// through the element's style object, which the policy allows. Nothing else of the library changes.
//
// The patch names the library's code exactly: when a new version changes it, the build stops here and says so, and the
// patch is written for that version.

import type { Plugin } from 'vite';

const FILE = /[\\/]opensheetmusicdisplay[\\/]build[\\/]opensheetmusicdisplay\.min\.js$/;

const CHANGES: [string, string][] = [
    // the drawing's <svg>: its default fill and stroke
    ['e.setAttribute("style",E)', 'e.style.cssText=E'],
    // any attribute given to a drawn element: a style among them goes through the style object
    ['i&&i[o]||x(s,o,a)||t.setAttribute(o,a)', 'i&&i[o]||x(s,o,a)||("style"===o?t.style.cssText=a:t.setAttribute(o,a))'],
];

export function patchEngraver(code: string): string {
    for (const [from, to] of CHANGES) {
        const at = code.indexOf(from);
        if (at < 0 || code.indexOf(from, at + 1) >= 0)
            throw new Error(`build/engraverStyles.ts: the score's engraver does not contain "${from}" exactly once; write the patch for this version`);
        code = code.replace(from, to);
    }
    return code;
}

/** For the build and the dev server's pre-bundling alike. */
export function engraverStyles(): Plugin {
    return {
        name: 'engraver-styles',
        enforce: 'pre',
        transform(code, id) {
            return FILE.test(id) ? { code: patchEngraver(code), map: null } : null;
        },
    };
}
