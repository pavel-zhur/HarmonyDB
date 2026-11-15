// More of what Blazor's renderer does to elements that React does differently (see also attributes.ts).
//
// - nativeChange(handler): Blazor's @onchange on an <input>: the handler of the browser's "change" event, called when
//   and where Blazor calls it (eventDelegator.ts). React's onChange is not that event on an input: on a text input
//   it fires on every keystroke (the change event fires when the edit is committed), on a checkbox or a radio it
//   comes from the click (the change event follows the click). (On a <select> React's onChange is the change event.)
//   The latest handler given is the one called.
// - textareaText(text): a ref for a <textarea> whose content Razor writes as its child text (<textarea>@x</textarea>):
//   Blazor gives it a text node and changes that node when the rendered text changes, never the value property, so
//   the shown text follows the rendered one until the user edits it. React's defaultValue sets the value property
//   (after which the text no longer follows), so the text node is kept here instead.
// - combineRefs(...refs): several ref callbacks on one element.

import { setChangeHandler } from './eventDelegator';

const renderedTexts = new WeakMap<HTMLTextAreaElement, string>();

export function textareaText(text: string | null): (element: HTMLTextAreaElement | null) => void {
    const value = text ?? '';
    return element => {
        if (element === null) return;
        if (renderedTexts.has(element) && renderedTexts.get(element) === value) return;
        if (renderedTexts.has(element) || value.length > 0) element.textContent = value;
        renderedTexts.set(element, value);
    };
}

/** What a nativeChange handler is given: the element whose value changed (Blazor's ChangeEventArgs carries its value). */
export interface NativeChangeEvent<TElement extends Element> {
    readonly currentTarget: TElement;
}

export function nativeChange<TElement extends Element>(handler: (event: NativeChangeEvent<TElement>) => void): (element: TElement | null) => void {
    return element => {
        if (element === null) return;
        setChangeHandler(element, target => handler({ currentTarget: target as TElement }));
    };
}

export function combineRefs<TElement extends Element>(...refs: ((element: TElement | null) => void)[]): (element: TElement | null) => void {
    return element => {
        for (const ref of refs) ref(element);
    };
}
