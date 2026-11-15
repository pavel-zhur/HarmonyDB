/**
 * The site's configuration (IConfiguration) as the clients read it: the value at a flat key such as
 * "FrontendApiClientOptions:Endpoint", or null/undefined when there is none.
 */
export type ConfigurationGetter = (key: string) => string | null | undefined;
