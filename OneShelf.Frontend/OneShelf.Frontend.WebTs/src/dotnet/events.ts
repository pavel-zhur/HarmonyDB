// C# events (multicast delegates) as the site's services raise them.
//
// - `add` is `+=`: the same handler may be added more than once and is then called as many times.
// - `remove` is `-=`: removes the last occurrence of the handler, if any.
// - Raising calls the handlers in the order they were added, over the list as it was when raising began; an exception
//   from a handler stops the raising and propagates (as invoking a multicast delegate does).
// - DelegateEvent is `event Action<...>` / `event EventHandler<...>`: `invoke` calls each handler.
// - AsyncDelegateEvent is `event Func<..., Task>` raised as the original does it:
//   `Task.WhenAll(X?.GetInvocationList().Cast<Func<..., Task>>().Select(x => x(...)) ?? Enumerable.Empty<Task>())`:
//   every handler is called in turn, and the raising completes when all their tasks have.

export class DelegateEvent<TArgs extends unknown[] = []> {
    private handlers: readonly ((...args: TArgs) => void)[] = [];

    add(handler: (...args: TArgs) => void): void {
        this.handlers = [...this.handlers, handler];
    }

    remove(handler: (...args: TArgs) => void): void {
        const index = this.handlers.lastIndexOf(handler);
        if (index >= 0) {
            this.handlers = [...this.handlers.slice(0, index), ...this.handlers.slice(index + 1)];
        }
    }

    /** `X?.Invoke(...)` */
    invoke(...args: TArgs): void {
        for (const handler of this.handlers) {
            handler(...args);
        }
    }
}

export class AsyncDelegateEvent<TArgs extends unknown[] = []> {
    private handlers: readonly ((...args: TArgs) => Promise<void>)[] = [];

    add(handler: (...args: TArgs) => Promise<void>): void {
        this.handlers = [...this.handlers, handler];
    }

    remove(handler: (...args: TArgs) => Promise<void>): void {
        const index = this.handlers.lastIndexOf(handler);
        if (index >= 0) {
            this.handlers = [...this.handlers.slice(0, index), ...this.handlers.slice(index + 1)];
        }
    }

    /** `await Task.WhenAll(X?.GetInvocationList()...Select(x => x(...)) ?? Enumerable.Empty<Task>())` */
    async invokeAll(...args: TArgs): Promise<void> {
        const tasks: Promise<void>[] = [];
        for (const handler of this.handlers) {
            tasks.push(handler(...args));
        }

        await whenAll(tasks);
    }
}

/** `await Task.WhenAll(tasks)`: waits for all of them, then throws the first failure (in the order of the tasks). */
export async function whenAll<T>(tasks: readonly Promise<T>[]): Promise<T[]> {
    const results = await Promise.allSettled(tasks);
    for (const result of results) {
        if (result.status === 'rejected') throw result.reason;
    }

    return results.map(x => (x as PromiseFulfilledResult<T>).value);
}
