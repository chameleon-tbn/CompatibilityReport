# Compatibility Report

### Version 0.7.2
* Minor textual changes
* Preparation for new DLCs
* New catalog download location
* Minor Updater improvements

### Version 0.7.1
* Minor textual changes
* Improved logging
* Bugfix: Catalog download might hang in rare cases

### Version 0.7
* No longer in beta

### Version 0.6.5 beta
* Logging of path/filenames when saving fails
* Bugfix: report no longer saved on MacOS (and possibly Linux) in version 0.6.4

### Version 0.6.4 beta
* Ignore Map Themes, which are technically mods, in the report

### Version 0.6.3 beta
* Catalog major version has increased. New catalogs cannot be read by previous mod versions
* Added 'Work when disabled' status that suppresses warnings about disabled state for certain mods
* Added local mod count, local mod path and warning about failed catalog download to the report
* Minor improvements and textual changes
* Airports DLC added for mods that will require this
* Bugfix: local mods counted as non-reviewed subscribed mods
* Updater: Datadumper changes
* Bugfix for the Updater: Mod review dates now correctly updated

### Version 0.6.2 beta
* Added note to the report about non-reviewed subscribed mods
* Removed note in the report about beta version
* Minor textual changes
* Bugfix: some non-reviewed mods were counted as reviewed
* Updater: Gist source URLs now automatically found by the Updater
* Updater: Datadumper changes
* Bugfixes for the Updater

### Version 0.6.1 beta
* Textual changes and bugfixes

### Version 0.6 beta
* Public beta on Steam Workshop
* Bugfixes for the Updater

### Version 0.5.3 alpha
* Textual changes and bugfixes

### Version 0.5.2 alpha
* Textual changes and bugfixes
* Steam Workshop preview image

### Version 0.5.1 alpha
* Minor report changes
* Mod summary list added to the end of the report

### Version 0.5 alpha - Steam Workshop release
* Steam Workshop release
* Textual changes and bugfixes

### Version 0.4.3 alpha
* Miscellaneous bugfixes

### Version 0.4.2 alpha
* Bugfixes for reporter and updater

### Version 0.4.1 alpha
* Report revision
* Split report into multiple categories, based on issue severity

### Version 0.4 alpha - Code revision and cleanup
* Major code revision and restructure
* Textual changes
* Bugfixes
* Catalog rebuilt, and limited compatibility information added
* GitHub repository set to public

### Version 0.3 private - Catalog Updater: CSV import
* Catalog updater method based on CSV import, in addition to the web crawler
  - Simple CSV actions for easy maintenance  
  - Imports new and changed mods, compatibilities, groups and authors
  - Support for exclusions, to avoid manual changes being overwritten by the web crawler
  - [Updater Guide](https://github.com/Finwickle/CompatibilityReport/blob/main/CompatibilityReport/Updater/Updater%20Guide.md) created for easier sharing or taking over of mod support in the future
* Web crawler enhanced:
  - Detection of author retirement (no mod updates for at least a year)
* Mod renamed to Compatibility Report
* Catalog rebuilt due to mod name change and structure changes

### Version 0.2 private - Catalog Updater: Steam Workshop web crawler
* Catalog updater method based on web crawling the Steam Workshop
  - detects new mods and changes in mod information (name, required DLC/mods, source URL, etc.)
* Automatic catalog versioning and change notes
* Support for unlisted and removed mods
* Support for mod groups, to allow different editions of mods as mod requirement
* Second catalog created, with information of all Steam Workshop mods, but without reviews

### Version 0.1.1 private
* Bugfixes and textual changes

### Version 0.1 private - Initial build
* Detection of subscribed and local mods
* Xml catalog with basic mod information and compatibility information
* Review of all subscribed mods with the catalog information
* Text report, sorted by mod name; split into reviewed and non-reviewed mods
* Automatic download of a new catalog; no need for a mod update for every new incompatibility
* Logging
* First catalog created with only the 5 built-in mods