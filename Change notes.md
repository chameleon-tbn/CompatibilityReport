# Compatibility Report

### Version 0.4 - Code revision and cleanup
* Major code revision and restructure
* Textual changes
* Bugfixes
* Catalogs rebuilt and third catalog created with mod reviews

### Version 0.3 - Catalog Updater: CSV import
* Catalog updater method based on CSV import, in addition to the web crawler
  - Simple CSV actions for easy maintenance  
  - Imports new and changed mods, compatibilities, groups and authors
  - Support for exclusions, to avoid manual changes being overwritten by the web crawler
  - [Updater Guide](https://github.com/Finwickle/CompatibilityReport/blob/main/CompatibilityReport/Updater/Updater%20Guide.md) created for easier sharing or taking over of mod support in the future
* Web crawler enhanced:
  - Detection of author retirement (no mod updates for at least a year)
* Mod renamed to Compatibility Report. 
* Catalogs rebuilt due to mod name change and structure changes.

### Version 0.2 - Catalog Updater: Steam Workshop web crawler
* Catalog updater method based on web crawling the Steam Workshop
  - detects new mods and changes in mod information (name, required DLC/mods, source URL, etc.)
* Automatic catalog versioning and change notes
* Support for unlisted and removed mods
* Support for mod groups, to allow different editions of mods as mod requirement
* Second catalog created, with information of all Steam Workshop mods, but without reviews

### Version 0.1.1 - Bugfixes and textual changes

### Version 0.1 - Initial build
* Detection of subscribed and local mods
* Xml catalog with basic mod information and compatibility information
* Review of all subscribed mods with the catalog information
* Text report, sorted by mod name; split into reviewed and non-reviewed mods
* Automatic download of a new catalog; no need for a mod update for every new incompatibility
* Logging
* First catalog created with only the 5 builtin mods