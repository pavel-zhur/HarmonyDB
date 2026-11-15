// The .NET exception types the ported libraries throw or the site catches by type, in .NET's hierarchy, with .NET's
// default messages. A plain C# `throw new("...")` (System.Exception) is a plain `Error` here.

export class ArgumentException extends Error {
    constructor(message = 'Value does not fall within the expected range.') {
        super(message);
        this.name = 'ArgumentException';
    }
}

export class ArgumentOutOfRangeException extends ArgumentException {
    constructor(paramName?: string, message?: string) {
        super(message ?? `Specified argument was out of the range of valid values.${paramName ? ` (Parameter '${paramName}')` : ''}`);
        this.name = 'ArgumentOutOfRangeException';
    }
}

export class InvalidOperationException extends Error {
    constructor(message = 'Operation is not valid due to the current state of the object.') {
        super(message);
        this.name = 'InvalidOperationException';
    }
}

export class InvalidCastException extends Error {
    constructor(message = 'Specified cast is not valid.') {
        super(message);
        this.name = 'InvalidCastException';
    }
}

export class KeyNotFoundException extends Error {
    constructor(key: unknown) {
        super(`The given key '${String(key)}' was not present in the dictionary.`);
        this.name = 'KeyNotFoundException';
    }
}

export class FormatException extends Error {
    constructor(message = 'One of the identified items was in an invalid format.') {
        super(message);
        this.name = 'FormatException';
    }
}

/** System.OverflowException (an ArithmeticException). */
export class OverflowException extends Error {
    constructor(message = 'Arithmetic operation resulted in an overflow.') {
        super(message);
        this.name = 'OverflowException';
    }
}

/** System.DivideByZeroException (an ArithmeticException). */
export class DivideByZeroException extends Error {
    constructor(message = 'Attempted to divide by zero.') {
        super(message);
        this.name = 'DivideByZeroException';
    }
}

export class IndexOutOfRangeException extends Error {
    constructor(message = 'Index was outside the bounds of the array.') {
        super(message);
        this.name = 'IndexOutOfRangeException';
    }
}

export class IOException extends Error {
    constructor(message = 'I/O error occurred.') {
        super(message);
        this.name = 'IOException';
    }
}

export class EndOfStreamException extends IOException {
    constructor(message = 'Unable to read beyond the end of the stream.') {
        super(message);
        this.name = 'EndOfStreamException';
    }
}

/** System.Text.Json.JsonException. */
export class JsonException extends Error {
    constructor(message: string) {
        super(message);
        this.name = 'JsonException';
    }
}

/** System.Net.Http.HttpRequestException. */
export class HttpRequestException extends Error {
    readonly statusCode: number | null;

    constructor(message: string, statusCode: number | null = null) {
        super(message);
        this.name = 'HttpRequestException';
        this.statusCode = statusCode;
    }
}

/** System.OperationCanceledException. */
export class OperationCanceledException extends Error {
    constructor(message = 'The operation was canceled.') {
        super(message);
        this.name = 'OperationCanceledException';
    }
}

/** System.Threading.Tasks.TaskCanceledException: what HttpClient throws when its cancellation token fires. */
export class TaskCanceledException extends OperationCanceledException {
    constructor(message = 'A task was canceled.') {
        super(message);
        this.name = 'TaskCanceledException';
    }
}
