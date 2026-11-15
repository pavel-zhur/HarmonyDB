// The original: the services Program.cs registers, and what the packages it calls register.
//
// As with the DI container, a service is created the first time it is asked for (a component's @inject, another
// service's constructor), its dependencies first, in the order of its constructor's parameters; a constructor that
// starts work (CollectionIndexProvider syncs 1.5 s later, IllustrationsProvider loads 10 s later) starts it then.
// So a component reads the services it injects at its first render, the way Blazor injects them on creation:
//
//     const identityProvider = services.identityProvider;     // @inject IdentityProvider IdentityProvider
//
// Lifetimes, as in Program.cs: singletons live as long as the application. The scoped ones (CollectionNavigation,
// JsFunctions, SearchContext, ProgressionsSearchContext, ReceiverInstance, CollectivesApiClient, FrontendApiClient)
// are, in Blazor WebAssembly, one per the application's scope — that is, also one for the application — except where
// the original makes a scope of its own: Api creates one for its FrontendApiClient (a second client, its own).
// MyIndexedDb is transient: each service that asks gets a new one (only DataProvider asks).
//
// Not here, as the site never resolves them: HttpClient (each API client has its own, see HttpClientFactory),
// ISyncLocalStorageService, HarmonyDB.Index.Analysis.Em's MusicAnalyzer.
// The Application Insights configuration (AddBlazorApplicationInsights(config => ...)) is set in main.tsx.

import { configuration } from '../configuration';
import { getLogger } from '../framework/logging';
import { Receiver } from '../interop/Receiver';
import type { ReceiverInstance } from '../interop/ReceiverInstance';
import { ChordDataParser } from '../lib/analysis/Services/ChordDataParser';
import { IndexExtractor } from '../lib/analysis/Services/IndexExtractor';
import { InputParser } from '../lib/analysis/Services/InputParser';
import { PathsSearcher } from '../lib/analysis/Services/PathsSearcher';
import { ProgressionsBuilder } from '../lib/analysis/Services/ProgressionsBuilder';
import { ProgressionsSearch } from '../lib/analysis/Services/ProgressionsSearch';
import { ProgressionsVisualizer } from '../lib/analysis/Services/ProgressionsVisualizer';
import { CollectivesApiClient } from '../lib/api/OneShelf.Collectives.Api.Client/CollectivesApiClient';
import { FrontendApiClient } from '../lib/api/OneShelf.Frontend.Api.Client/FrontendApiClient';
import { FrontendOptions } from '../models/FrontendOptions';
import { applicationInsights, type ApplicationInsights } from '../packages/BlazorApplicationInsights/ApplicationInsights';
import { LocalStorageService } from '../packages/Blazored.LocalStorage/LocalStorageService';
import { createHttpClient } from '../packages/Microsoft.Extensions.Http/HttpClientFactory';
import { Api } from './Api';
import { ChordsCacheLoader } from './ChordsCacheLoader';
import { CollectionIndexProvider } from './CollectionIndexProvider';
import { CollectionNavigation } from './CollectionNavigation';
import { DataProvider } from './DataProvider';
import { FingeringsProvider } from './FingeringsProvider';
import { IdentityProvider } from './IdentityProvider';
import { IllustrationsProvider } from './IllustrationsProvider';
import { InstantActions } from './InstantActions';
import { JsFunctions } from './JsFunctions';
import { LogoImageSequencer } from './LogoImageSequencer';
import { MyIndexedDb } from './MyIndexedDb';
import { Player } from './Player';
import { Preferences } from './Preferences';
import { ProgressionsCacheLoader } from './ProgressionsCacheLoader';
import { ProgressionsSearchContext } from './ProgressionsSearchContext';
import { SearchContext } from './SearchContext';

const web = 'OneShelf.Frontend.Web.Services.';
const analysis = 'HarmonyDB.Index.Analysis.Services.';

const instances = new Map<string, unknown>();

/** One instance, created the first time it is asked for. */
function single<T>(name: string, create: () => T): T {
    if (!instances.has(name)) {
        instances.set(name, create());
    }

    return instances.get(name) as T;
}

/** The API clients' configuration (IConfiguration, read when a client is created). */
const configurationGetter = (key: string) => configuration.get(key);

export const services = {
    // AddBlazoredLocalStorageAsSingleton
    get localStorageService(): LocalStorageService {
        return single('ILocalStorageService', () => new LocalStorageService());
    },

    get receiver(): Receiver {
        return single('Receiver', () => new Receiver(services.identityProvider));
    },

    /** Scoped: `services.GetRequiredService<Receiver>().CreateInstance()` */
    get receiverInstance(): ReceiverInstance {
        return single('ReceiverInstance', () => services.receiver.createInstance());
    },

    get identityProvider(): IdentityProvider {
        return single('IdentityProvider', () => new IdentityProvider(getLogger(web + 'IdentityProvider'), services.localStorageService, services.applicationInsights));
    },

    get api(): Api {
        return single('Api', () => {
            const identityProvider = services.identityProvider;
            // _scope = serviceScopeFactory.CreateScope(); _scope.ServiceProvider.GetRequiredService<FrontendApiClient>()
            const frontendApiClient = FrontendApiClient.create(configurationGetter, createHttpClient());
            return new Api(identityProvider, frontendApiClient);
        });
    },

    get dataProvider(): DataProvider {
        return single('DataProvider', () => {
            const api = services.api;
            const logger = getLogger(web + 'DataProvider');
            return new DataProvider(api, logger, services.myIndexedDb);
        });
    },

    get progressionsCacheLoader(): ProgressionsCacheLoader {
        return single('ProgressionsCacheLoader', () => {
            const logger = getLogger(web + 'ProgressionsCacheLoader');
            const dataProvider = services.dataProvider;
            const collectionIndexProvider = services.collectionIndexProvider;
            const progressionsBuilder = services.progressionsBuilder;
            return new ProgressionsCacheLoader(logger, dataProvider, collectionIndexProvider, progressionsBuilder, services.chordDataParser);
        });
    },

    get instantActions(): InstantActions {
        return single('InstantActions', () => new InstantActions(services.localStorageService, getLogger(web + 'InstantActions')));
    },

    get collectionIndexProvider(): CollectionIndexProvider {
        return single('CollectionIndexProvider', () => {
            const logger = getLogger(web + 'CollectionIndexProvider');
            const identityProvider = services.identityProvider;
            const localStorageService = services.localStorageService;
            const api = services.api;
            return new CollectionIndexProvider(logger, identityProvider, localStorageService, api, services.instantActions);
        });
    },

    get illustrationsProvider(): IllustrationsProvider {
        return single('IllustrationsProvider', () => {
            const logger = getLogger(web + 'IllustrationsProvider');
            const localStorageService = services.localStorageService;
            return new IllustrationsProvider(logger, localStorageService, services.api);
        });
    },

    /** Scoped. */
    get collectionNavigation(): CollectionNavigation {
        return single('CollectionNavigation', () => new CollectionNavigation(getLogger(web + 'CollectionNavigation'), services.identityProvider));
    },

    /** Scoped. */
    get jsFunctions(): JsFunctions {
        return single('JsFunctions', () => new JsFunctions(getLogger(web + 'JsFunctions')));
    },

    /** Scoped. */
    get searchContext(): SearchContext {
        return single('SearchContext', () => {
            const logger = getLogger(web + 'SearchContext');
            const dataProvider = services.dataProvider;
            return new SearchContext(logger, dataProvider, services.instantActions);
        });
    },

    /** Scoped. */
    get progressionsSearchContext(): ProgressionsSearchContext {
        return single('ProgressionsSearchContext', () => {
            const logger = getLogger(web + 'ProgressionsSearchContext');
            const progressionsCacheLoader = services.progressionsCacheLoader;
            return new ProgressionsSearchContext(logger, progressionsCacheLoader, services.progressionsSearch);
        });
    },

    // AddIndexAnalysis

    get chordDataParser(): ChordDataParser {
        return single('ChordDataParser', () => new ChordDataParser(getLogger(analysis + 'ChordDataParser')));
    },

    get progressionsBuilder(): ProgressionsBuilder {
        return single('ProgressionsBuilder', () => new ProgressionsBuilder(getLogger(analysis + 'ProgressionsBuilder')));
    },

    get progressionsVisualizer(): ProgressionsVisualizer {
        return single('ProgressionsVisualizer', () => new ProgressionsVisualizer());
    },

    get indexExtractor(): IndexExtractor {
        return single('IndexExtractor', () => new IndexExtractor(services.pathsSearcher));
    },

    get inputParser(): InputParser {
        return single('InputParser', () => {
            const progressionsBuilder = services.progressionsBuilder;
            return new InputParser(progressionsBuilder, services.chordDataParser);
        });
    },

    get progressionsSearch(): ProgressionsSearch {
        return single('ProgressionsSearch', () => new ProgressionsSearch(getLogger(analysis + 'ProgressionsSearch')));
    },

    get pathsSearcher(): PathsSearcher {
        return single('PathsSearcher', () => new PathsSearcher());
    },

    get chordsCacheLoader(): ChordsCacheLoader {
        return single('ChordsCacheLoader', () => {
            const logger = getLogger(web + 'ChordsCacheLoader');
            const collectionIndexProvider = services.collectionIndexProvider;
            const dataProvider = services.dataProvider;
            return new ChordsCacheLoader(logger, collectionIndexProvider, dataProvider, services.api);
        });
    },

    get player(): Player {
        return single('Player', () => new Player(getLogger(web + 'Player'), services.chordDataParser));
    },

    get preferences(): Preferences {
        return single('Preferences', () => new Preferences(services.localStorageService));
    },

    get logoImageSequencer(): LogoImageSequencer {
        return single('LogoImageSequencer', () => new LogoImageSequencer());
    },

    get fingeringsProvider(): FingeringsProvider {
        return single('FingeringsProvider', () => new FingeringsProvider());
    },

    /** Scoped (AddCollectivesApiClient). */
    get collectivesApiClient(): CollectivesApiClient {
        return single('CollectivesApiClient', () => CollectivesApiClient.create(configurationGetter, createHttpClient()));
    },

    /** Scoped (AddFrontendApiClient) — the application scope's own; Api has another. */
    get frontendApiClient(): FrontendApiClient {
        return single('FrontendApiClient', () => FrontendApiClient.create(configurationGetter, createHttpClient()));
    },

    /** IOptions<FrontendOptions> */
    get frontendOptions(): FrontendOptions {
        return single('IOptions<FrontendOptions>', () => FrontendOptions.bind());
    },

    /** AddBlazorApplicationInsights: IApplicationInsights */
    get applicationInsights(): ApplicationInsights {
        return applicationInsights;
    },

    /** Transient: a new one each time. */
    get myIndexedDb(): MyIndexedDb {
        return new MyIndexedDb(getLogger(web + 'MyIndexedDb'));
    },
};
