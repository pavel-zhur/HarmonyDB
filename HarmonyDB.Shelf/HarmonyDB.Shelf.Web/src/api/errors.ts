// The server sends codes, never text; here they become words (i18n errors.<code>).

import { i18n } from '../i18n';
import { ApiError } from './client';

export function errorText(error: unknown): string {
    if (error instanceof ApiError) {
        const key = `errors.${error.code}`;
        return i18n.exists(key) ? i18n.t(key as 'errors.unknown', { code: error.code }) : i18n.t('errors.unknown', { code: error.code });
    }
    return i18n.t('errors.unknown', { code: error instanceof Error ? error.message : String(error) });
}
