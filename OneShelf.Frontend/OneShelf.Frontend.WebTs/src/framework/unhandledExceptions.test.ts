import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { getLogger } from './logging';
import { OperationCanceledException, TaskCanceledException } from '../lib/common/dotnet/exceptions';
import { asyncVoid, errorHandledTask, handleRendererException } from './unhandledExceptions';

// What the original writes was taken from a Blazor WebAssembly 9.0 test application in a browser.

let errorBar: { style: { display: string } };

beforeEach(() => {
    errorBar = { style: { display: 'none' } };
    vi.stubGlobal('document', {
        querySelector: (selector: string) => (selector === '#blazor-error-ui' ? errorBar : null),
        querySelectorAll: () => [],
    });
});

afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
});

describe('unhandled exceptions', () => {
    it('in a component: logged by the renderer as Critical, which shows the error bar', () => {
        const error = vi.spyOn(console, 'error').mockImplementation(() => {});
        const e = new Error('sync handler');
        e.stack = 'Error: sync handler\n    at handler';

        handleRendererException(e);
        handleRendererException(e);

        expect(error).toHaveBeenCalledTimes(1);
        expect(error).toHaveBeenCalledWith('crit: Microsoft.AspNetCore.Components.WebAssembly.Rendering.WebAssemblyRenderer[100]\n'
            + '      Unhandled exception rendering component: sync handler\n'
            + 'Error: sync handler\n    at handler');
        expect(errorBar.style.display).toBe('block');
    });

    it('any Critical message shows the error bar; an Error one does not', () => {
        const error = vi.spyOn(console, 'error').mockImplementation(() => {});
        getLogger('X').logError(new Error('e'), 'an error');
        expect(errorBar.style.display).toBe('none');
        getLogger('X').logCritical(null, 'a critical message');
        expect(error).toHaveBeenLastCalledWith('crit: X[0]\n      a critical message');
        expect(errorBar.style.display).toBe('block');
    });

    it('a component\'s failed task is logged by the renderer; a cancelled one is not', async () => {
        const error = vi.spyOn(console, 'error').mockImplementation(() => {});
        const e = new Error('async handler');
        e.stack = 'Error: async handler\n    at handler';

        await errorHandledTask(Promise.reject(new OperationCanceledException()));
        await errorHandledTask(Promise.reject(new TaskCanceledException()));
        expect(error).not.toHaveBeenCalled();
        expect(errorBar.style.display).toBe('none');

        await errorHandledTask(Promise.reject(e));
        expect(error).toHaveBeenCalledWith('crit: Microsoft.AspNetCore.Components.WebAssembly.Rendering.WebAssemblyRenderer[100]\n'
            + '      Unhandled exception rendering component: async handler\n'
            + 'Error: async handler\n    at handler');
        expect(errorBar.style.display).toBe('block');
    });

    it('in async void: written by the runtime as unhandled, with the error bar', async () => {
        const lines: string[] = [];
        vi.spyOn(console, 'warn').mockImplementation(x => lines.push(`warn ${x}`));
        vi.spyOn(console, 'error').mockImplementation(x => lines.push(`error ${x}`));

        asyncVoid(async () => {
            const e = new Error('thrown before any await');
            e.stack = 'Error: thrown before any await\n    at body';
            throw e;
        });
        expect(lines).toEqual([]);
        await new Promise(resolve => setTimeout(resolve, 0));

        expect(lines).toEqual([
            'warn [MONO] Process terminated.',
            'warn [MONO] ThreadPool.BackgroundJobHandler failed',
            'error (null)',
            'error Unhandled Exception:',
            'error Error: thrown before any await',
            'error     at body',
        ]);
        expect(errorBar.style.display).toBe('block');
    });
});
