// The original: Program.cs (and what Blazor WebAssembly does when it starts).
//
// The settings are loaded first. Then what Program.cs does before running:
// - AddBlazorApplicationInsights(config => config.ConnectionString = FrontendOptions:AppInsightsConnectionString),
//   read at registration (a missing FrontendOptions section fails the start, as Get<FrontendOptions>()! does there);
// - the services are registered (src/services/registry.ts creates each when it is first asked for);
// - Blazor loads the packages' JavaScript initializers before the application starts: BlazorApplicationInsights'
//   lib.module.js;
// - `await host.Services.GetRequiredService<IdentityProvider>().Initialize();`
// Then the application is rendered into #app, replacing index.html's loading screen, with HeadOutlet (the original's
// root component at "head::after") beside it.

import { App } from './App';
import { configuration, loadConfiguration } from './configuration';
import { showErrorNotification } from './framework/errorUi';
import { createRootWithBlazorEventOrder } from './framework/eventDelegator';
import { HeadOutlet } from './framework/PageTitle';
import { handleUnhandledExceptions } from './framework/unhandledExceptions';
import { importJsModule } from './interop/jsRuntime';
import { applicationInsightsInitConfig } from './packages/BlazorApplicationInsights/ApplicationInsights';
import { NullReferenceException } from './packages/BlazorApplicationInsights/ApplicationInsights';
import { services } from './services/registry';

async function main() {
    handleUnhandledExceptions();

    await loadConfiguration();

    // builder.Configuration.GetSection(nameof(FrontendOptions)).Get<FrontendOptions>()!.AppInsightsConnectionString
    const frontendOptionsSection = configuration.getSection('FrontendOptions');
    if (frontendOptionsSection.value === null && frontendOptionsSection.getChildren().length === 0) {
        throw new NullReferenceException();
    }

    applicationInsightsInitConfig.config = {
        connectionString: frontendOptionsSection.get('AppInsightsConnectionString'),
        enableAutoRouteTracking: true,
    };

    await importJsModule('./_content/BlazorApplicationInsights/BlazorApplicationInsights.lib.module.js');

    await services.identityProvider.initialize();

    createRootWithBlazorEventOrder(document.getElementById('app')!).render(
        <>
            <App />
            <HeadOutlet />
        </>,
    );
}

// A failed start is not a component's exception: Blazor writes it to the console and shows the error bar.
main().catch(e => {
    console.error(e);
    showErrorNotification();
});
