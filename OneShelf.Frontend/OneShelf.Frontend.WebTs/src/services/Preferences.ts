// The original: Services/Preferences.cs — localStorage "cof" ("yes" or absent), "zoom" (the number's text),
// "ppv1" (PrintPreferences, JSON).

import { PrintPreferences } from '../models/PrintPreferences';
import type { LocalStorageService } from '../packages/Blazored.LocalStorage/LocalStorageService';

/** int.TryParse(s, out var value) with the invariant culture. */
function tryParseInt32(s: string | null): number | null {
    if (s === null) return null;
    const match = /^[\t\n\v\f\r ]*([+-]?)([0-9]+)[\t\n\v\f\r ]*$/.exec(s);
    if (match === null) return null;
    const value = Number(match[1]! + match[2]!);
    return value >= -2147483648 && value <= 2147483647 ? (value === 0 ? 0 : value) : null;
}

export class Preferences {
    private readonly _localStorageService: LocalStorageService;

    private static readonly CofKey = 'cof';
    private static readonly ZoomKey = 'zoom';
    private static readonly PrintPreferencesKey = 'ppv1';

    private _circleOfFifthsVisible: boolean | null = null;
    private _zoom: number | null = null;
    private _printPreferences: PrintPreferences | null = null;

    constructor(localStorageService: LocalStorageService) {
        this._localStorageService = localStorageService;
    }

    async isCircleOfFifthsVisible(): Promise<boolean> {
        if (this._circleOfFifthsVisible !== null) return this._circleOfFifthsVisible;

        const value = await this._localStorageService.getItemAsStringAsync(Preferences.CofKey);
        this._circleOfFifthsVisible = value !== null && value.length > 0;

        return this._circleOfFifthsVisible;
    }

    async circleOfFifthsVisible(isVisible: boolean): Promise<void> {
        this._circleOfFifthsVisible = isVisible;
        if (isVisible) {
            await this._localStorageService.setItemAsStringAsync(Preferences.CofKey, 'yes');
        } else {
            await this._localStorageService.removeItemAsync(Preferences.CofKey);
        }
    }

    async getZoom(): Promise<number> {
        if (this._zoom !== null) return this._zoom;

        this._zoom = tryParseInt32(await this._localStorageService.getItemAsStringAsync(Preferences.ZoomKey)) ?? 100;

        return this._zoom;
    }

    async setZoom(zoom: number): Promise<void> {
        this._zoom = zoom;

        await this._localStorageService.setItemAsStringAsync(Preferences.ZoomKey, zoom.toString());
    }

    async getPrintPreferences(): Promise<PrintPreferences> {
        if (this._printPreferences !== null) return this._printPreferences;

        this._printPreferences = await this._localStorageService.getItemAsync(Preferences.PrintPreferencesKey, PrintPreferences.json);

        if (this._printPreferences === null) {
            this._printPreferences = new PrintPreferences();
            await this._localStorageService.setItemAsync(Preferences.PrintPreferencesKey, this._printPreferences, PrintPreferences.json);
        }

        return this._printPreferences;
    }

    async setPrintPreferences(printPreferences: PrintPreferences): Promise<void> {
        this._printPreferences = printPreferences;
        await this._localStorageService.setItemAsync(Preferences.PrintPreferencesKey, printPreferences, PrintPreferences.json);
    }
}