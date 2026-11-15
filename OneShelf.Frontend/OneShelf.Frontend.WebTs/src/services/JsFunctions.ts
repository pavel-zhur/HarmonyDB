// The original: Services/JsFunctions.cs — each call imports ./services-js/JsFunctions.js (the browser imports it once).

import type { ILogger } from '../framework/logging';
import { importJsModule, invokeJs, type JsFunctionsJs } from '../interop/jsRuntime';

export class JsFunctions {
    private readonly _logger: ILogger;

    constructor(logger: ILogger) {
        this._logger = logger;
    }

    private async module(): Promise<JsFunctionsJs> {
        return await importJsModule<JsFunctionsJs>('./services-js/JsFunctions.js');
    }

    async prompt(query: string): Promise<string | null> {
        const module = await this.module();
        return await invokeJs<string | null>(module.Prompt, query);
    }

    async confirm(query: string): Promise<boolean> {
        const module = await this.module();
        return await invokeJs<boolean>(module.Confirm, query);
    }

    async scrollToTop(): Promise<void> {
        const module = await this.module();
        await invokeJs<void>(module.ScrollToTop);
    }
}