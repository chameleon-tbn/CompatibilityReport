# Compatibility Report

### Version 2.3.3
* Fixed a webcraler bug (thanks krzychu!)
* Updated Translations
* Updated Catalog

### Version 2.3.2
* Redesign of the Compatibility Report for better useability and adding a dark mode (Thanks to mxdanger for this great work!).
* Updated Translations
* Updated Catalog to 7.9

### Version 2.3.1
* Updated some internal texts to match more standards for mods for modders and asset creators.
* Updated the general note in the header of the compatibility report.
* Updated Translations
* Updated Catalog to 7.8

### Version 2.3.0
* Merged Status "MusicCopyrightFree", "MusicCopyrighted", "MusicCopyrightUnknown" to "MusicCopyright" with the same information text.
* Added Status "SourceNotPublic" to Inform about mods where no public source code is available.
* Added all new DLCs, CCPs and Radios up to Financial Districts to "RequiredDlcs".
* Changed the logic when to mark as "Deprecated". A mod will be marked as "Deprecated" only, if a successor is available. 
* Old mods without successor might have alternatives listed, Status "Abandoned" and can even have Stability "Stable"
* Stability "RequiresIncompatibleMod" can be set if a mod have a broken mod as dependency, that is not (yet) marked as Incompatible in the workshop.

### Version 2.2.0
* Improved version check for loading catalog in Updater run
* CSV Import - unlisted check downloads analyzing file in memory instead of saving temp locally
* Version bump, no-workshop check disabled in Debug builds
* Removed obsolete code
* Updated translations
* Catalog maintenance to remove wrong status information
* Catalog 6.14

### Version 2.1.1
* Added Picker.log, NetworkAnarchy.log and MoveIt.log to the CompatibilityReport_Logs*.zip (Thanks to Quboid!)
* fixed some typos
* added a missing translation string
* updated translations

### Version 2.1.0
* added functionality to ZIP log files for Support
* added links to broken mods and assets as well as links to recommended mods
* various optimisations regarding to the update process

### Version 2.0.4
* catalog version 6.1
* fixed parsing of stability notes
* catalog versioning fix
* added svg flag to support new language in the html report 
* form link text update
* new status column in the Processed mods html table
* typo fixes
* improving Settings UI
* added missing translations
* fixed default report type selection
* fixed NullReferenceException when generating the config

### Version 2.0.0
* Detection of subscribed and local mods.
* NEW: Gzipped XML catalog with basic mod information and compatibility information integrated in the Mod itself. No need for side downloads and no issues with download limits or non-accessible storage any more.
* Review of subscribed mods with the catalog information.
* HTML and/or text report, sorted by mod name. Split into multiple categories, based on issue severity.
* NEW: Mod options for easy access to reports, catalog download, feedback via report-form or on discord, links to recommended mods and also broken mods.
* Support for mod groups, to allow different editions (e.g. stable and test) of mods as mod requirement.
* NEW: Basic translation frame work (UI and catalog standard fields. Languages for notes will be integrated later)
* Catalog Updater method (for mod maintainers only), based on web crawling the Steam Workshop and CSV import.
* Automatically detects new mods and changes in mod information: name, required DLC/mods, source URL, ...
* Easy catalog maintenance with simple CSV files for updated mod and compatibility information. This allows for catalog maintenance by multiple people, upload ofc only through mod-owner.
* Automatic change notes and catalog versioning.
* Dedicated UI for easier maintenance.

### Version 0.8.0
* HTML report generation
* Options screen with basic info about catalog state and lots of configurable settings
* Updater: a lot of code improvements and changes, created dedicated UI for easier maintenance

### Version 0.7.6
* Support for fake subscriptions, for testing
* Support for one-time actions of the Updater

### Version 0.7.5
* Minor code improvements
* Improved logging
* Bugfix for the Updater: some GitHub source URLs not detected

### Version 0.7.4
* Don't report incompatibilities if one mod is the successor of the other mod
* Report 'different release type' (stable vs beta) at both mods
* Minor textual changes
* Gzip support for the catalog (thanks Mircea Chirea!)
* Bugfix: bundled catalog not located on MacOS
* Updater: allow author name change to Steam ID
* Bugfix for the Updater: adding required mod as successor/etc. should not be allowed
* NOTE: The bundled catalog is changed to gzip, but the catalog download file not yet
* NOTE: Catalog major version has increased. Previous mod versions might not be able to read new catalogs

### Version 0.7.3
* Minor textual changes
* Bugfix: current game version was not detected correctly

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
* Added 'Work when disabled' status that suppresses warnings about disabled state for certain mods
* Added local mod count, local mod path and warning about failed catalog download to the report
* Minor improvements and textual changes
* Airports DLC added for mods that will require this
* Bugfix: local mods counted as non-reviewed subscribed mods
* Updater: Datadumper changes
* Bugfix for the Updater: Mod review dates now correctly updated
* NOTE: Catalog major version has increased. New catalogs cannot be read by previous mod versions

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
