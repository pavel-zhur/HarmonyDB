// The tests run under node's own runner with its TypeScript stripping; the app's imports name no extension (the
// bundler's way), so a relative import is looked up as .ts / .tsx / index.ts here.
import { register } from 'node:module';

register('data:text/javascript,' + encodeURIComponent(`
export async function resolve(specifier, context, next) {
    try {
        return await next(specifier, context);
    } catch (error) {
        if (!['ERR_MODULE_NOT_FOUND', 'ERR_UNSUPPORTED_DIR_IMPORT'].includes(error.code) || !specifier.startsWith('.')) throw error;
        for (const tail of ['.ts', '/index.ts'])
            try { return await next(specifier + tail, context); } catch { /* the next */ }
        throw error;
    }
}`));
