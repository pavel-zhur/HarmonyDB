import { afterEach, describe, expect, it, vi } from 'vitest';
import { formatLogValues, getLogger } from './logging';

describe('logging', () => {
    afterEach(() => {
        vi.restoreAllMocks();
    });

    it('writes to the console as Blazor WebAssembly does', () => {
        const info = vi.spyOn(console, 'info').mockImplementation(() => {});
        const error = vi.spyOn(console, 'error').mockImplementation(() => {});
        const debug = vi.spyOn(console, 'debug').mockImplementation(() => {});
        const logger = getLogger('OneShelf.Frontend.Web.Services.SearchContext');

        logger.logInformation('last requested: \'abc\'\nsecond line');
        logger.logDebug('below the minimum level');
        const e = new Error('boom');
        e.stack = 'Error: boom\n    at somewhere';
        logger.logError(e, 'Error getting the chords progression for {externalId}.', 'ex-1');

        expect(info).toHaveBeenCalledWith('info: OneShelf.Frontend.Web.Services.SearchContext[0]\n      last requested: \'abc\'\n      second line');
        expect(error).toHaveBeenCalledWith('fail: OneShelf.Frontend.Web.Services.SearchContext[0]\n      Error getting the chords progression for ex-1.\nError: boom\n    at somewhere');
        expect(debug).not.toHaveBeenCalled();
    });

    it('formats message templates as LogValuesFormatter', () => {
        expect(formatLogValues('a {x} b {y:0.0} {{c}}', [null, [1, null]])).toEqual({
            formatted: 'a (null) b 1, (null) {c}',
            state: [['x', null], ['y', [1, null]], ['{OriginalFormat}', 'a {x} b {y:0.0} {{c}}']],
        });
        expect(formatLogValues('no {holes} without arguments', []).formatted).toBe('no {holes} without arguments');
    });
});
