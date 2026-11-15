// The .NET task primitives the site's services use, as they behave in Blazor WebAssembly (one thread, no
// synchronization context: continuations queued to the thread pool run as macrotasks of the browser).
//
// A CancellationToken is an AbortSignal here (as in src/lib/api).

import { OperationCanceledException, TaskCanceledException } from '../lib/common/dotnet/exceptions';

/** `Task.Delay(milliseconds, token)`: a cancelled token gives TaskCanceledException. */
export function delay(milliseconds: number, cancellationToken?: AbortSignal): Promise<void> {
    return new Promise<void>((resolve, reject) => {
        if (cancellationToken?.aborted) {
            reject(new TaskCanceledException());
            return;
        }

        const onAbort = () => {
            clearTimeout(timeout);
            reject(new TaskCanceledException());
        };

        const timeout = setTimeout(() => {
            cancellationToken?.removeEventListener('abort', onAbort);
            resolve();
        }, milliseconds);

        cancellationToken?.addEventListener('abort', onAbort, { once: true });
    });
}

/** `await Task.Yield()`: the rest runs later, after what the browser has queued (rendering included). */
export function yieldTask(): Promise<void> {
    return new Promise<void>(resolve => setTimeout(resolve, 0));
}

/** `token.ThrowIfCancellationRequested()`: OperationCanceledException (not TaskCanceledException). */
export function throwIfCancellationRequested(cancellationToken: AbortSignal | undefined): void {
    if (cancellationToken?.aborted) {
        throw new OperationCanceledException();
    }
}

/** `CancellationTokenSource` */
export class CancellationTokenSource {
    private readonly controller = new AbortController();

    get token(): AbortSignal {
        return this.controller.signal;
    }

    cancel(): void {
        this.controller.abort();
    }

    dispose(): void {
    }
}

/** `System.Threading.Timer(callback, null, dueTime, period)`; `dispose()` stops it. */
export class Timer {
    private timeout: ReturnType<typeof setTimeout> | undefined;
    private interval: ReturnType<typeof setInterval> | undefined;

    constructor(callback: () => void, dueTimeMilliseconds: number, periodMilliseconds: number) {
        this.timeout = setTimeout(() => {
            this.timeout = undefined;
            this.interval = setInterval(callback, periodMilliseconds);
            callback();
        }, dueTimeMilliseconds);
    }

    dispose(): void {
        clearTimeout(this.timeout);
        clearInterval(this.interval);
        this.timeout = undefined;
        this.interval = undefined;
    }
}
