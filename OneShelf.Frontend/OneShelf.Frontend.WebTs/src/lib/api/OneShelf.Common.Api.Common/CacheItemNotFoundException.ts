export class CacheItemNotFoundException extends Error {
    static readonly StatusCode = 460;

    constructor() {
        super('Exception of type \'OneShelf.Common.Api.Common.CacheItemNotFoundException\' was thrown.');
        this.name = 'CacheItemNotFoundException';
    }
}
