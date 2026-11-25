// The browser API's client, generated: schema.d.ts comes from HarmonyDB.Shelf.Model/openapi.json (`npm run api`).
// One fetch for every call: same origin, the session cookie, the CSRF header on every writing request, and one
// repeat when the server answers csrf_failed (it hands a fresh token cookie with that answer).
//
//   const songs = await call(api.GET('/api/library', { params: { query: { q, sort } } }));
//
// call() gives the answer's data or throws ApiError with the server's code.

import createClient from 'openapi-fetch';
import type { components, paths } from './schema';

export type Schemas = components['schemas'];

const writing = new Set(['POST', 'PUT', 'PATCH', 'DELETE']);

function csrfToken(): string | null {
    // __Host-shelf-csrf on https; shelf-csrf on the local stand's plain http
    for (const part of document.cookie.split('; ')) {
        const eq = part.indexOf('=');
        const name = part.slice(0, eq);
        if (name === '__Host-shelf-csrf' || name === 'shelf-csrf') return decodeURIComponent(part.slice(eq + 1));
    }
    return null;
}

/** Ensures a CSRF cookie exists before the first writing request: any answer of /api hands one when it is missing. */
async function ensureCsrf(): Promise<void> {
    if (csrfToken() === null) await fetch('/api/auth/session', { credentials: 'same-origin' });
}

function withCsrf(request: Request): Request {
    const headers = new Headers(request.headers);
    const token = csrfToken();
    if (token !== null) headers.set('X-CSRF-Token', token);
    return new Request(request, { headers });
}

async function shelfFetch(input: Request): Promise<Response> {
    if (!writing.has(input.method)) return fetch(input);
    await ensureCsrf();
    // a copy is kept for the one repeat: a request's body is read once
    const request = withCsrf(input);
    const retry = request.clone();
    const response = await fetch(request);
    if (response.status === 403) {
        const body = await response.clone().json().catch(() => null) as Schemas['ErrorResponse'] | null;
        if (body?.code === 'csrf_failed') return fetch(withCsrf(retry));
    }
    return response;
}

export const api = createClient<paths>({ baseUrl: location.origin, credentials: 'same-origin', fetch: shelfFetch });

/** An error answer of the API (its code, as ErrorResponse gives it), or no connection (code "network"). */
export class ApiError extends Error {
    readonly status: number;
    readonly code: string;
    readonly details?: Record<string, string> | null;

    constructor(status: number, code: string, details?: Record<string, string> | null) {
        super(code);
        this.status = status;
        this.code = code;
        this.details = details;
    }

    get isSignInRequired(): boolean {
        return this.status === 401 && this.code === 'sign_in_required';
    }
}

type Answer<T> = { data?: T; error?: unknown; response: Response };

/** The data of an answer, or ApiError. */
export async function call<T>(answer: Promise<Answer<T>>): Promise<T> {
    let result: Answer<T>;
    try {
        result = await answer;
    } catch (error) {
        if (error instanceof TypeError) throw new ApiError(0, 'network');
        throw error;
    }

    if (result.response.ok) return result.data as T;
    const body = result.error as Schemas['ErrorResponse'] | undefined;
    throw new ApiError(result.response.status, body?.code ?? 'internal_error', body?.details);
}
