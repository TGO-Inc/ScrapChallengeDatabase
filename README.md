![WorkshopScraper](/ChallengeModeDatabase/Mod/preview.png)

# scrap-mechanic-mod-scraper
Scrapes the Steam Workshop for Scrap Mechanic mods and stores useful information.

## Usage
do sm.json.open(todo) and then use the list of uuids to check for challenges: local success, data = pcall(sm.json.open, uuid)

### Dependency
To use the mod database in your own mod you need to add it to your dependencies. This can be done in the `description.json` of your mod.

```json
{
   "description" : "A mod for testing the Challenge Pack Database mod.",
   "localId" : "521cbf4e-8901-4741-b6a6-4aee2386339f",
   "name" : "Test",
   "type" : "Blocks and Parts",
   "version" : 0,
   "dependencies" : [
      {
         "fileId": todo,
         "localId": todo
      }
   ]
}
```
