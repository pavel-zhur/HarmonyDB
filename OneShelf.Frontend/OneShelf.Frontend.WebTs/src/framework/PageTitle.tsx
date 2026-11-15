// Blazor's PageTitle and HeadOutlet (.NET 9).
//
// <PageTitle>text</PageTitle> anywhere in the page sets the document's title; when several are rendered, the one added
// last wins, and when none is, the title the page had at the start (index.html's) is shown. HeadOutlet, rendered once
// (the original adds it as a root component at "head::after"), removes index.html's <title> after its first render and
// renders its own <title> at the end of <head>.

import { useEffect, useLayoutEffect, useRef, useState, useSyncExternalStore } from 'react';
import { createPortal } from 'react-dom';

type Text = string | number | (string | number)[];

interface Provider {
    text: string;
}

let providers: Provider[] = [];
const listeners = new Set<() => void>();

function changed() {
    providers = [...providers];
    for (const listener of listeners) listener();
}

function toText(children: Text | undefined): string {
    return Array.isArray(children) ? children.join('') : children === undefined ? '' : String(children);
}

export function PageTitle({ children }: { children?: Text }) {
    const text = toText(children);
    const provider = useRef<Provider>();

    useLayoutEffect(() => {
        const current: Provider = { text: '' };
        provider.current = current;
        providers.push(current);
        changed();
        return () => {
            providers = providers.filter(x => x !== current);
            changed();
        };
    }, []);

    useLayoutEffect(() => {
        if (provider.current!.text !== text) {
            provider.current!.text = text;
            changed();
        }
    }, [text]);

    return null;
}

export function HeadOutlet() {
    const current = useSyncExternalStore(
        listener => {
            listeners.add(listener);
            return () => listeners.delete(listener);
        },
        () => providers);
    const [defaultTitle, setDefaultTitle] = useState<string | null>(null);
    const ownTitle = useRef<HTMLTitleElement>(null);

    useEffect(() => {
        setDefaultTitle(getAndRemoveExistingTitle(ownTitle.current));
    }, []);

    const last = current[current.length - 1];
    const text = last !== undefined ? last.text : defaultTitle;
    return text === null || (last === undefined && text === '')
        ? null
        : createPortal(<title ref={ownTitle}>{text}</title>, document.head);
}

// Blazor._internal.PageTitle.getAndRemoveExistingTitle: the text of the last <title> in <head> that is not the
// outlet's own, and all such <title>s removed.
function getAndRemoveExistingTitle(own: HTMLTitleElement | null): string | null {
    const titleElements = Array.from(document.head.getElementsByTagName('title'));
    let existingTitle: string | null = null;
    for (let index = titleElements.length - 1; index >= 0; index--) {
        const element = titleElements[index]!;
        if (element === own) continue;
        if (existingTitle === null) existingTitle = element.textContent;
        element.parentNode?.removeChild(element);
    }

    return existingTitle;
}
