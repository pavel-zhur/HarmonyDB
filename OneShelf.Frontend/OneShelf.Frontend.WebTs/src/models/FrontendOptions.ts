// The original: Models/FrontendOptions.cs — IOptions<FrontendOptions>, bound from the "FrontendOptions" section.

import { configuration } from '../configuration';

export class FrontendOptions {
    /** `required string`, but Bind leaves it null when the setting is missing. */
    localSourceName: string | null = null;
    appInsightsConnectionString: string | null = null;

    get localSourceNameSafe(): string {
        const value = this.localSourceName
            ?.replaceAll('\'', '')
            .replaceAll('"', '')
            .trim();
        return value === undefined ? null! : value.length === 0 ? 'Local' : value;
    }

    /** `.Configure<FrontendOptions>(o => builder.Configuration.GetSection(nameof(FrontendOptions)).Bind(o))` */
    static bind(): FrontendOptions {
        const options = new FrontendOptions();
        const section = configuration.getSection('FrontendOptions');
        const localSourceName = section.get('LocalSourceName');
        if (localSourceName !== null) options.localSourceName = localSourceName;
        const appInsightsConnectionString = section.get('AppInsightsConnectionString');
        if (appInsightsConnectionString !== null) options.appInsightsConnectionString = appInsightsConnectionString;
        return options;
    }
}