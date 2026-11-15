// Attributes and properties as Blazor renders them, where React would not render them the same way. Each is a ref
// callback for the element (React calls it after every render with a new one, as Blazor applies the rendered values).
//
// - stringAttributes({ style: 'font-size: 100%', onclick: "alert('...')" }): sets attributes React does not take as
//   text (style, inline on* handlers, a boolean attribute's own text such as disabled="disabled") to the exact text
//   the Razor markup gives; null removes one. Written when the element appears and when the rendered text changes.
// - booleanAttribute('selected', condition): a boolean attribute written in Razor as `selected="@(condition)"`: present
//   (empty) when true, absent when false — an attribute, as Blazor sets it, where React would set only the property.
// - blazorValue(value): an <input>, <select> or <textarea> whose `value` Blazor binds one way (value="@x", @bind):
//   Blazor sets the element's value property only when the rendered value differs from the one it rendered before
//   (not when it differs from what the person has typed meanwhile; a new element: when not null), never as an
//   attribute; null is written as "". (A React controlled input would instead force the element back to its state
//   after every keystroke.)
// - blazorChecked(checked): the same for checked="@x" on a checkbox or radio: the checked property (never the
//   attribute) when the element is inserted with the attribute present, and again only when the rendered value
//   differs from the one rendered before; false or null is the attribute left out (unchecked).

const renderedAttributes = new WeakMap<Element, Map<string, string | null>>();

export function stringAttributes(attributes: Readonly<Record<string, string | null>>): (element: Element | null) => void {
    return element => {
        if (element === null) return;
        // As Blazor's diff: an attribute is written only when its rendered text differs from the one rendered before
        // (so what the theme's scripts set meanwhile, such as a dropdown's position in its style, stays).
        let rendered = renderedAttributes.get(element);
        if (rendered === undefined) {
            rendered = new Map();
            renderedAttributes.set(element, rendered);
        }

        for (const [name, value] of Object.entries(attributes)) {
            if (rendered.has(name) && rendered.get(name) === value) continue;
            if (value === null) {
                element.removeAttribute(name);
            } else {
                element.setAttribute(name, value);
            }

            rendered.set(name, value);
        }
    };
}

export function booleanAttribute(name: string, value: boolean): (element: Element | null) => void {
    return element => {
        if (element === null) return;
        if (value) {
            if (!element.hasAttribute(name)) element.setAttribute(name, '');
        } else {
            element.removeAttribute(name);
        }
    };
}

type ValueElement = HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement;

const renderedValues = new WeakMap<ValueElement, string | null>();

export function blazorValue(value: string | null): (element: ValueElement | null) => void {
    return element => {
        if (element === null) return;
        if (renderedValues.has(element) && renderedValues.get(element) === value) return;
        if (renderedValues.has(element) || value !== null) element.value = value ?? '';
        renderedValues.set(element, value);
    };
}

const renderedChecked = new WeakMap<HTMLInputElement, boolean>();

export function blazorChecked(checked: boolean | string | null): (element: HTMLInputElement | null) => void {
    const value = checked !== null && checked !== false;
    return element => {
        if (element === null) return;
        if (renderedChecked.has(element) && renderedChecked.get(element) === value) return;
        if (renderedChecked.has(element) || value) element.checked = value;
        renderedChecked.set(element, value);
    };
}
