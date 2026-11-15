import { describe, expect, it } from 'vitest';
import { createConfiguration, parseJsonConfiguration } from './configuration';

describe('configuration', () => {
    const configuration = createConfiguration([
        parseJsonConfiguration(`{
  "TelegramBotId": "",
  "HideUnstable": false,
  "CollectivesApiClientOptions:Endpoint": "",
  "FrontendApiClientOptions:Endpoint": "",
  "FrontendOptions:LocalSourceName": "Local",
  "FrontendOptions:AppInsightsConnectionString": ""
}`),
        parseJsonConfiguration(`{
  // comments and trailing commas are allowed
  "FrontendApiClientOptions": { "Endpoint": "http://localhost:7071/api/", "ConditionalStreams": { "b": { "Endpoint": "x" }, "A": {} } },
  "BadHash": null,
  "List": [1, 2.50, true, {}],
}`),
    ]);

    it('reads values by ":" paths, ignoring case, the later file over the earlier', () => {
        expect(configuration.get('TelegramBotId')).toBe('');
        expect(configuration.get('frontendoptions:localsourcename')).toBe('Local');
        expect(configuration.getSection('FrontendOptions').get('LocalSourceName')).toBe('Local');
        expect(configuration.get('FrontendApiClientOptions:Endpoint')).toBe('http://localhost:7071/api/');
        expect(configuration.get('Missing')).toBeNull();
    });

    it('gives the values as the JSON provider does', () => {
        expect(configuration.get('HideUnstable')).toBe('False');
        expect(configuration.get('BadHash')).toBe('');
        expect(configuration.get('List:1')).toBe('2.50');
        expect(configuration.get('List:2')).toBe('True');
        expect(configuration.get('List:3')).toBeNull();
        expect(configuration.get('FrontendApiClientOptions:ConditionalStreams:A')).toBeNull();
    });

    it('converts booleans as GetValue<bool> does', () => {
        expect(configuration.getBoolean('HideUnstable')).toBe(false);
        expect(configuration.getBoolean('Missing')).toBe(false);
        expect(() => configuration.getBoolean('FrontendOptions:LocalSourceName')).toThrow();
    });

    it('lists children in the configuration key order', () => {
        expect(configuration.getSection('FrontendApiClientOptions:ConditionalStreams').getChildren().map(x => x.key)).toEqual(['A', 'b']);
        expect(configuration.getSection('List').getChildren().map(x => x.path)).toEqual(['List:0', 'List:1', 'List:2', 'List:3']);
    });

    it('rejects a key given twice', () => {
        expect(() => parseJsonConfiguration('{ "a": 1, "A": 2 }')).toThrow(/duplicate key/);
        expect(() => parseJsonConfiguration('{ "a:b": 1, "a": { "b": 2 } }')).toThrow(/duplicate key/);
    });
});
