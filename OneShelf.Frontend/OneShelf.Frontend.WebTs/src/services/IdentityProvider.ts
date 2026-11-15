// The original: Services/IdentityProvider.cs.
//
// localStorage "authv2" holds the Telegram login data as the widget gave it (its JSON text, unchanged).
// Set(object? user) takes that text here: Receiver passes the JSON of the widget's object (what JsonElement.ToString()
// gives in the original), Login's TempLogin its string.

import type { ILogger } from '../framework/logging';
import { navigationManager } from '../framework/navigation';
import { Identity } from '../lib/api/OneShelf.Authorization.Api.Model/Identity';
import { UnauthorizedException } from '../lib/api/OneShelf.Common.Api.Common/UnauthorizedException';
import { InvalidOperationException } from '../lib/common/dotnet/exceptions';
import { defaultJsonSerializerOptions, deserialize } from '../lib/common/dotnet/json/JsonSerializer';
import { DelegateEvent } from '../dotnet/events';
import type { ApplicationInsights } from '../packages/BlazorApplicationInsights/ApplicationInsights';
import type { LocalStorageService } from '../packages/Blazored.LocalStorage/LocalStorageService';
import { AsyncLock } from '../packages/Nito.AsyncEx/AsyncLock';

export class IdentityProvider {
    private static readonly AuthKey = 'authv2';

    private readonly _logger: ILogger;
    private readonly _localStorageService: LocalStorageService;
    private readonly _lock = new AsyncLock();
    private readonly _appInsights: ApplicationInsights;

    private _identity: Identity | null = null;
    private _isInitialized = false;

    private _appInsightsInitExceptions = 0;
    private _appInsightsSetUserId: string | null = null;

    constructor(logger: ILogger, localStorageService: LocalStorageService, appInsights: ApplicationInsights) {
        this._logger = logger;
        this._localStorageService = localStorageService;
        this._appInsights = appInsights;
    }

    readonly identityChange = new DelegateEvent();

    get identity(): Identity | null {
        if (!this._isInitialized) throw new InvalidOperationException('Not initialized.');
        return this._identity;
    }

    get requiredIdentity(): Identity {
        const identity = this.identity;
        if (identity === null) {
            this.navigateToLogin();
            throw new UnauthorizedException('No identity stored.');
        }

        return identity;
    }

    ensureAuthorized(): boolean {
        if (this.identity === null) {
            this.navigateToLogin();
            return false;
        }

        return true;
    }

    private navigateToLogin(): void {
        navigationManager.navigateTo('/login');
    }

    /** Given to the API client as its `unauthorized` callback. */
    readonly navigateToUserUnauthorizedIdentity = (): void => {
        navigationManager.navigateTo('/user?Unauthorized=true');
    };

    async set(user: string | null): Promise<void> {
        const _ = await this._lock.lockAsync();
        try {
            if (user !== null) {
                const data = user;
                this._identity = deserialize(data, Identity.json, defaultJsonSerializerOptions)!;
                await this._localStorageService.setItemAsStringAsync(IdentityProvider.AuthKey, data);
            } else {
                this._identity = null;
                await this._localStorageService.removeItemAsync(IdentityProvider.AuthKey);
            }

            await this.setApplicationInsights();
            this.onIdentityChange();
        } finally {
            _.dispose();
        }
    }

    async initialize(): Promise<void> {
        if (this._isInitialized) throw new InvalidOperationException('Already initialized.');

        const value = await this._localStorageService.getItemAsStringAsync(IdentityProvider.AuthKey);
        if (value !== null) {
            this._identity = deserialize(value, Identity.json, defaultJsonSerializerOptions)!;
            await this.setApplicationInsights();
        }

        this._isInitialized = true;
    }

    private async setApplicationInsights(): Promise<void> {
        try {
            const authenticatedUserId = this._identity?.id.toString() ?? null;
            if (authenticatedUserId === this._appInsightsSetUserId) return;
            this._appInsightsSetUserId = authenticatedUserId;

            this._appInsights.initJSRuntime();

            if (authenticatedUserId !== null) {
                await this._appInsights.setAuthenticatedUserContext(authenticatedUserId);
            } else {
                await this._appInsights.clearAuthenticatedUserContext();
            }
        } catch (e) {
            this._appInsightsInitExceptions++;

            if (this._appInsightsInitExceptions < 5) {
                this._logger.logError(e, 'Error setting the application insights user.');
            }
        }
    }

    protected onIdentityChange(): void {
        this.identityChange.invoke();
    }
}
