// ApiClientOptions<TClient>, bound by AddApiClient<TClient> from the section "{TClient name}Options".
// Of its members the site reaches only Endpoint: it never passes a conditional stream id and never calls the
// methods that use MasterCode/ServiceCode (PostWithCode, GetServiceIdentity), so those are not ported.
// The binder makes the Uri with UriTypeConverter: an empty value is null, any other is
// new Uri(value, UriKind.RelativeOrAbsolute), and a value it rejects fails the binding.

import { InvalidOperationException } from '../../common/dotnet/exceptions';
import { Uri, UriKind } from '../../common/dotnet/Uri';
import type { ConfigurationGetter } from './Configuration';

export class ApiClientOptions {
    readonly endpoint: Uri | null;

    constructor(endpoint: Uri | null) {
        this.endpoint = endpoint;
    }

    /** configuration.Bind($"{typeof(TClient).Name}Options", options) */
    static bind(configuration: ConfigurationGetter, clientName: string): ApiClientOptions {
        const path = `${clientName}Options:Endpoint`;
        const value = configuration(path) ?? null;
        if (value === null || value === '') return new ApiClientOptions(null);
        try {
            return new ApiClientOptions(new Uri(value, UriKind.RelativeOrAbsolute));
        } catch {
            throw new InvalidOperationException(`Failed to convert configuration value at '${path}' to type 'System.Uri'.`);
        }
    }

    /** GetEndpoint(conditionalStreamId: null) */
    getEndpoint(): Uri {
        if (this.endpoint === null) {
            throw new Error('The endpoint for conditional stream \'\' is not configured, the default fallback value is not available, either.');
        }

        return this.endpoint;
    }
}
