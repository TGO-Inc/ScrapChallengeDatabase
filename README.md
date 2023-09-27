![WorkshopScraper](/ChallengeModeDatabase/Mod/preview.jpg)

# Scrap-Mechanic-Mod-Scraper
Scrapes the Steam Workshop for Scrap Mechanic items and stores useful information.

## Usage
do sm.json.open(todo) and then use the list of uuids to check for challenges: local success, data = pcall(sm.json.open, uuid)

### Dependency
To use the mod database in your own mod you need to add it to your dependencies. This can be done in the `description.json` of your mod.

```json
{
   "description" : "A mod for testing the Challenge Pack Database mod.",
   "localId" : "todo",
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
