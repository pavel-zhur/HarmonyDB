// ILogger<T> as the original has it: Microsoft.Extensions.Logging with the minimum level Information (AddLogging's
// default; the site has no "Logging" settings), and two providers:
//
// - Blazor WebAssembly's console logger (WebAssemblyConsoleLogger, .NET 9): one console call per message,
//     "{trce|dbug|info|warn|fail|crit}: {category}[{eventId}]\n      {message}" (each further line of the message
//     indented the same), then "\n{exception}" when there is one; console.debug for Trace/Debug, console.info,
//     console.warn, console.error, and for Critical Blazor's dotNetCriticalError: console.error and the error bar.
// - BlazorApplicationInsights' logger (ApplicationInsightsLoggerProvider, AddBlazorApplicationInsights's default
//   addWasmLogger: true, its default options: every level, category name and scopes included): a message without an
//   exception is appInsights.trackTrace, with one appInsights.trackException.
//
// The category is the C# type name of ILogger<T> (e.g. "OneShelf.Frontend.Web.Services.CollectionIndexProvider").
// Message templates are formatted as LogValuesFormatter does: "{name}" holes take the arguments in order, null is
// "(null)", a collection is its items joined with ", ".
//
// An exception's text (Exception.ToString() in .NET: "Type: message" and the stack) is here the JavaScript error's
// stack, which starts with "Name: message".

import { applicationInsightsLoggerProvider } from '../packages/BlazorApplicationInsights/ApplicationInsightsLoggerProvider';
import { showErrorNotification } from './errorUi';

export enum LogLevel {
    Trace = 0,
    Debug = 1,
    Information = 2,
    Warning = 3,
    Error = 4,
    Critical = 5,
    None = 6,
}

const minimumLevel = LogLevel.Information;

export interface ILogger {
    log(logLevel: LogLevel, exception: unknown, message: string | null, ...args: unknown[]): void;
    logTrace(message: string | null, ...args: unknown[]): void;
    logDebug(message: string | null, ...args: unknown[]): void;
    logInformation(message: string | null, ...args: unknown[]): void;
    logWarning(message: string | null, ...args: unknown[]): void;
    /** LogError(Exception, string, params object[]) â€” the only form the site uses. */
    logError(exception: unknown, message: string | null, ...args: unknown[]): void;
    logCritical(exception: unknown, message: string | null, ...args: unknown[]): void;
}

/** What a provider gets for one message. */
export interface LogEntry {
    readonly logLevel: LogLevel;
    readonly category: string;
    readonly eventId: number;
    readonly eventName?: string | null;
    /** The scopes the message is logged in, outermost first: their key-value pairs, or a text. */
    readonly scopes?: readonly (string | readonly (readonly [key: string, value: unknown])[])[];
    /** The formatted message. */
    readonly message: string;
    readonly exception: unknown;
    /** The state's key-value pairs (FormattedLogValues): the named holes with their values, then "{OriginalFormat}". */
    readonly state: readonly (readonly [key: string, value: unknown])[];
}

const loggers = new Map<string, ILogger>();

/** `ILogger<T>` for the category (the C# full type name). */
export function getLogger(category: string): ILogger {
    let logger = loggers.get(category);
    if (logger === undefined) {
        logger = createLogger(category);
        loggers.set(category, logger);
    }

    return logger;
}

/** Logger.Log: the message goes to each provider; their failures are thrown together after all have been called. */
export function writeLog(entry: LogEntry): void {
    if (entry.logLevel < minimumLevel || entry.logLevel === LogLevel.None) return;
    const errors: unknown[] = [];
    for (const provider of [writeToConsole, applicationInsightsLoggerProvider.log]) {
        try {
            provider(entry);
        } catch (e) {
            errors.push(e);
        }
    }

    if (errors.length > 0) {
        throw new AggregateError(errors, 'An error occurred while writing to logger(s).');
    }
}

function createLogger(category: string): ILogger {
    const log = (logLevel: LogLevel, exception: unknown, message: string | null, ...args: unknown[]) => {
        if (logLevel < minimumLevel || logLevel === LogLevel.None) return;
        const { formatted, state } = formatLogValues(message, args);
        writeLog({ logLevel, category, eventId: 0, message: formatted, exception: exception ?? null, state });
    };

    return {
        log,
        logTrace: (message, ...args) => log(LogLevel.Trace, null, message, ...args),
        logDebug: (message, ...args) => log(LogLevel.Debug, null, message, ...args),
        logInformation: (message, ...args) => log(LogLevel.Information, null, message, ...args),
        logWarning: (message, ...args) => log(LogLevel.Warning, null, message, ...args),
        logError: (exception, message, ...args) => log(LogLevel.Error, exception, message, ...args),
        logCritical: (exception, message, ...args) => log(LogLevel.Critical, exception, message, ...args),
    };
}

/** FormattedLogValues / LogValuesFormatter. */
export function formatLogValues(format: string | null, args: readonly unknown[]): { formatted: string; state: (readonly [string, unknown])[] } {
    if (format === null) {
        return { formatted: '[null]', state: [['{OriginalFormat}', '[null]']] };
    }

    if (args.length === 0) {
        return { formatted: format, state: [['{OriginalFormat}', format]] };
    }

    const names: string[] = [];
    let formatted = '';
    let i = 0;
    let argIndex = 0;
    while (i < format.length) {
        const c = format[i]!;
        if (c === '{' && format[i + 1] === '{') {
            formatted += '{';
            i += 2;
        } else if (c === '}' && format[i + 1] === '}') {
            formatted += '}';
            i += 2;
        } else if (c === '{') {
            const end = format.indexOf('}', i);
            if (end < 0) {
                formatted += format.substring(i);
                break;
            }

            const hole = format.substring(i + 1, end);
            const nameEnd = hole.search(/[,:]/);
            names.push(nameEnd < 0 ? hole : hole.substring(0, nameEnd));
            formatted += formatValue(args[argIndex++]);
            i = end + 1;
        } else {
            formatted += c;
            i++;
        }
    }

    const state: (readonly [string, unknown])[] = names.map((name, index) => [name, args[index]] as const);
    state.push(['{OriginalFormat}', format]);
    return { formatted, state };
}

function formatValue(value: unknown): string {
    if (value === null || value === undefined) return '(null)';
    if (typeof value !== 'string' && typeof (value as Iterable<unknown>)[Symbol.iterator] === 'function') {
        return [...(value as Iterable<unknown>)].map(x => x == null ? '(null)' : String(x)).join(', ');
    }

    return String(value);
}

const logLevelStrings: Record<number, string> = {
    [LogLevel.Trace]: 'trce',
    [LogLevel.Debug]: 'dbug',
    [LogLevel.Information]: 'info',
    [LogLevel.Warning]: 'warn',
    [LogLevel.Error]: 'fail',
    [LogLevel.Critical]: 'crit',
};

const messagePadding = '      ';

/** Exception.ToString() */
export function exceptionToString(exception: unknown): string {
    if (exception instanceof Error) {
        return exception.stack ?? `${exception.name}: ${exception.message}`;
    }

    return String(exception);
}

function writeToConsole(entry: LogEntry): void {
    if (entry.message.length === 0 && entry.exception === null) return;

    let text = `${logLevelStrings[entry.logLevel]}: ${entry.category}[${entry.eventId}]`;
    if (entry.message.length > 0) {
        text += '\n' + messagePadding + entry.message.split('\n').join('\n' + messagePadding);
    }

    if (entry.exception !== null) {
        text += '\n' + exceptionToString(entry.exception);
    }

    switch (entry.logLevel) {
        case LogLevel.Trace:
        case LogLevel.Debug:
            console.debug(text);
            break;
        case LogLevel.Information:
            console.info(text);
            break;
        case LogLevel.Warning:
            console.warn(text);
            break;
        case LogLevel.Error:
            console.error(text);
            break;
        default:
            // Critical: Blazor's dotNetCriticalError, which also shows the error bar.
            console.error(text);
            showErrorNotification();
            break;
    }
}
