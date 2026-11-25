// What the tests under node need of a browser: storage, the cookie, the address, a fetch that the test answers.
// Imported first by a test.

class MemoryStorage {
    private items = new Map<string, string>();
    getItem(key: string) { return this.items.get(key) ?? null; }
    setItem(key: string, value: string) { this.items.set(key, String(value)); }
    removeItem(key: string) { this.items.delete(key); }
    clear() { this.items.clear(); }
    key(i: number) { return [...this.items.keys()][i] ?? null; }
    get length() { return this.items.size; }
}

const g = globalThis as Record<string, unknown>;
g.localStorage = new MemoryStorage();
g.sessionStorage = new MemoryStorage();
g.location = { origin: 'http://shelf.test', href: 'http://shelf.test/', pathname: '/', search: '' };
g.document = { cookie: '', addEventListener() {}, visibilityState: 'visible', documentElement: { lang: '', style: { setProperty() {} }, dataset: {} }, getElementById: () => null };

export interface Call {
    method: string;
    path: string;
    headers: Headers;
    body: unknown;
}

export const calls: Call[] = [];

/** The test's answers: by "METHOD /path", a function of the call. */
export const answers = new Map<string, (call: Call) => { status?: number; body?: unknown; cookie?: string }>();

g.fetch = async (request: Request | string, init?: RequestInit) => {
    const input = typeof request === 'string' ? new Request(new URL(request, 'http://shelf.test'), init) : request;
    const url = new URL(input.url);
    const text = input.method === 'GET' ? '' : await input.text();
    const call: Call = { method: input.method, path: url.pathname, headers: input.headers, body: text ? JSON.parse(text) : null };
    calls.push(call);
    const answer = answers.get(`${call.method} ${call.path}`)?.(call) ?? { status: 404, body: { code: 'not_found' } };
    if (answer.cookie !== undefined) (g.document as { cookie: string }).cookie = answer.cookie;
    const status = answer.status ?? 200;
    return new Response(status === 204 ? null : JSON.stringify(answer.body ?? {}), { status, headers: { 'content-type': 'application/json' } });
};

export function reset(): void {
    calls.length = 0;
    answers.clear();
    (g.localStorage as MemoryStorage).clear();
    (g.sessionStorage as MemoryStorage).clear();
    (g.document as { cookie: string }).cookie = '';
}
