// The tests of the .NET SDK's RewriteCss task (test/Microsoft.NET.Sdk.Razor.Tests/StaticWebAssets/RewriteCssTest.cs,
// release/9.0.1xx), one for one.

import { describe, expect, it } from 'vitest';
import { addScopeToSelectors } from './rewriteCss';

const rewrite = (text: string) => addScopeToSelectors('file.css', text, 'TestScope');

describe('RewriteCss', () => {
    it('HandlesEmptyFile', () => {
        expect(rewrite('')).toBe('');
    });

    it('AddsScopeAfterSelector', () => {
        expect(rewrite(`
    .myclass { color: red; }
`)).toBe(`
    .myclass[TestScope] { color: red; }
`);
    });

    it('HandlesMultipleSelectors', () => {
        expect(rewrite(`
    .first, .second { color: red; }
    .third { color: blue; }
    :root { color: green; }
    * { color: white; }
    #some-id { color: yellow; }
`)).toBe(`
    .first[TestScope], .second[TestScope] { color: red; }
    .third[TestScope] { color: blue; }
    :root[TestScope] { color: green; }
    *[TestScope] { color: white; }
    #some-id[TestScope] { color: yellow; }
`);
    });

    it('HandlesComplexSelectors', () => {
        expect(rewrite(`
    .first div > li, body .second:not(.fancy)[attr~=whatever] { color: red; }
`)).toBe(`
    .first div > li[TestScope], body .second:not(.fancy)[attr~=whatever][TestScope] { color: red; }
`);
    });

    it('HandlesSpacesAndCommentsWithinSelectors', () => {
        expect(rewrite(`
    .first /* space at end {} */ div , .myclass /* comment at end */ { color: red; }
`)).toBe(`
    .first /* space at end {} */ div[TestScope] , .myclass[TestScope] /* comment at end */ { color: red; }
`);
    });

    it('HandlesPseudoClasses', () => {
        expect(rewrite(`
    a:fake-pseudo-class { color: red; }
    a:focus b:hover { color: green; }
    tr:nth-child(4n + 1) { color: blue; }
    a:has(b > c) { color: yellow; }
    a:last-child > ::deep b { color: pink; }
    a:not(#something) { color: purple; }
`)).toBe(`
    a:fake-pseudo-class[TestScope] { color: red; }
    a:focus b:hover[TestScope] { color: green; }
    tr:nth-child(4n + 1)[TestScope] { color: blue; }
    a:has(b > c)[TestScope] { color: yellow; }
    a:last-child[TestScope] >  b { color: pink; }
    a:not(#something)[TestScope] { color: purple; }
`);
    });

    it('HandlesPseudoElements', () => {
        expect(rewrite(`
    a::before { content: "✋"; }
    a::after::placeholder { content: "🐯"; }
    custom-element::part(foo) { content: "🤷‍"; }
    a::before > ::deep another { content: "👞"; }
    a::fake-PsEuDo-element { content: "🐔"; }
    ::selection { content: "😾"; }
    other, ::selection { content: "👂"; }
`)).toBe(`
    a[TestScope]::before { content: "✋"; }
    a[TestScope]::after::placeholder { content: "🐯"; }
    custom-element[TestScope]::part(foo) { content: "🤷‍"; }
    a[TestScope]::before >  another { content: "👞"; }
    a[TestScope]::fake-PsEuDo-element { content: "🐔"; }
    [TestScope]::selection { content: "😾"; }
    other[TestScope], [TestScope]::selection { content: "👂"; }
`);
    });

    it('HandlesSingleColonPseudoElements', () => {
        expect(rewrite(`
    a:after { content: "x"; }
    a:before { content: "x"; }
    a:first-letter { content: "x"; }
    a:first-line { content: "x"; }
    a:AFTER { content: "x"; }
    a:not(something):before { content: "x"; }
`)).toBe(`
    a[TestScope]:after { content: "x"; }
    a[TestScope]:before { content: "x"; }
    a[TestScope]:first-letter { content: "x"; }
    a[TestScope]:first-line { content: "x"; }
    a[TestScope]:AFTER { content: "x"; }
    a:not(something)[TestScope]:before { content: "x"; }
`);
    });

    it('RespectsDeepCombinator', () => {
        expect(rewrite(`
    .first ::deep .second { color: red; }
    a ::deep b, c ::deep d { color: blue; }
`)).toBe(`
    .first[TestScope]  .second { color: red; }
    a[TestScope]  b, c[TestScope]  d { color: blue; }
`);
    });

    it('RespectsDeepCombinatorWithDirectDescendant', () => {
        expect(rewrite(`
    a  >  ::deep b { color: red; }
    c ::deep  >  d { color: blue; }
`)).toBe(`
    a[TestScope]  >   b { color: red; }
    c[TestScope]   >  d { color: blue; }
`);
    });

    it('RespectsDeepCombinatorWithAdjacentSibling', () => {
        expect(rewrite(`
    a + ::deep b { color: red; }
    c ::deep + d { color: blue; }
`)).toBe(`
    a[TestScope] +  b { color: red; }
    c[TestScope]  + d { color: blue; }
`);
    });

    it('RespectsDeepCombinatorWithGeneralSibling', () => {
        expect(rewrite(`
    a ~ ::deep b { color: red; }
    c ::deep ~ d { color: blue; }
`)).toBe(`
    a[TestScope] ~  b { color: red; }
    c[TestScope]  ~ d { color: blue; }
`);
    });

    it('IgnoresMultipleDeepCombinators', () => {
        expect(rewrite(`
    .first ::deep .second ::deep .third { color:red; }
`)).toBe(`
    .first[TestScope]  .second ::deep .third { color:red; }
`);
    });

    it('RespectsDeepCombinatorWithSpacesAndComments', () => {
        expect(rewrite(`
    .a .b /* comment ::deep 1 */  ::deep  /* comment ::deep 2 */  .c /* ::deep */ .d { color: red; }
    ::deep * { color: blue; } /* Leading deep combinator */
    another ::deep { color: green }  /* Trailing deep combinator */
`)).toBe(`
    .a .b[TestScope] /* comment ::deep 1 */    /* comment ::deep 2 */  .c /* ::deep */ .d { color: red; }
    [TestScope] * { color: blue; } /* Leading deep combinator */
    another[TestScope]  { color: green }  /* Trailing deep combinator */
`);
    });

    it('HandlesAtBlocks', () => {
        expect(rewrite(`
    .myclass { color: red; }

    @media only screen and (max-width: 600px) {
        .another .thing {
            content: 'This should not be a selector: .fake-selector { color: red }'
        }
    }
`)).toBe(`
    .myclass[TestScope] { color: red; }

    @media only screen and (max-width: 600px) {
        .another .thing[TestScope] {
            content: 'This should not be a selector: .fake-selector { color: red }'
        }
    }
`);
    });

    it('AddsScopeToKeyframeNames', () => {
        expect(rewrite(`
    @keyframes my-animation { /* whatever */ }
`)).toBe(`
    @keyframes my-animation-TestScope { /* whatever */ }
`);
    });

    it('RewritesAnimationNamesWhenMatchingKnownKeyframes', () => {
        expect(rewrite(`
    .myclass {
        color: red;
        animation: /* ignore comment */ my-animation 1s infinite;
    }

    .another-thing { animation-name: different-animation; }

    h1 { animation: unknown-animation; } /* Should not be scoped */

    @keyframes my-animation { /* whatever */ }
    @keyframes different-animation { /* whatever */ }
    @keyframes unused-animation { /* whatever */ }
`)).toBe(`
    .myclass[TestScope] {
        color: red;
        animation: /* ignore comment */ my-animation-TestScope 1s infinite;
    }

    .another-thing[TestScope] { animation-name: different-animation-TestScope; }

    h1[TestScope] { animation: unknown-animation; } /* Should not be scoped */

    @keyframes my-animation-TestScope { /* whatever */ }
    @keyframes different-animation-TestScope { /* whatever */ }
    @keyframes unused-animation-TestScope { /* whatever */ }
`);
    });

    it('RewritesMultipleAnimationNames', () => {
        expect(rewrite(`
    .myclass1 { animation-name: my-animation , different-animation }
    .myclass2 { animation: 4s linear 0s alternate my-animation infinite, different-animation 0s }
    @keyframes my-animation { }
    @keyframes different-animation { }
`)).toBe(`
    .myclass1[TestScope] { animation-name: my-animation-TestScope , different-animation-TestScope }
    .myclass2[TestScope] { animation: 4s linear 0s alternate my-animation-TestScope infinite, different-animation-TestScope 0s }
    @keyframes my-animation-TestScope { }
    @keyframes different-animation-TestScope { }
`);
    });

    it('RejectsImportStatements', () => {
        const message = (line: number) => `file.css(${line},5): @import rules are not supported within scoped CSS files because the loading order would be undefined. @import may only be placed in non-scoped CSS files.`;
        expect(() => rewrite(`
    @import "basic-import.css";
    @import "import-with-media-type.css" print;
    @import "import-with-media-query.css" screen and (orientation:landscape);
    @ImPoRt /* comment */ "scheme://path/to/complex-import" /* another-comment */ screen;
    @otheratrule "should-not-cause-error.css";
    /* @import "should-be-ignored-because-it-is-in-a-comment.css"; */
    .myclass { color: red; }
`)).toThrow([message(2), message(3), message(4), message(5)].join('\n'));
    });
});
