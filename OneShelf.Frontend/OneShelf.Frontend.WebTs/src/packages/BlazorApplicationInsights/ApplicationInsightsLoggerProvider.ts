// BlazorApplicationInsights 3.1.0: ApplicationInsightsLoggerProvider / ApplicationInsightsLogger with the default
// ApplicationInsightsLoggerOptions (MinLogLevel Trace, IncludeCategoryName, IncludeScopes), registered by
// AddBlazorApplicationInsights (addWasmLogger: true) for every ILogger.
//
// A message without an exception: appInsights.trackTrace({ message, severityLevel, properties });
// with one: appInsights.trackException({ id: "{eventId}", exception: { name, message, stack }, severityLevel, properties }).
// The properties: the scopes' key-value pairs, "CategoryName", "EventId" (when not 0), "EventName" (when there is one),
// then the state's key-value pairs ("{OriginalFormat}" as "OriginalFormat"), the values as strings.
// The calls are not awaited; when they fail (before the JS runtime is set, or in JavaScript) nothing is seen.

import type { LogEntry } from '../../framework/logging';
import { exceptionToString, LogLevel } from '../../framework/logging';
import { applicationInsights, SeverityLevel } from './ApplicationInsights';

function getSeverityLevel(logLevel: LogLevel): SeverityLevel {
    switch (logLevel) {
        case LogLevel.Information: return SeverityLevel.Information;
        case LogLevel.Warning: return SeverityLevel.Warning;
        case LogLevel.Error: return SeverityLevel.Error;
        case LogLevel.Critical: return SeverityLevel.Critical;
        default: return SeverityLevel.Verbose;
    }
}

/** Convert.ToString(value, CultureInfo.InvariantCulture): null gives "". */
function convertToString(value: unknown): string {
    return value == null ? '' : String(value);
}

function applyDictionary(target: Record<string, unknown>, source: readonly (readonly [string, unknown])[]): void {
    for (const [key, value] of source) {
        target[key === '{OriginalFormat}' ? 'OriginalFormat' : key] = convertToString(value);
    }
}

function getCustomDimensions(entry: LogEntry): Record<string, unknown> {
    const customDimensions: Record<string, unknown> = {};
    const scopeText: string[] = [];
    for (const scope of entry.scopes ?? []) {
        if (typeof scope === 'string') {
            scopeText.push(' => ' + scope);
        } else {
            applyDictionary(customDimensions, scope);
        }
    }

    if (scopeText.length > 0) {
        customDimensions['Scope'] = scopeText.join('');
    }

    if (entry.category.length > 0) {
        customDimensions['CategoryName'] = entry.category;
    }

    if (entry.eventId !== 0) {
        customDimensions['EventId'] = String(entry.eventId);
    }

    if (entry.eventName != null && entry.eventName.length > 0) {
        customDimensions['EventName'] = entry.eventName;
    }

    applyDictionary(customDimensions, entry.state);
    return customDimensions;
}

function exceptionTypeName(exception: unknown): string {
    return exception instanceof Error ? exception.name : typeof exception;
}

function exceptionMessage(exception: unknown): string {
    return exception instanceof Error ? exception.message : String(exception);
}

export const applicationInsightsLoggerProvider = {
    log(entry: LogEntry): void {
        const severityLevel = getSeverityLevel(entry.logLevel);
        const customDimensions = getCustomDimensions(entry);
        if (entry.exception === null) {
            applicationInsights.trackTrace({
                message: entry.message,
                severityLevel,
                iKey: null,
                properties: customDimensions,
            }).catch(() => {
            });
            return;
        }

        applicationInsights.trackException({
            exception: {
                name: exceptionTypeName(entry.exception),
                message: exceptionMessage(entry.exception),
                stack: exceptionToString(entry.exception),
            },
            id: entry.eventName ?? String(entry.eventId),
            severityLevel,
            properties: customDimensions,
        }).catch(() => {
        });
    },
};
