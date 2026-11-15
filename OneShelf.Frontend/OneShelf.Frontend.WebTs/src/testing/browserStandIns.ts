// For tests (Vitest runs in Node): stand-ins for the browser's localStorage and location, installed on globalThis.

/** A Storage over a Map, as window.localStorage (keys in the order they were added). */
export class MemoryStorage implements Storage {
    private readonly items = new Map<string, string>();

    get length(): number {
        return this.items.size;
    }

    clear(): void {
        this.items.clear();
    }

    getItem(key: string): string | null {
        return this.items.get(key) ?? null;
    }

    key(index: number): string | null {
        return [...this.items.keys()][index] ?? null;
    }

    removeItem(key: string): void {
        this.items.delete(key);
    }

    setItem(key: string, value: string): void {
        this.items.set(key, String(value));
    }

    [name: string]: unknown;
}

/** window.localStorage (a new, empty one) for the code under test. */
export function installMemoryLocalStorage(): MemoryStorage {
    const storage = new MemoryStorage();
    const g = globalThis as unknown as { window?: { localStorage: Storage } };
    g.window = Object.assign(g.window ?? {}, { localStorage: storage }) as { localStorage: Storage };
    return storage;
}
