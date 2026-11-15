// The global styles: public/css/app.scss (the original's wwwroot/css/app.scss) compiled into css/app.css as the
// original's build compiles it (build/compileStyle.ts), written, as there, in UTF-8 with the byte order mark.

import path from 'node:path';
import type { Plugin } from 'vite';
import { compileStyle } from './compileStyle';

const outputName = 'css/app.css';

export async function buildGlobalStyles(publicDirectory: string): Promise<string> {
    return '﻿' + await compileStyle(path.join(publicDirectory, 'css', 'app.scss'), path.join(publicDirectory, outputName));
}

export function globalStyles(): Plugin {
    let publicDirectory = '';

    return {
        name: 'oneshelf-global-styles',

        configResolved(config) {
            publicDirectory = config.publicDir;
        },

        configureServer(server) {
            server.middlewares.use((request, response, next) => {
                if (request.url?.split('?')[0] !== '/' + outputName) {
                    next();
                    return;
                }

                buildGlobalStyles(publicDirectory).then(css => {
                    response.setHeader('Content-Type', 'text/css; charset=utf-8');
                    response.end(css);
                }, next);
            });
        },

        async generateBundle() {
            this.emitFile({ type: 'asset', fileName: outputName, source: await buildGlobalStyles(publicDirectory) });
        },
    };
}
