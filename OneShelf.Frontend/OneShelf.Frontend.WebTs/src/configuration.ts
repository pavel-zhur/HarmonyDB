// The application's settings, as Blazor WebAssembly gives them to the original (WebAssemblyHostBuilder.CreateDefault):
//
// - The environment name comes from the "Blazor-Environment" (or "DotNet-Environment") response header the server
//   sends with the boot files, "Production" when there is none. The original reads it from the response to
//   _framework/blazor.boot.json; here it is read from the response to appsettings.json (the servers set the header on
//   every file they serve: the original's .NET development server; here docker/frontend-web/nginx.conf.template).
// - appsettings.json, then appsettings.{Environment}.json on top of it, each loaded only if it was among the site's
//   files when it was built (the boot manifest lists them), fetched without the HTTP cache.
// - Each file is read by the JSON configuration provider: comments and trailing commas allowed, objects flattened to
//   keys joined by ":", values as strings (true -> "True", null -> ""), an empty object or array -> null, a key given
//   twice -> an error. Keys are case-insensitive; a later file overrides an earlier one key by key.
//
// Use: configuration.get('TelegramBotId'), configuration.getSection('FrontendOptions').get('LocalSourceName'),
// configuration.getBoolean('HideUnstable') (= GetValue<bool>), hostEnvironment.environment.

declare const __APPSETTINGS_FILES__: string[];

const keyDelimiter = ':';

type Data = Map<string, { key: string; value: string | null }>;

export interface ConfigurationSection {
    readonly key: string;
    readonly path: string;
    /** The value at this path: null when there is none. */
    readonly value: string | null;
    /** The value at the path below this one (IConfiguration's indexer). */
    get(key: string): string | null;
    getSection(key: string): ConfigurationSection;
    getChildren(): ConfigurationSection[];
}

export interface Configuration {
    /** The value at the path (IConfiguration's indexer, and GetValue<string>): null when there is none. */
    get(key: string): string | null;
    getSection(key: string): ConfigurationSection;
    getChildren(): ConfigurationSection[];
    /** ConfigurationBinder.GetValue<bool>: false when there is no value, an error when the value is not a boolean. */
    getBoolean(key: string): boolean;
}

export interface HostEnvironment {
    readonly environment: string;
}

let loaded: { configuration: Configuration; hostEnvironment: HostEnvironment } | undefined;

function getLoaded() {
    if (loaded === undefined) {
        throw new Error('The configuration is not loaded yet: loadConfiguration() runs before the application starts.');
    }

    return loaded;
}

export const configuration: Configuration = {
    get: key => getLoaded().configuration.get(key),
    getSection: key => getLoaded().configuration.getSection(key),
    getChildren: () => getLoaded().configuration.getChildren(),
    getBoolean: key => getLoaded().configuration.getBoolean(key),
};

export const hostEnvironment: HostEnvironment = {
    get environment() {
        return getLoaded().hostEnvironment.environment;
    },
};

export async function loadConfiguration(): Promise<void> {
    const files = __APPSETTINGS_FILES__;
    if (!files.includes('appsettings.json')) {
        throw new Error('appsettings.json is not among the site\'s files; the environment is read from its response.');
    }

    const response = await fetchSettings('appsettings.json');
    const environment = getEnvironment(response);
    const providers = [parseJsonConfiguration(await response.text())];

    const environmentFile = `appsettings.${environment}.json`;
    if (files.includes(environmentFile)) {
        providers.push(parseJsonConfiguration(await (await fetchSettings(environmentFile)).text()));
    }

    loaded = { configuration: createConfiguration(providers), hostEnvironment: { environment } };
}

async function fetchSettings(file: string): Promise<Response> {
    const response = await fetch(file, { cache: 'no-cache' });
    if (!response.ok) {
        throw new Error(`Failed to load '${file}': ${response.status} ${response.statusText}`);
    }

    return response;
}

function getEnvironment(response: Response): string {
    return response.headers.get('Blazor-Environment') || response.headers.get('DotNet-Environment') || 'Production';
}

// --- ConfigurationRoot over JSON configuration providers ---

export function createConfiguration(providers: Data[]): Configuration {
    const getValue = (path: string): string | null => {
        const normalized = normalizeKey(path);
        for (let i = providers.length - 1; i >= 0; i--) {
            const entry = providers[i]!.get(normalized);
            if (entry !== undefined) return entry.value;
        }

        return null;
    };

    const getChildren = (path: string | null): ConfigurationSection[] => {
        let keys: string[] = [];
        for (const provider of providers) {
            keys = getChildKeys(provider, keys, path);
        }

        const seen = new Set<string>();
        const distinct = keys.filter(key => {
            const normalized = normalizeKey(key);
            if (seen.has(normalized)) return false;
            seen.add(normalized);
            return true;
        });

        return distinct.map(key => getSection(path === null ? key : path + keyDelimiter + key));
    };

    const getSection = (path: string): ConfigurationSection => ({
        key: path.substring(path.lastIndexOf(keyDelimiter) + 1),
        path,
        get value() {
            return getValue(path);
        },
        get: key => getValue(path + keyDelimiter + key),
        getSection: key => getSection(path + keyDelimiter + key),
        getChildren: () => getChildren(path),
    });

    return {
        get: getValue,
        getSection,
        getChildren: () => getChildren(null),
        getBoolean: key => {
            const value = getValue(key);
            if (value === null) return false;
            const trimmed = value.trim();
            if (/^true$/i.test(trimmed)) return true;
            if (/^false$/i.test(trimmed)) return false;
            throw new Error(`Failed to convert configuration value at '${key}' to type 'System.Boolean'.`);
        },
    };
}

// ConfigurationProvider.GetChildKeys: this provider's child keys and the earlier ones, sorted.
function getChildKeys(data: Data, earlierKeys: string[], parentPath: string | null): string[] {
    const results: string[] = [];
    for (const { key } of data.values()) {
        if (parentPath === null) {
            results.push(segment(key, 0));
        } else if (key.length > parentPath.length
            && normalizeKey(key.substring(0, parentPath.length)) === normalizeKey(parentPath)
            && key[parentPath.length] === keyDelimiter) {
            results.push(segment(key, parentPath.length + 1));
        }
    }

    results.push(...earlierKeys);
    return results.sort(compareConfigurationKeys);
}

function segment(key: string, prefixLength: number): string {
    const index = key.indexOf(keyDelimiter, prefixLength);
    return index < 0 ? key.substring(prefixLength) : key.substring(prefixLength, index);
}

// ConfigurationKeyComparer: segment by segment; integers numerically and before other segments; others ordinal,
// ignoring case.
export function compareConfigurationKeys(x: string, y: string): number {
    const xParts = x.split(keyDelimiter).filter(p => p !== '');
    const yParts = y.split(keyDelimiter).filter(p => p !== '');
    for (let i = 0; i < Math.min(xParts.length, yParts.length); i++) {
        const result = compareSegments(xParts[i]!, yParts[i]!);
        if (result !== 0) return result;
    }

    return xParts.length === yParts.length ? 0 : xParts.length < yParts.length ? -1 : 1;
}

function compareSegments(a: string, b: string): number {
    const aInt = parseInt32(a);
    const bInt = parseInt32(b);
    if (aInt === undefined && bInt === undefined) {
        const au = normalizeKey(a);
        const bu = normalizeKey(b);
        return au < bu ? -1 : au > bu ? 1 : 0;
    }

    if (aInt !== undefined && bInt !== undefined) return aInt - bInt;
    return aInt !== undefined ? -1 : 1;
}

function parseInt32(s: string): number | undefined {
    if (!/^\s*[+-]?\d+\s*$/.test(s)) return undefined;
    const value = Number(s.trim());
    return value >= -2147483648 && value <= 2147483647 ? value : undefined;
}

// Keys compare ordinally ignoring case.
function normalizeKey(key: string): string {
    let result = '';
    for (const c of key) {
        const upper = c.toUpperCase();
        result += upper.length === 1 ? upper : c;
    }

    return result;
}

// --- JsonConfigurationFileParser ---

export function parseJsonConfiguration(text: string): Data {
    const data: Data = new Map();
    const reader = new JsonReader(text);
    const paths: string[] = [];

    const setValue = (value: string | null, leaf: boolean) => {
        const key = paths[paths.length - 1]!;
        const normalized = normalizeKey(key);
        if (leaf && data.has(normalized)) {
            throw new Error(`A duplicate key '${key}' was found.`);
        }

        data.set(normalized, { key: data.get(normalized)?.key ?? key, value });
    };

    const visitValue = (): void => {
        const token = reader.peek();
        if (token === '{') {
            visitObject();
        } else if (token === '[') {
            visitArray();
        } else {
            setValue(reader.readScalar(), true);
        }
    };

    const visitObject = (): void => {
        let isEmpty = true;
        reader.readObject(name => {
            isEmpty = false;
            paths.push(paths.length > 0 ? paths[paths.length - 1] + keyDelimiter + name : name);
            visitValue();
            paths.pop();
        });

        if (isEmpty && paths.length > 0) setValue(null, false);
    };

    const visitArray = (): void => {
        let index = 0;
        reader.readArray(() => {
            paths.push(paths[paths.length - 1] + keyDelimiter + index);
            visitValue();
            paths.pop();
            index++;
        });

        if (index === 0 && paths.length > 0) setValue(null, false);
    };

    if (reader.peek() !== '{') {
        throw new Error('Top-level JSON element must be an object.');
    }

    visitObject();
    reader.end();
    return data;
}

// JSON with comments (skipped) and trailing commas, as System.Text.Json reads the settings files.
class JsonReader {
    private i = 0;

    constructor(private readonly text: string) {
    }

    peek(): string {
        this.skip();
        const c = this.text[this.i];
        if (c === undefined) this.fail('Unexpected end of JSON');
        return c;
    }

    end(): void {
        this.skip();
        if (this.i < this.text.length) this.fail('Unexpected content after the JSON value');
    }

    readObject(onProperty: (name: string) => void): void {
        this.expect('{');
        while (this.peek() !== '}') {
            if (this.peek() !== '"') this.fail('Expected a property name');
            const name = this.readString();
            this.expect(':');
            onProperty(name);
            if (this.peek() === ',') this.i++;
            else if (this.peek() !== '}') this.fail('Expected "," or "}"');
        }

        this.i++;
    }

    readArray(onItem: () => void): void {
        this.expect('[');
        while (this.peek() !== ']') {
            onItem();
            if (this.peek() === ',') this.i++;
            else if (this.peek() !== ']') this.fail('Expected "," or "]"');
        }

        this.i++;
    }

    // JsonElement.ToString(): a string's value, a number's text, "True", "False", "" for null.
    readScalar(): string {
        const c = this.peek();
        if (c === '"') return this.readString();
        for (const [literal, value] of [['true', 'True'], ['false', 'False'], ['null', '']] as const) {
            if (this.text.startsWith(literal, this.i)) {
                this.i += literal.length;
                return value;
            }
        }

        const number = /^-?(0|[1-9]\d*)(\.\d+)?([eE][+-]?\d+)?/.exec(this.text.substring(this.i));
        if (number === null) this.fail('Unexpected character');
        this.i += number[0].length;
        return number[0];
    }

    private readString(): string {
        const match = /^"(?:[^"\\\u0000-\u001f]|\\(?:["\\/bfnrt]|u[0-9a-fA-F]{4}))*"/.exec(this.text.substring(this.i));
        if (match === null) this.fail('Invalid string');
        this.i += match[0].length;
        return JSON.parse(match[0]) as string;
    }

    private expect(c: string): void {
        if (this.peek() !== c) this.fail(`Expected "${c}"`);
        this.i++;
    }

    private skip(): void {
        const text = this.text;
        while (this.i < text.length) {
            const c = text[this.i]!;
            if (c === ' ' || c === '\t' || c === '\n' || c === '\r') {
                this.i++;
            } else if (text.startsWith('//', this.i)) {
                const end = text.indexOf('\n', this.i);
                this.i = end < 0 ? text.length : end + 1;
            } else if (text.startsWith('/*', this.i)) {
                const end = text.indexOf('*/', this.i + 2);
                if (end < 0) this.fail('Unterminated comment');
                this.i = end + 2;
            } else {
                break;
            }
        }
    }

    private fail(message: string): never {
        throw new Error(`${message} at position ${this.i} of the settings file.`);
    }
}
