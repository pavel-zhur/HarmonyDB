// Microsoft.Extensions.Logging.ILogger<T>: the calls the ported libraries make (LogInformation, LogWarning with
// message-template arguments, LogError with an exception).

export interface ILogger {
    logInformation(message: string): void;
    logWarning(message: string, ...args: unknown[]): void;
    logError(error: unknown, message: string): void;
}
