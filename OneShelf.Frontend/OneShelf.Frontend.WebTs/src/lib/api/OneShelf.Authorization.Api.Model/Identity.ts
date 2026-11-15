import { jsonClass, jsonInt64, jsonProperty, jsonString, type JsonContract } from '../../common/dotnet/json/JsonSerializer';

export interface IdentityInit {
    id?: number;
    firstName?: string | null;
    lastName?: string | null;
    username?: string | null;
    photoUrl?: string | null;
    authDate?: number;
    hash: string;
}

/** A record (equality by all its members). The JSON names are fixed by [JsonPropertyName] (the Telegram login data). */
export class Identity {
    readonly id: number;
    readonly firstName: string | null;
    readonly lastName: string | null;
    readonly username: string | null;
    readonly photoUrl: string | null;
    readonly authDate: number;
    readonly hash: string;

    constructor(init: IdentityInit) {
        this.id = init.id ?? 0;
        this.firstName = init.firstName ?? null;
        this.lastName = init.lastName ?? null;
        this.username = init.username ?? null;
        this.photoUrl = init.photoUrl ?? null;
        this.authDate = init.authDate ?? 0;
        this.hash = init.hash;
    }

    /** [JsonIgnore] */
    get title(): string {
        return this.firstName ?? this.username ?? this.id.toString();
    }

    equals(other: Identity | null | undefined): boolean {
        return other != null
            && this.id === other.id
            && this.firstName === other.firstName
            && this.lastName === other.lastName
            && this.username === other.username
            && this.photoUrl === other.photoUrl
            && this.authDate === other.authDate
            && this.hash === other.hash;
    }

    static readonly json: JsonContract<Identity> = jsonClass<Identity>('OneShelf.Authorization.Api.Model.Identity', init => new Identity(init as unknown as IdentityInit), () => [
        jsonProperty('Id', jsonInt64, { jsonName: 'id' }),
        jsonProperty('FirstName', jsonString, { jsonName: 'first_name' }),
        jsonProperty('LastName', jsonString, { jsonName: 'last_name' }),
        jsonProperty('Username', jsonString, { jsonName: 'username' }),
        jsonProperty('PhotoUrl', jsonString, { jsonName: 'photo_url' }),
        jsonProperty('AuthDate', jsonInt64, { jsonName: 'auth_date' }),
        jsonProperty('Hash', jsonString, { jsonName: 'hash', required: true }),
    ]);
}
