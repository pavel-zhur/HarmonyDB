// BlazorApplicationInsights 3.1.0: the ApplicationInsightsInit component (App.razor: <ApplicationInsightsInit IsWasmStandalone="true" />).
// Standalone, it renders nothing (the SDK snippet is in index.html); after its first render it sets the JS runtime,
// imports the package's script module, replaces the SDK's configuration with the site's (updateCfg, not merged) and
// tracks the first page view. Later page views are the SDK's own (enableAutoRouteTracking).
// A failure there is an unhandled error of the component (the error bar), as in the original.

import { useEffect } from 'react';
import { importJsModule } from '../../interop/jsRuntime';
import { applicationInsights, applicationInsightsInitConfig } from './ApplicationInsights';
import { errorHandledTask } from '../../framework/unhandledExceptions';

export interface ApplicationInsightsInitProps {
    isWasmStandalone: boolean;
}

export function ApplicationInsightsInit({ isWasmStandalone }: ApplicationInsightsInitProps) {
    useEffect(() => {
        void errorHandledTask((async () => {
            applicationInsights.initJSRuntime();

            if (isWasmStandalone) {
                await importJsModule('./_content/BlazorApplicationInsights/BlazorApplicationInsights.lib.module.js');
                if (applicationInsightsInitConfig.config !== null) {
                    await applicationInsights.updateCfg(applicationInsightsInitConfig.config, false);
                    await applicationInsights.trackPageView();
                }
            }
        })());
        // OnAfterRenderAsync(firstRender): once.
    }, []);

    return null;
}
