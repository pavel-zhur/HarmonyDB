It's an app to export photos and videos from the Telegram-exported chats to Google Photos.

It is not production ready, it's just once-use. Probably I'll make a library later.

# Notes for myself

## Step 1

- Uncomment the block `////re-login and display auth token`
- Copy the token and paste it to appsettings.Secrets.json
```
"Scopes": [
  "ReadOnly",
  "AppendOnly",
  "AppCreatedData"
  //"Access",
  //"Sharing",
],
```
- Prerequisites:
  - the root path in the appsettings needs to point to a folder with:
    - all.json (not actualy currently used)
    - may have an _app* directories (will be excluded from all.json on generation)
    - result (i).json files where i corresponds to the folders
    - each folder is an exported chat
    - the db file will be created automatically at the path specified in appsettings
  - an empty Google Photos account (for the future analysis of how well the videos have been uploaded. currently not sure.)
```
"ConnectionStrings": {
  "VideosDatabase": "Data Source=c:/temp/oneshelf.videos.app.db",
}
```

## File share mounting to the linux function app

```az webapp config storage-account add --access-key (storage account access key) -t AzureFiles --account-name (storage account name) --custom-id (storage account name) --name (function app name) --resource-group (function app resource group) --sn (file share name) --mount-path /sharepath1```