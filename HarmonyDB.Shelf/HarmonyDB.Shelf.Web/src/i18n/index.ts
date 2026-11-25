// The interface's languages. Every text a person sees is a key here: t('library.whole'), t('playlists.songs',
// { count }). The language: the person's setting, else (and for a guest) the browser's, else English.

import i18next from 'i18next';
import { initReactI18next } from 'react-i18next';
import { en } from './en';
import { ru } from './ru';

export const locales = ['en', 'ru'] as const;
export type Locale = (typeof locales)[number];

declare module 'i18next' {
    interface CustomTypeOptions {
        defaultNS: 'translation';
        resources: { translation: typeof en };
    }
}

/** The browser's first language the app has. */
export function browserLocale(): Locale {
    for (const language of navigator.languages ?? [navigator.language])
        for (const locale of locales)
            if (language.toLowerCase().split('-')[0] === locale) return locale;
    return 'en';
}

export function isLocale(value: string | null | undefined): value is Locale {
    return locales.includes(value as Locale);
}

export const i18n = i18next.createInstance();

void i18n.use(initReactI18next).init({
    resources: { en: { translation: en }, ru: { translation: ru } },
    lng: browserLocale(),
    fallbackLng: false,
    interpolation: { escapeValue: false },
    returnNull: false,
});

/** Switches the language: texts, the page's lang and title, the install manifest. */
export function applyLocale(locale: Locale): void {
    if (i18n.language !== locale) void i18n.changeLanguage(locale);
    document.documentElement.lang = locale;
    document.title = i18n.t('app.name');
    const manifest = document.getElementById('manifest');
    if (manifest) manifest.setAttribute('href', `/manifest.${locale}.webmanifest`);
}

/** "5 minutes ago", "yesterday", "3 days ago" — how long ago, in the language. */
export function formatAgo(iso: string, now = Date.now()): string {
    const seconds = Math.round((new Date(iso).getTime() - now) / 1000);
    const format = new Intl.RelativeTimeFormat(i18n.language, { numeric: 'auto' });
    const abs = Math.abs(seconds);
    if (abs < 60) return i18n.t('time.justNow');
    if (abs < 3600) return format.format(Math.round(seconds / 60), 'minute');
    if (abs < 86400) return format.format(Math.round(seconds / 3600), 'hour');
    if (abs < 86400 * 30) return format.format(Math.round(seconds / 86400), 'day');
    if (abs < 86400 * 365) return format.format(Math.round(seconds / (86400 * 30)), 'month');
    return format.format(Math.round(seconds / (86400 * 365)), 'year');
}

export function formatDate(iso: string): string {
    return new Intl.DateTimeFormat(i18n.language, { day: 'numeric', month: 'long', year: 'numeric' }).format(new Date(iso));
}
