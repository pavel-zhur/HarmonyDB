// Puts the settings on the page: the theme, the interface's size (CSS zoom of the whole app, --ui-scale), the chords'
// size (--chords-scale, for the chord sheet), the language. Remembers them in this browser for the next start
// (public/boot.js reads them before the app).

import { useEffect } from 'react';
import { applyLocale, browserLocale, isLocale } from '../i18n';
import { useSettings } from '../data/settings';

export const scaleLimits = { interface: { min: 0.8, max: 1.7 }, chords: { min: 0.6, max: 2.6 } } as const;

export function clampScale(kind: keyof typeof scaleLimits, value: number): number {
    const { min, max } = scaleLimits[kind];
    return Math.round(Math.min(max, Math.max(min, value)) * 100) / 100;
}

export function setInterfaceScale(value: number): void {
    document.documentElement.style.setProperty('--ui-scale', String(value));
}

export function setChordsScale(value: number): void {
    document.documentElement.style.setProperty('--chords-scale', String(value));
}

export function useAppearance(): void {
    const settings = useSettings();
    const locale = isLocale(settings.locale) ? settings.locale : browserLocale();

    useEffect(() => {
        const root = document.documentElement;
        if (settings.theme === 'system') delete root.dataset.theme;
        else root.dataset.theme = settings.theme;
        setInterfaceScale(settings.interfaceScale);
        setChordsScale(settings.chordsScale);
        applyLocale(locale);
        try {
            localStorage.setItem('shelf.look', JSON.stringify({ theme: settings.theme, interfaceScale: settings.interfaceScale, locale }));
        } catch {
            // no storage: the defaults at the next start
        }
    }, [settings.theme, settings.interfaceScale, settings.chordsScale, locale]);
}

/** Whether the page is dark now (the setting, or the device's when it is "system"). */
export function isDark(): boolean {
    const theme = document.documentElement.dataset.theme;
    return theme ? theme === 'dark' : matchMedia('(prefers-color-scheme: dark)').matches;
}
