// Blazored.LocalStorage 4.5.0 (LocalStorageService over BrowserStorageProvider, SystemTextJsonSerializer), as
// Program.cs registers it: AddBlazoredLocalStorageAsSingleton(o => o.JsonSerializerOptions.UnmappedMemberHandling =
// JsonUnmappedMemberHandling.Skip). The serializer options are `new JsonSerializerOptions()` (property names as in
// C#, enums as numbers, nulls written; Skip is the default anyway) plus the package's TimespanJsonConverter (no stored
// type has a TimeSpan).
//
// What it does, as the package does it:
// - the async methods call window.localStorage through JS interop: the browser call happens at once, the result
//   comes back asynchronously;
// - GetItemAsync<T>: a missing, empty or white-space value gives default (null); otherwise the JSON is deserialized
//   (a JsonException propagates, except for T = string, where a value that is not JSON comes back as it is);
// - SetItemAsync<T> / SetItemAsStringAsync first read the old value (GetItemInternalAsync<object>, for the Changing
//   event: deserialized as a JsonElement, or kept as a string when it is not JSON), raise Changing (a handler may
//   cancel), write, raise Changed;
// - an empty or white-space key gives ArgumentNullException ("key"), a null string value ArgumentNullException ("data");
// - a browser that refuses access to localStorage gives BrowserStorageDisabledException.

import { JsonException } from '../../lib/common/dotnet/exceptions';
import { JsonElement, parseJson } from '../../lib/common/dotnet/json/JsonNode';
import { defaultJsonSerializerOptions, deserialize, jsonString, serialize, type JsonContract } from '../../lib/common/dotnet/json/JsonSerializer';
import { DelegateEvent } from '../../dotnet/events';

export class ArgumentNullException extends Error {
    readonly paramName: string;

    constructor(paramName: string) {
        super(`Value cannot be null. (Parameter '${paramName}')`);
        this.name = 'ArgumentNullException';
        this.paramName = paramName;
    }
}

export class BrowserStorageDisabledException extends Error {
    constructor(message: string, readonly innerException: unknown) {
        super(message);
        this.name = 'BrowserStorageDisabledException';
    }
}

export interface ChangingEventArgs {
    key: string;
    oldValue: unknown;
    newValue: unknown;
    cancel: boolean;
}

export interface ChangedEventArgs {
    key: string;
    oldValue: unknown;
    newValue: unknown;
}

const storageNotAvailableMessage = 'Unable to access the browser storage. This is most likely due to the browser settings.';

/** BrowserStorageProvider: each call wrapped as the package wraps it. */
function storage<T>(action: (localStorage: Storage) => T): T {
    try {
        return action(window.localStorage);
    } catch (e) {
        if (e instanceof Error && e.message.includes('Failed to read the \'localStorage\' property from \'Window\'')) {
            throw new BrowserStorageDisabledException(storageNotAvailableMessage, e);
        }

        throw e;
    }
}

function isNullOrWhiteSpace(value: string | null | undefined): value is null | undefined | '' {
    return value == null || value.trim().length === 0;
}

export class LocalStorageService {
    readonly changing = new DelegateEvent<[sender: LocalStorageService, e: ChangingEventArgs]>();
    readonly changed = new DelegateEvent<[sender: LocalStorageService, e: ChangedEventArgs]>();

    async setItemAsync<T>(key: string, data: T, contract: JsonContract<T>): Promise<void> {
        if (isNullOrWhiteSpace(key)) throw new ArgumentNullException('key');
        const e = await this.raiseOnChangingAsync(key, data);
        if (e.cancel) return;
        const serialized = serialize(data, contract, defaultJsonSerializerOptions);
        await Promise.resolve(storage(s => s.setItem(key, serialized)));
        this.raiseOnChanged(key, e.oldValue, data);
    }

    async setItemAsStringAsync(key: string, data: string): Promise<void> {
        if (isNullOrWhiteSpace(key)) throw new ArgumentNullException('key');
        if (data == null) throw new ArgumentNullException('data');
        const e = await this.raiseOnChangingAsync(key, data);
        if (e.cancel) return;
        await Promise.resolve(storage(s => s.setItem(key, data)));
        this.raiseOnChanged(key, e.oldValue, data);
    }

    async getItemAsync<T>(key: string, contract: JsonContract<T>): Promise<T | null> {
        if (isNullOrWhiteSpace(key)) throw new ArgumentNullException('key');
        const text = await Promise.resolve(storage(s => s.getItem(key)));
        if (isNullOrWhiteSpace(text)) return null;
        return LocalStorageService.deserializeOrString(text, contract);
    }

    async getItemAsStringAsync(key: string): Promise<string | null> {
        if (isNullOrWhiteSpace(key)) throw new ArgumentNullException('key');
        return await Promise.resolve(storage(s => s.getItem(key)));
    }

    async removeItemAsync(key: string): Promise<void> {
        if (isNullOrWhiteSpace(key)) throw new ArgumentNullException('key');
        await Promise.resolve(storage(s => s.removeItem(key)));
    }

    async clearAsync(): Promise<void> {
        await Promise.resolve(storage(s => s.clear()));
    }

    async lengthAsync(): Promise<number> {
        return await Promise.resolve(storage(s => s.length));
    }

    async keyAsync(index: number): Promise<string | null> {
        return await Promise.resolve(storage(s => s.key(index)));
    }

    async keysAsync(): Promise<string[]> {
        return await Promise.resolve(storage(s => Object.keys(s)));
    }

    async containKeyAsync(key: string): Promise<boolean> {
        return await Promise.resolve(storage(s => Object.prototype.hasOwnProperty.call(s, key)));
    }

    async removeItemsAsync(keys: Iterable<string>): Promise<void> {
        for (const key of keys) {
            await Promise.resolve(storage(s => s.removeItem(key)));
        }
    }

    // ISyncLocalStorageService

    keys(): string[] {
        return storage(s => Object.keys(s));
    }

    setItem<T>(key: string, data: T, contract: JsonContract<T>): void {
        if (isNullOrWhiteSpace(key)) throw new ArgumentNullException('key');
        const e = this.raiseOnChangingSync(key, data);
        if (e.cancel) return;
        const serialized = serialize(data, contract, defaultJsonSerializerOptions);
        storage(s => s.setItem(key, serialized));
        this.raiseOnChanged(key, e.oldValue, data);
    }

    setItemAsString(key: string, data: string): void {
        if (isNullOrWhiteSpace(key)) throw new ArgumentNullException('key');
        if (data == null) throw new ArgumentNullException('data');
        const e = this.raiseOnChangingSync(key, data);
        if (e.cancel) return;
        storage(s => s.setItem(key, data));
        this.raiseOnChanged(key, e.oldValue, data);
    }

    getItem<T>(key: string, contract: JsonContract<T>): T | null {
        if (isNullOrWhiteSpace(key)) throw new ArgumentNullException('key');
        const text = storage(s => s.getItem(key));
        if (isNullOrWhiteSpace(text)) return null;
        return LocalStorageService.deserializeOrString(text, contract);
    }

    getItemAsString(key: string): string | null {
        if (isNullOrWhiteSpace(key)) throw new ArgumentNullException('key');
        return storage(s => s.getItem(key));
    }

    removeItem(key: string): void {
        if (isNullOrWhiteSpace(key)) throw new ArgumentNullException('key');
        storage(s => s.removeItem(key));
    }

    removeItems(keys: Iterable<string>): void {
        if (keys == null) throw new ArgumentNullException('keys');
        for (const key of keys) {
            storage(s => s.removeItem(key));
        }
    }

    clear(): void {
        storage(s => s.clear());
    }

    length(): number {
        return storage(s => s.length);
    }

    key(index: number): string | null {
        return storage(s => s.key(index));
    }

    containKey(key: string): boolean {
        return storage(s => Object.prototype.hasOwnProperty.call(s, key));
    }

    private static deserializeOrString<T>(text: string, contract: JsonContract<T>): T {
        try {
            return deserialize(text, contract, defaultJsonSerializerOptions);
        } catch (e) {
            // catch (JsonException ex) when (ex.Path == "$" && typeof(T) == typeof(string))
            if (e instanceof JsonException && (contract as unknown) === jsonString) {
                return text as unknown as T;
            }

            throw e;
        }
    }

    private async raiseOnChangingAsync(key: string, data: unknown): Promise<ChangingEventArgs> {
        const e: ChangingEventArgs = { key, oldValue: null, newValue: null, cancel: false };
        e.oldValue = await this.getItemInternalAsync(key);
        e.newValue = data;
        this.changing.invoke(this, e);
        return e;
    }

    private raiseOnChangingSync(key: string, data: unknown): ChangingEventArgs {
        const e: ChangingEventArgs = { key, oldValue: this.getItemInternal(key), newValue: data, cancel: false };
        this.changing.invoke(this, e);
        return e;
    }

    /** GetItemInternalAsync<object> */
    private async getItemInternalAsync(key: string): Promise<unknown> {
        if (key == null || key.length === 0) throw new ArgumentNullException('key');
        const text = await Promise.resolve(storage(s => s.getItem(key)));
        if (isNullOrWhiteSpace(text)) return null;
        return LocalStorageService.deserializeObject(text);
    }

    private getItemInternal(key: string): unknown {
        if (key == null || key.length === 0) throw new ArgumentNullException('key');
        const text = storage(s => s.getItem(key));
        if (isNullOrWhiteSpace(text)) return null;
        return LocalStorageService.deserializeObject(text);
    }

    /** JsonSerializer.Deserialize<object>: a JsonElement; text that is not JSON is kept as the string. */
    private static deserializeObject(text: string): unknown {
        try {
            const node = parseJson(text);
            return new JsonElement(node);
        } catch (e) {
            if (e instanceof JsonException) return text;
            throw e;
        }
    }

    private raiseOnChanged(key: string, oldValue: unknown, data: unknown): void {
        this.changed.invoke(this, { key, oldValue, newValue: data });
    }
}
