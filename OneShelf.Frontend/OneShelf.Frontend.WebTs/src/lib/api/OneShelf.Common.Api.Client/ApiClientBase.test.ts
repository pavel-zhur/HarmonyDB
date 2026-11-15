import { describe, expect, it, vi } from 'vitest';
import { HttpRequestException, TaskCanceledException } from '../../common/dotnet/exceptions';
import { CollectivesApiClient } from '../OneShelf.Collectives.Api.Client/CollectivesApiClient';
import { Identity } from '../OneShelf.Authorization.Api.Model/Identity';
import { CacheItemNotFoundException } from '../OneShelf.Common.Api.Common/CacheItemNotFoundException';
import { ConcurrencyException } from '../OneShelf.Common.Api.Common/ConcurrencyException';
import { UnauthorizedException } from '../OneShelf.Common.Api.Common/UnauthorizedException';
import { FrontendApiClient } from '../OneShelf.Frontend.Api.Client/FrontendApiClient';
import type { HttpFetch } from './ApiClientBase';

const configuration = (values: Record<string, string>) => (key: string) => values[key];
const identity = new Identity({ id: 1, hash: 'h' });

function respond(status: number, body: string, statusText = '') {
    const calls: { url: string; init: RequestInit }[] = [];
    const httpFetch: HttpFetch = async (url, init) => {
        calls.push({ url, init });
        return new Response(body, { status, statusText });
    };
    return { calls, httpFetch };
}

function frontend(httpFetch: HttpFetch) {
    return FrontendApiClient.create(configuration({ 'FrontendApiClientOptions:Endpoint': 'http://localhost:7071/api/' }), httpFetch);
}

describe('ApiClientBase', () => {
    it('POSTs camelCase JSON to the endpoint + the method name', async () => {
        const { calls, httpFetch } = respond(200, '{"result":true}');
        const response = await frontend(httpFetch).deleteLikeCategory(identity, 5, null);
        expect(response.result).toBe(true);
        expect(calls).toHaveLength(1);
        expect(calls[0]!.url).toBe('http://localhost:7071/api/V3DeleteLikeCategory');
        expect(calls[0]!.init.method).toBe('POST');
        expect(calls[0]!.init.headers).toEqual({ 'Content-Type': 'application/json; charset=utf-8' });
        expect(calls[0]!.init.body).toBe('{"identity":{"id":1,"first_name":null,"last_name":null,"username":null,"photo_url":null,"auth_date":0,"hash":"h"},"likeCategoryId":5}');
    });

    it('resolves the method name against the endpoint as System.Uri does', async () => {
        const { calls, httpFetch } = respond(200, '{"headers":[]}');
        const client = FrontendApiClient.create(configuration({ 'FrontendApiClientOptions:Endpoint': 'https://host/x/api' }), httpFetch);
        await client.search(identity, 'q', null);
        expect(calls[0]!.url).toBe('https://host/x/V3Search');
    });

    it('the Collectives client reads its own section', async () => {
        const { calls, httpFetch } = respond(200, '{}');
        const client = CollectivesApiClient.create(configuration({ 'CollectivesApiClientOptions:Endpoint': 'http://localhost:7284/api/' }), httpFetch);
        await client.delete(identity, '0f8fad5b-d9cb-469f-a165-70867728950e');
        expect(calls[0]!.url).toBe('http://localhost:7284/api/V2Delete');
        expect(calls[0]!.init.body).toContain('"collectiveId":"0f8fad5b-d9cb-469f-a165-70867728950e"');
    });

    it('401: calls `unauthorized`, then throws UnauthorizedException with the response text', async () => {
        const unauthorized = vi.fn();
        const { httpFetch } = respond(401, 'Bad hash.');
        const error = await frontend(httpFetch).getVisitedChords(identity, unauthorized).catch(e => e);
        expect(unauthorized).toHaveBeenCalledOnce();
        expect(error).toBeInstanceOf(UnauthorizedException);
        expect(error.message).toBe('Bad hash.');
    });

    it('429 → ConcurrencyException, 460 → CacheItemNotFoundException, other failures → HttpRequestException', async () => {
        await expect(frontend(respond(429, '').httpFetch).search(identity, 'q', null)).rejects.toBeInstanceOf(ConcurrencyException);
        await expect(frontend(respond(460, '').httpFetch).search(identity, 'q', null)).rejects.toBeInstanceOf(CacheItemNotFoundException);
        const error = await frontend(respond(500, 'x', 'Internal Server Error').httpFetch).search(identity, 'q', null).catch(e => e);
        expect(error).toBeInstanceOf(HttpRequestException);
        expect(error.message).toBe('Response status code does not indicate success: 500 (Internal Server Error).');
        expect(error.statusCode).toBe(500);
    });

    it('GET does not treat 401 specially', async () => {
        const unauthorized = respond(401, '');
        await expect(frontend(unauthorized.httpFetch).getIllustrationsAnonymous()).rejects.toBeInstanceOf(HttpRequestException);
        expect(unauthorized.calls[0]!.init.method).toBe('GET');
        expect(unauthorized.calls[0]!.url).toBe('http://localhost:7071/api/V3IllustrationsAnonymous');
    });

    it('a JSON null response is "Empty response."', async () => {
        await expect(frontend(respond(200, 'null').httpFetch).search(identity, 'q', null)).rejects.toThrow('Empty response.');
    });

    it('cancellation → TaskCanceledException; a network failure → HttpRequestException', async () => {
        const controller = new AbortController();
        controller.abort();
        const aborting: HttpFetch = async (_url, init) => {
            if (init.signal?.aborted) throw new DOMException('The operation was aborted.', 'AbortError');
            return new Response('{}');
        };
        await expect(frontend(aborting).getChords(identity, ['a'], null, controller.signal)).rejects.toBeInstanceOf(TaskCanceledException);

        const failing: HttpFetch = async () => {
            throw new TypeError('Failed to fetch');
        };
        const error = await frontend(failing).getChords(identity, ['a'], null).catch(e => e);
        expect(error).toBeInstanceOf(HttpRequestException);
        expect(error.message).toBe('TypeError: Failed to fetch');
    });

    it('a missing endpoint fails the call', async () => {
        const client = FrontendApiClient.create(configuration({}), respond(200, '{}').httpFetch);
        await expect(client.search(identity, 'q', null)).rejects.toThrow('The endpoint for conditional stream \'\' is not configured, the default fallback value is not available, either.');
    });
});
