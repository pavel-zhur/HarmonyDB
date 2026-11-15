// What the application needs from the build for its settings (src/configuration.ts):
// - the list of the settings files the site has (public/appsettings*.json), as the original's boot manifest lists them;
// - on the development server, the "Blazor-Environment" header with the environment name, as the .NET development
//   server sends it: ASPNETCORE_ENVIRONMENT, "Development" when it is not set (the original's launchSettings.json).
//   The preview server stands for a published site: it sends the header only when ASPNETCORE_ENVIRONMENT is set.

import fs from 'node:fs';
import path from 'node:path';
import type { Plugin } from 'vite';

export function appSettings(): Plugin {
    const header = (environment: string | undefined) =>
        (_request: unknown, response: { setHeader(name: string, value: string): void }, next: () => void) => {
            if (environment !== undefined) response.setHeader('Blazor-Environment', environment);
            next();
        };

    return {
        name: 'oneshelf-app-settings',

        config(config) {
            const publicDirectory = path.resolve(config.root ?? process.cwd(), typeof config.publicDir === 'string' ? config.publicDir : 'public');
            const files = fs.readdirSync(publicDirectory).filter(name => /^appsettings.*\.json$/i.test(name)).sort();
            return { define: { __APPSETTINGS_FILES__: JSON.stringify(files) } };
        },

        configureServer(server) {
            server.middlewares.use(header(process.env.ASPNETCORE_ENVIRONMENT ?? 'Development'));
        },

        configurePreviewServer(server) {
            server.middlewares.use(header(process.env.ASPNETCORE_ENVIRONMENT));
        },
    };
}
