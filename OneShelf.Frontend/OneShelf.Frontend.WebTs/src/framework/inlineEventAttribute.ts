// A plain HTML event attribute written in Razor markup as text, e.g. `onclick="document.getElementById('x').click()"`:
// the browser runs it as an inline handler. React does not set on* attributes itself, so the element gets it through
// its ref, after each render, with the value of that render:
//
//     <a ref={inlineEventAttribute('onclick', "document.getElementById('minor').scrollIntoView({behavior:'smooth'})")}>

export function inlineEventAttribute(name: string, value: string): (element: Element | null) => void {
    return element => {
        element?.setAttribute(name, value);
    };
}
