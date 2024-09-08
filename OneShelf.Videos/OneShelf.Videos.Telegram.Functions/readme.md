# A note for myself

- Issue: there's a 20MB bot download limit.
  - Consequently, a bot is not needed.
- Required cloud connection string: `VideosDatabase`
- Required cloud app settings: 
```
{
    "name": "CasCap__GooglePhotosOptions__ClientId",
    "value": //
    "slotSetting": false
  },
  {
    "name": "CasCap__GooglePhotosOptions__ClientSecret",
    "value": //
    "slotSetting": false
  },
  {
    "name": "CasCap__GooglePhotosOptions__FileDataStoreFullPathOverride",
    "value": "/sharepath1/_auth",
    "slotSetting": false
  },
  {
    "name": "CasCap__GooglePhotosOptions__Scopes__0",
    "value": "ReadOnly",
    "slotSetting": false
  },
  {
    "name": "CasCap__GooglePhotosOptions__Scopes__1",
    "value": "AppendOnly",
    "slotSetting": false
  },
  {
    "name": "CasCap__GooglePhotosOptions__Scopes__2",
    "value": "AppCreatedData",
    "slotSetting": false
  },
  {
    "name": "CasCap__GooglePhotosOptions__User",
    "value": //
    "slotSetting": false
  },
  {
    "name": "TelegramOptions__AdminId",
    "value": //
    "slotSetting": false
  },
  {
    "name": "TelegramOptions__Token",
    "value": //
    "slotSetting": false
  },
  {
    "name": "TelegramOptions__WebHooksSecretToken",
    "value": //
    "slotSetting": false
  },
  {
    "name": "VideosOptions__BasePath",
    "value": "/sharepath1",
    "slotSetting": false
  }
```
- Cloud drive mapping: `az webapp config storage-account add --access-key (storage account access key) -t AzureFiles --account-name (storage account name) --custom-id (storage account name) --name (function app name) --resource-group (function app resource group) --sn (file share name) --mount-path /sharepath1`