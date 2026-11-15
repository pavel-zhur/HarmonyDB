export class ConcurrencyException extends Error {
    /** HttpStatusCode.TooManyRequests */
    static readonly StatusCode = 429;

    constructor() {
        super('Exception of type \'OneShelf.Common.Api.Common.ConcurrencyException\' was thrown.');
        this.name = 'ConcurrencyException';
    }
}
