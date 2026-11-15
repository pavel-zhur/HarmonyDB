// ApiClientBase<TClient>: JSON over HTTP with `new JsonSerializerOptions { PropertyNamingPolicy = CamelCase }`.
// The address is `new Uri(endpoint, url)`: the method name resolved against the configured endpoint; the request goes
// to its AbsoluteUri.
// 401 → the `unauthorized` callback (POST only), then UnauthorizedException with the response text;
// 429 → ConcurrencyException; 460 → CacheItemNotFoundException; any other non-2xx → HttpRequestException;
// a JSON `null` response → "Empty response.". A cancelled request (the CancellationToken, here an AbortSignal)
// → TaskCanceledException; a network failure → HttpRequestException.
// Not ported, as the site does not reach them: PostWithCode (both), PostDirect, Ping, GetServiceIdentity, the trace
// bag (ApiTraceBag, ApiTraceItem, MaskIdentity, MaskedIdentityConverter) and conditional streams.

import { HttpRequestException, TaskCanceledException } from '../../common/dotnet/exceptions';
import { camelCaseJsonSerializerOptions, deserialize, serialize, type JsonContract } from '../../common/dotnet/json/JsonSerializer';
import { Uri } from '../../common/dotnet/Uri';
import { CacheItemNotFoundException } from '../OneShelf.Common.Api.Common/CacheItemNotFoundException';
import { ConcurrencyException } from '../OneShelf.Common.Api.Common/ConcurrencyException';
import { UnauthorizedException } from '../OneShelf.Common.Api.Common/UnauthorizedException';
import type { ApiClientOptions } from './ApiClientOptions';

/** The HTTP client (IHttpClientFactory's HttpClient): `fetch`, or a stand-in in tests. */
export type HttpFetch = (input: string, init: RequestInit) => Promise<Response>;

const jsonSerializerOptions = camelCaseJsonSerializerOptions;

export abstract class ApiClientBase {
    protected readonly options: ApiClientOptions;
    private readonly httpFetch: HttpFetch;

    protected constructor(options: ApiClientOptions, httpFetch: HttpFetch = (input, init) => fetch(input, init)) {
        this.options = options;
        this.httpFetch = httpFetch;
    }

    /** Post<TRequest, TResponse> (named apart from the clients' own methods: TypeScript has no overloads across classes). */
    protected async httpPost<TRequest, TResponse>(
        url: string,
        request: TRequest,
        requestContract: JsonContract<TRequest>,
        responseContract: JsonContract<TResponse>,
        unauthorized: (() => void) | null = null,
        cancellationToken?: AbortSignal,
    ): Promise<TResponse> {
        const uri = new Uri(this.options.getEndpoint(), url).absoluteUri;
        const httpResponseMessage = await this.send(uri, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json; charset=utf-8' },
            body: serialize(request, requestContract, jsonSerializerOptions),
            signal: cancellationToken,
        }, cancellationToken);

        if (httpResponseMessage.status === UnauthorizedException.StatusCode) {
            unauthorized?.();
            throw new UnauthorizedException(await this.readAsString(httpResponseMessage, cancellationToken));
        }

        if (httpResponseMessage.status === ConcurrencyException.StatusCode) {
            throw new ConcurrencyException();
        }

        if (httpResponseMessage.status === CacheItemNotFoundException.StatusCode) {
            throw new CacheItemNotFoundException();
        }

        ApiClientBase.ensureSuccessStatusCode(httpResponseMessage);
        return ApiClientBase.readResponse(await this.readAsString(httpResponseMessage, cancellationToken), responseContract);
    }

    /** Get<TResponse> */
    protected async httpGet<TResponse>(url: string, responseContract: JsonContract<TResponse>, cancellationToken?: AbortSignal): Promise<TResponse> {
        const uri = new Uri(this.options.getEndpoint(), url).absoluteUri;
        const httpResponseMessage = await this.send(uri, { method: 'GET', signal: cancellationToken }, cancellationToken);

        if (httpResponseMessage.status === ConcurrencyException.StatusCode) {
            throw new ConcurrencyException();
        }

        if (httpResponseMessage.status === CacheItemNotFoundException.StatusCode) {
            throw new CacheItemNotFoundException();
        }

        ApiClientBase.ensureSuccessStatusCode(httpResponseMessage);
        return ApiClientBase.readResponse(await this.readAsString(httpResponseMessage, cancellationToken), responseContract);
    }

    private async send(uri: string, init: RequestInit, cancellationToken: AbortSignal | undefined): Promise<Response> {
        try {
            return await this.httpFetch(uri, init);
        } catch (e) {
            throw ApiClientBase.translateError(e, cancellationToken);
        }
    }

    private async readAsString(response: Response, cancellationToken: AbortSignal | undefined): Promise<string> {
        try {
            return await response.text();
        } catch (e) {
            throw ApiClientBase.translateError(e, cancellationToken);
        }
    }

    private static translateError(e: unknown, cancellationToken: AbortSignal | undefined): Error {
        if (cancellationToken?.aborted || (e instanceof Error && e.name === 'AbortError')) {
            return new TaskCanceledException();
        }

        return new HttpRequestException(e instanceof Error ? `${e.name}: ${e.message}` : String(e));
    }

    /** HttpResponseMessage.EnsureSuccessStatusCode */
    private static ensureSuccessStatusCode(response: Response) {
        if (response.status < 200 || response.status > 299) {
            throw new HttpRequestException(`Response status code does not indicate success: ${response.status} (${response.statusText}).`, response.status);
        }
    }

    /** ReadFromJsonAsync<TResponse>(...) ?? throw new("Empty response.") */
    private static readResponse<TResponse>(text: string, responseContract: JsonContract<TResponse>): TResponse {
        const response = deserialize(text, responseContract, jsonSerializerOptions);
        if (response == null) throw new Error('Empty response.');
        return response;
    }
}
