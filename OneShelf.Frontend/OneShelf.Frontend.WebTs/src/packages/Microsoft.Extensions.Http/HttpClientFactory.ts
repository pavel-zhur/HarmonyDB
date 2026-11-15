// IHttpClientFactory.CreateClient() (Microsoft.Extensions.Http 8.0.0), the HttpClient the API clients use:
// the "Default" client with its logging handlers around the browser's fetch, and HttpClient's default timeout.
//
// - LoggingScopeHttpMessageHandler, category "System.Net.Http.HttpClient.Default.LogicalHandler": a scope
//   "HTTP {HttpMethod} {Uri}", "Start processing HTTP request {HttpMethod} {Uri}" (event 100), and when the response
//   headers have come "End processing HTTP request after {ElapsedMilliseconds}ms - {StatusCode}" (event 101);
// - LoggingHttpMessageHandler, category "System.Net.Http.HttpClient.Default.ClientHandler": "Sending HTTP request
//   {HttpMethod} {Uri}" (event 100), "Received HTTP response headers after {ElapsedMilliseconds}ms - {StatusCode}"
//   (event 101).
// A request that fails (no response) logs no end. The header logs are Trace, below the site's minimum level.
// - HttpClient.Timeout, 100 seconds: the request is cancelled then (TaskCanceledException).

import { formatLogValues, LogLevel, writeLog } from '../../framework/logging';
import type { HttpFetch } from '../../lib/api/OneShelf.Common.Api.Client/ApiClientBase';

const logicalCategory = 'System.Net.Http.HttpClient.Default.LogicalHandler';
const clientCategory = 'System.Net.Http.HttpClient.Default.ClientHandler';
const timeoutMilliseconds = 100 * 1000;

type Scope = readonly (readonly [string, unknown])[];

function log(category: string, eventId: number, eventName: string, scopes: readonly Scope[], format: string, ...args: unknown[]): void {
    const { formatted, state } = formatLogValues(format, args);
    writeLog({ logLevel: LogLevel.Information, category, eventId, eventName, scopes, message: formatted, exception: null, state });
}

/** TimeSpan.TotalMilliseconds of a stopwatch measurement (whole ticks of 100 ns, e.g. 2161.1999). */
function elapsedMilliseconds(started: number): number {
    return Math.floor((performance.now() - started) * 10000) / 10000;
}

export function createHttpClient(): HttpFetch {
    return async (input, init) => {
        const method = init.method ?? 'GET';
        const uri = new URL(input).href;

        const logicalStarted = performance.now();
        const scope: Scope = formatLogValues('HTTP {HttpMethod} {Uri}', [method, uri]).state;
        log(logicalCategory, 100, 'RequestPipelineStart', [scope], 'Start processing HTTP request {HttpMethod} {Uri}', method, uri);

        log(clientCategory, 100, 'RequestStart', [scope], 'Sending HTTP request {HttpMethod} {Uri}', method, uri);
        const clientStarted = performance.now();

        const controller = new AbortController();
        const callerSignal = init.signal ?? undefined;
        const onCallerAbort = () => controller.abort(callerSignal?.reason);
        if (callerSignal?.aborted) {
            controller.abort(callerSignal.reason);
        } else {
            callerSignal?.addEventListener('abort', onCallerAbort, { once: true });
        }

        const timeout = setTimeout(() => controller.abort(), timeoutMilliseconds);

        let response: Response;
        try {
            response = await fetch(input, { ...init, signal: controller.signal });
        } catch (e) {
            clearTimeout(timeout);
            callerSignal?.removeEventListener('abort', onCallerAbort);
            throw e;
        }

        log(clientCategory, 101, 'RequestEnd', [scope], 'Received HTTP response headers after {ElapsedMilliseconds}ms - {StatusCode}', elapsedMilliseconds(clientStarted), response.status);
        log(logicalCategory, 101, 'RequestPipelineEnd', [scope], 'End processing HTTP request after {ElapsedMilliseconds}ms - {StatusCode}', elapsedMilliseconds(logicalStarted), response.status);
        return response;
    };
}
