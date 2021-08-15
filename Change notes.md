# Compatibility Report

### Version 0.3   - Catalog Updater: CSV import
* Catalog updater method based on CSV import, in addition to the web crawler
  - Imports new and changed Mods, Groups, Compatibilities and Authors
  - Support for Exclusions, so manual changes are not overwritten by the web crawler
  - Updater Guide created for easier sharing or taking over of mod support in the future
* Web crawler enhanced:
  - Detection of author retirement (no mod updates for at least a year)
* Mod renamed to Compatibility Report. Catalogs rebuilt due to name change.
* Third catalog created with additional mod information and a limited number of mod reviews

### Version 0.2   - Catalog Updater: Steam Workshop web crawler
* Catalog updater method based on web crawling the Steam Workshop
  - detects new mods and changes in mod information (name, required dlc/mods, source url, etc.)
* Automatic catalog versioning and change notes
* Support for unlisted and removed mods
* Support for mod groups, to allow different editions of mods as mod requirement
* Second catalog created, with information of all Workshop mods, but without reviews

### Version 0.1.1 - Bugfixes and textual changes

### Version 0.1   - Initial build
* Detection of subscribed and local mods
* Xml catalog with basic mod information and compatibility information
* Review of all subscribed mods with the catalog information
* Text report, sorted by mod name; split into reviewed and non-reviewed mods
* Automatic download of a new catalog; no need for a mod update for every new incompatibility
* Logging
* First catalog created with only the 5 builtin mods