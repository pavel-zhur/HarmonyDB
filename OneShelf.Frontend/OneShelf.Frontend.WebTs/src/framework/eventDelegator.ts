// When the application's event handlers run and when the DOM changes they cause are made, as in the original
// (Blazor WebAssembly, .NET 9: Web.JS EventDelegator.ts and the renderer; read in the original's
// blazor.webassembly.js and seen in the browser on the original site):
//
// - Blazor handles DOM events with one listener per event name on `document`, added when the first handler for that
//   name is rendered (for click, when the renderer is created, as its link interception needs it), in the bubbling
//   phase — or in the capturing phase for the events that do not bubble (the list below: change, focus, blur, scroll,
//   submit, load, the media events…), which it then gives to their target only.
// - Its listener walks the event's path from the target up; for each element with a handler for the event it calls the
//   .NET handler synchronously, and .NET renders what the handler changed before it returns. So the DOM is already
//   updated for everything that runs after Blazor's listener, and still as it was for everything before it.
// - After the click handlers, in the same listener, comes Blazor's link interception (the navigation).
// - The browser's propagation is never stopped (Razor's :stopPropagation stops only Blazor's own walk; the
//   application uses neither it nor :preventDefault); the default action stays the browser's.
//
// So the theme's document listeners (Bootstrap's, index.html's outside-click listener), all added before the
// application starts, act first and on the DOM as it was; the application's handler then runs and the DOM is updated
// at once, still inside the browser's dispatch of the event (user activation and the like are kept).
//
// React listens on its root container instead, and applies the updates of a handler only after its listener returns
// (in a microtask, or when the event's dispatch is over): the application's handlers would run, and re-render, before
// the event reaches `document`, where the theme's listeners would then see a DOM already changed. Here React's own
// listeners are moved to where Blazor's are: they are taken from the container when the root is created and added to
// `document` (each event in Blazor's phase for it), and each dispatch to React is made with flushSync, so the
// updates the handlers cause are applied before the listener returns. They are added when the root is created, after
// all the page's other document listeners; the original adds its listeners later and, except for click, only while a
// handler of that name is rendered — the page's scripts add no listeners of their own to `document` after the start
// for the events the application handles (click, input, change), so the order is the same.
//
// React gives `onChange` of a checkbox or a radio from the click; Blazor's @onchange is the browser's change event.
// nativeChange (elementRefs.ts) takes such handlers, and they are called here, from the change listener, for the
// event's target.

import { flushSync } from 'react-dom';
import { createRoot, type Root } from 'react-dom/client';
import { handleRendererException } from './unhandledExceptions';

/** Blazor's nonBubblingEvents: listened to in the capturing phase, given to the target only. */
const nonBubblingEvents = new Set([
    'abort', 'blur', 'cancel', 'canplay', 'canplaythrough', 'change', 'close', 'cuechange', 'durationchange', 'emptied',
    'ended', 'error', 'focus', 'load', 'loadeddata', 'loadedmetadata', 'loadend', 'loadstart', 'mouseenter',
    'mouseleave', 'pointerenter', 'pointerleave', 'pause', 'play', 'playing', 'progress', 'ratechange', 'reset',
    'scroll', 'seeked', 'seeking', 'stalled', 'submit', 'suspend', 'timeupdate', 'toggle', 'unload', 'volumechange',
    'waiting', 'DOMNodeInsertedIntoDocument', 'DOMNodeRemovedFromDocument',
]);

type Listener = (event: Event) => void;

interface ReactListeners {
    capture?: Listener;
    bubble?: Listener;
    options: AddEventListenerOptions;
}

const afterClickCallbacks: ((event: MouseEvent) => void)[] = [];
const changeHandlers = new WeakMap<Element, (element: Element) => void>();

/** createRoot, with React's listeners where Blazor's are (see above). */
export function createRootWithBlazorEventOrder(container: Element): Root {
    const listeners = new Map<string, ReactListeners>();
    // React adds its listeners to the container while the root is created, and never again for it.
    Object.defineProperty(container, 'addEventListener', {
        configurable: true,
        value: (type: string, listener: EventListenerOrEventListenerObject, options?: boolean | AddEventListenerOptions) => {
            const capture = typeof options === 'boolean' ? options : options?.capture === true;
            const entry = listeners.get(type) ?? { options: {} };
            const callable: Listener = typeof listener === 'function' ? listener : event => listener.handleEvent(event);
            if (capture) entry.capture = callable; else entry.bubble = callable;
            if (typeof options === 'object' && options.passive !== undefined) entry.options = { passive: options.passive };
            listeners.set(type, entry);
        },
    });

    let root: Root;
    try {
        root = createRoot(container);
    } finally {
        delete (container as { addEventListener?: unknown }).addEventListener;
    }

    for (const [type, { capture, bubble, options }] of listeners) {
        if (nonBubblingEvents.has(type)) {
            document.addEventListener(type, event => {
                if (capture) dispatchToApplication(() => capture(event));
                if (bubble) dispatchToApplication(() => bubble(event));
                if (type === 'change') dispatchChange(event);
            }, { ...options, capture: true });
        } else {
            if (capture) document.addEventListener(type, event => dispatchToApplication(() => capture(event)), { ...options, capture: true });
            if (bubble) {
                document.addEventListener(type, event => {
                    dispatchToApplication(() => bubble(event));
                    if (type === 'click' && event.target instanceof Element) {
                        for (const callback of afterClickCallbacks) callback(event as MouseEvent);
                    }
                }, { ...options, capture: false });
            }
        }
    }

    return root;
}

/**
 * Runs what Blazor runs after the click handlers, in the same listener (the link interception); returns the
 * removal.
 */
export function addAfterClickCallback(callback: (event: MouseEvent) => void): () => void {
    afterClickCallbacks.push(callback);
    return () => {
        const index = afterClickCallbacks.indexOf(callback);
        if (index >= 0) afterClickCallbacks.splice(index, 1);
    };
}

/** Sets (or replaces) an element's handler of the browser's change event; see nativeChange. */
export function setChangeHandler(element: Element, handler: (element: Element) => void): void {
    changeHandlers.set(element, handler);
}

/**
 * Calls the application's handlers and applies the updates they cause before returning. An exception from a handler
 * is the renderer's to log, as Blazor's renderer logs an exception from an event handler; the listener goes on.
 */
function dispatchToApplication(dispatch: () => void): void {
    try {
        flushSync(dispatch);
    } catch (exception) {
        handleRendererException(exception);
    }
}

function dispatchChange(event: Event): void {
    const target = event.composedPath()[0];
    if (!(target instanceof Element)) return;
    const handler = changeHandlers.get(target);
    if (handler) dispatchToApplication(() => handler(target));
}
