export class UnauthorizedException extends Error {
    static readonly StatusCode = 401;

    constructor(errorMessage: string) {
        super(errorMessage);
        this.name = 'UnauthorizedException';
    }
}
