// What the original does through IJSRuntime with its JavaScript files:
//
// - `await JsRuntime.InvokeAsync<IJSObjectReference>("import", "./Shared/MainLayout.razor.js")` imports a JavaScript
//   module served by the site (the original's collocated *.razor.js files, wwwroot/services-js, the packages'
//   _content files), resolved against the document's base address: importJsModule('./Shared/MainLayout.razor.js').
//   The files are in public/ unchanged; the browser imports each once.
// - `module.InvokeVoidAsync("X", args)` / `InvokeAsync<T>("X", args)`: invokeJs(module.X, args) — the arguments
//   go as JSON (as JS interop sends them), the function is called at once, its result (awaited if it is a promise)
//   comes back asynchronously.
// - `DotNetObjectReference.Create(this)`: createDotNetObjectReference({ Method: (...args) => ... }) — an object
//   JavaScript calls back with `ref.invokeMethodAsync('Method', ...args)`; the arguments come as JSON values, the
//   call is dispatched asynchronously and returns a promise of the result.

/** `IJSRuntime.InvokeAsync<IJSObjectReference>("import", path)` */
export async function importJsModule<TModule>(path: string): Promise<TModule> {
    const url = new URL(path, document.baseURI).href;
    return await import(/* @vite-ignore */ url) as TModule;
}

/** `InvokeAsync<T>` / `InvokeVoidAsync` on a JavaScript function, with the arguments sent as JSON. */
export async function invokeJs<TResult>(fn: (...args: never[]) => unknown, ...args: unknown[]): Promise<TResult> {
    const serialized = args.map(toJsonArgument) as never[];
    const result = await Promise.resolve(fn(...serialized));
    return result as TResult;
}

/** An argument as JavaScript receives it from .NET: its JSON, except a DotNetObjectReference, which passes as itself. */
function toJsonArgument(arg: unknown): unknown {
    if (arg instanceof DotNetObjectReference || arg instanceof DotNetStreamReference) return arg;
    return arg === undefined ? null : JSON.parse(JSON.stringify(arg) ?? 'null');
}

export class DotNetObjectReference {
    constructor(private readonly methods: Readonly<Record<string, (...args: never[]) => unknown>>) {
    }

    /** DotNet's `invokeMethodAsync(identifier, ...args)` on the reference. */
    invokeMethodAsync(methodIdentifier: string, ...args: unknown[]): Promise<unknown> {
        const method = this.methods[methodIdentifier];
        const serialized = JSON.stringify(args);
        return Promise.resolve().then(async () => {
            if (method === undefined) {
                throw new Error(`The type does not contain a public invokable method with [JSInvokableAttribute("${methodIdentifier}")].`);
            }

            const result = await method(...(JSON.parse(serialized) as never[]));
            return result === undefined ? null : JSON.parse(JSON.stringify(result) ?? 'null');
        });
    }

    dispose(): void {
    }
}

/** `DotNetObjectReference.Create(obj)`: the object's [JSInvokable] methods by their identifiers. */
export function createDotNetObjectReference(methods: Readonly<Record<string, (...args: never[]) => unknown>>): DotNetObjectReference {
    return new DotNetObjectReference(methods);
}

/** `new DotNetStreamReference(stream)`: JavaScript reads it with `arrayBuffer()` (or `stream()`). */
export class DotNetStreamReference {
    constructor(private readonly data: Blob) {
    }

    arrayBuffer(): Promise<ArrayBuffer> {
        return this.data.arrayBuffer();
    }

    stream(): Promise<ReadableStream<Uint8Array>> {
        return Promise.resolve(this.data.stream());
    }
}

// The site's JavaScript modules, as the original calls them.

/** ./Pages/Index.razor.js */
export interface IndexRazorJs {
    getWasmReleaseVersion(): string;
}

/** ./Pages/Login.razor.js */
export interface LoginRazorJs {
    InjectWidgetHere(objRef: DotNetObjectReference, el: string, botId: string | null): void;
}

/** ./Shared/ChordsView.razor.js */
export interface ChordsViewRazorJs {
    bindAll(objRef: DotNetObjectReference): void;
}

/** ./Shared/MainLayout.razor.js */
export interface MainLayoutRazorJs {
    MainLayoutRendered(): void;
    InitSecondToTop(): void;
}

/** ./Shared/MenuLeft.razor.js */
export interface MenuLeftRazorJs {
    Clicked(): void;
}

/** ./services-js/JsFunctions.js */
export interface JsFunctionsJs {
    Prompt(query: string): string | null;
    Confirm(query: string): boolean;
    ScrollToTop(): void;
}

/** ./services-js/Player.js */
export interface PlayerJs {
    playChord(bass: number[], notes: number[]): void;
    mute(): void;
}

/** window functions the original calls by name (index.html, sw-registrator.js). */
export interface WindowFunctions {
    registerForUpdateAvailableNotification(caller: DotNetObjectReference, methodName: string): void;
    downloadFileFromStream(fileName: string, contentStreamReference: DotNetStreamReference): Promise<void>;
}

/** `IJSRuntime.InvokeAsync<T>("name", args)` / `InvokeVoidAsync` on a function of `window`. */
export async function invokeWindowFunction<TResult>(identifier: keyof WindowFunctions, ...args: unknown[]): Promise<TResult> {
    const fn = (window as unknown as Record<string, unknown>)[identifier];
    if (typeof fn !== 'function') {
        throw new Error(`Could not find '${identifier}' ('${identifier}' was undefined).`);
    }

    return await invokeJs<TResult>(fn as (...a: never[]) => unknown, ...args);
}
