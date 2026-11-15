// BlazorApplicationInsights 3.1.0: IApplicationInsights (ApplicationInsights) — the members the site reaches, each a
// JS interop call on the Application Insights SDK's global `appInsights` (set up by index.html's snippet). The
// arguments go to JavaScript as JSON (the JS runtime's options: camelCase, the models' [JsonPropertyName]; nulls are
// written, except the Config members marked WhenWritingNull).
//
// Until InitJSRuntime is called there is no JS runtime: each call fails (NullReferenceException in the original).

export enum SeverityLevel {
    Verbose = 0,
    Information = 1,
    Warning = 2,
    Error = 3,
    Critical = 4,
}

/** Models.Config: only the members the site sets; null ones are not written. */
export interface Config {
    connectionString?: string | null;
    enableAutoRouteTracking?: boolean | null;
}

export interface TraceTelemetry {
    message: string;
    severityLevel: SeverityLevel | null;
    iKey: string | null;
    properties: Record<string, unknown> | null;
}

export interface Error {
    name: string;
    message: string;
    stack: string | null;
}

export interface ExceptionTelemetry {
    id: string | null;
    exception: Error | null;
    severityLevel: SeverityLevel | null;
    properties: Record<string, unknown> | null;
}

interface AppInsightsGlobal {
    trackPageView(pageView: unknown): void;
    trackTrace(trace: unknown): void;
    trackException(exception: unknown): void;
    updateCfg(config: unknown, mergeExisting: boolean): void;
    setAuthenticatedUserContext(authenticatedUserId: string, accountId: string | null, storeInCookie: boolean | null): void;
    clearAuthenticatedUserContext(): void;
    flush(isAsync: boolean): void;
}

export class NullReferenceException extends globalThis.Error {
    constructor() {
        super('Object reference not set to an instance of an object.');
        this.name = 'NullReferenceException';
    }
}

/** IJSRuntime.InvokeVoidAsync("appInsights.x", ...): the arguments as JSON, the call made at once, the result later. */
async function invokeVoid(initialized: boolean, method: keyof AppInsightsGlobal, ...args: unknown[]): Promise<void> {
    if (!initialized) {
        throw new NullReferenceException();
    }

    const serialized = JSON.parse(JSON.stringify(args)) as unknown[];
    const appInsights = (window as unknown as { appInsights: AppInsightsGlobal }).appInsights;
    const target = appInsights[method] as (...a: unknown[]) => void;
    await Promise.resolve(target.apply(appInsights, serialized));
}

export class ApplicationInsights {
    private jsRuntimeInitialized = false;

    initJSRuntime(): void {
        this.jsRuntimeInitialized = true;
    }

    async trackPageView(pageView: unknown = null): Promise<void> {
        await invokeVoid(this.jsRuntimeInitialized, 'trackPageView', pageView);
    }

    async trackTrace(trace: TraceTelemetry): Promise<void> {
        await invokeVoid(this.jsRuntimeInitialized, 'trackTrace', trace);
    }

    async trackException(exception: ExceptionTelemetry): Promise<void> {
        await invokeVoid(this.jsRuntimeInitialized, 'trackException', exception);
    }

    async flush(): Promise<void> {
        await invokeVoid(this.jsRuntimeInitialized, 'flush', false);
    }

    async clearAuthenticatedUserContext(): Promise<void> {
        await invokeVoid(this.jsRuntimeInitialized, 'clearAuthenticatedUserContext');
    }

    async setAuthenticatedUserContext(authenticatedUserId: string, accountId: string | null = null, storeInCookie: boolean | null = null): Promise<void> {
        await invokeVoid(this.jsRuntimeInitialized, 'setAuthenticatedUserContext', authenticatedUserId, accountId, storeInCookie);
    }

    async updateCfg(newConfig: Config, mergeExisting = true): Promise<void> {
        await invokeVoid(this.jsRuntimeInitialized, 'updateCfg', withoutNulls(newConfig), mergeExisting);
    }
}

/** [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] on every Config member. */
function withoutNulls(config: Config): Record<string, unknown> {
    const result: Record<string, unknown> = {};
    for (const [key, value] of Object.entries(config)) {
        if (value != null) result[key] = value;
    }

    return result;
}

/** The singleton IApplicationInsights (TryAddSingleton<IApplicationInsights, ApplicationInsights>). */
export const applicationInsights = new ApplicationInsights();

/** ApplicationInsightsInitConfig, set by Program.cs's AddBlazorApplicationInsights(config => ...). */
export const applicationInsightsInitConfig: { config: Config | null } = { config: null };
