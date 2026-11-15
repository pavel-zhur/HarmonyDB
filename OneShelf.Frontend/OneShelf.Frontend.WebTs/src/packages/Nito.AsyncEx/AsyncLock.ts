// Nito.AsyncEx.AsyncLock (Nito.AsyncEx.Coordination 5.1.2): a mutual exclusion lock taken asynchronously, granted to
// the waiters in the order they asked, not reentrant.
//
//   using (await _lock.LockAsync()) { ... }   →   const _ = await this._lock.lockAsync(); try { ... } finally { _.dispose(); }
//
// Lock() (the synchronous one) waits on the calling thread when the lock is taken; on the single thread of
// Blazor WebAssembly that wait fails ("Cannot wait on monitors on this runtime."), and so it does here.

export interface IDisposable {
    dispose(): void;
}

export class AsyncLock {
    private taken = false;
    private readonly waiters: (() => void)[] = [];

    lockAsync(): Promise<IDisposable> {
        if (!this.taken) {
            this.taken = true;
            return Promise.resolve(this.createReleaser());
        }

        return new Promise<IDisposable>(resolve => {
            this.waiters.push(() => resolve(this.createReleaser()));
        });
    }

    lock(): IDisposable {
        if (this.taken) {
            throw new Error('Cannot wait on monitors on this runtime.');
        }

        this.taken = true;
        return this.createReleaser();
    }

    private createReleaser(): IDisposable {
        let released = false;
        return {
            dispose: () => {
                if (released) return;
                released = true;
                this.release();
            },
        };
    }

    private release(): void {
        const next = this.waiters.shift();
        if (next !== undefined) {
            next();
        } else {
            this.taken = false;
        }
    }
}
