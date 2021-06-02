# Mod Checker

Mod Checker mod for Cities: Skylines. This will review your subscribed mods and report on compatibility issues.

### Current status
This is still in early alpha stage and not yet available on the Steam Workshop. This mod aims to become a successor for the [Mod Compatibility Checker](https://steamcommunity.com/sharedfiles/filedetails/?id=2034713132) by aubergine.

### Implemented features
* Detection of subscribed and local mods
* Xml catalog with basic mod information and compatibility information
* Review of all subscribed mods with the catalog information
* Text report, sorted by mod name; split into reviewed and non-reviewed mods
* Automatic download of a new catalog; no need for a mod update for every new incompatibility
* Use of mod groups, to allow different editions of mods as mod requirement
* Note: the current catalog contains only builtin mods

### Roadmap towards version 1.0 (might change)
* 0.2 - Catalog auto-update, based on webcrawling
  * Detection of new and removed mods
  * Detection of changes to mod name, update date, author id/name, required dlc/mods, source url, ...
  * Automatic change notes and catalog versioning
* 0.3 - Catalog manual update routine, probably based on csv
  * Again with automatic change notes and catalog versioning
  * First real catalog, with basic information about 1600+ mods and related authors
  * Catalog also containing mod compatibility information for a limited set of mods
  * Set up download site
* 0.4 - Alpha release on GitHub and Steam Workshop (unlisted?)
  * Catalog with mod compatibilities and dependencies for many mods (not complete yet)
  * Steam pinned discussions for submissions by mod authors and others
  * Alpha testing by users; hopefully also Mac and Linux testing
* 0.5 - Bugfixing and performance testing
* 0.6 - HTML report, categorized by severity
* 0.7 - Settings UI; when to scan, on-demand scanning, text or HTML report, report sorting, ...
  * settings xml
* 0.8 - Beta release
  * Code cleanup
  * Text revision
  * Completing the catalog
  * Create Test branch on GitHub

### Future ideas (might not happen)
* In-game popup with summary and button to open report
* Version check before (down)loading a full catalog
* Reviewing local mods
* Detect mods that are not updated locally
* 'Second load' detection with warning popup
* Catalog updater based on Steam API for better update detection and performance
* Online catalog update procedure; supporting multiple contributors
* TLS 1.2 support for download, if possible (might need to switch to .NET 4)
* Detect missing mods for subscribed assets, like ETST, NExt2, Additive Shader, Trolleybus Trailer AI, etc.
* Localization

### Credits
This mod is inspired by and partially based on [Mod Compatibility Checker / AutoRepair](https://github.com/CitiesSkylinesMods/AutoRepair) by aubergine10.

This mod uses code snippets from:
* [Mod Compatibility Checker / AutoRepair](https://github.com/CitiesSkylinesMods/AutoRepair) by aubergine10 ([workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=2034713132))
* [Enhanced District Services](https://github.com/chronofanz/EnhancedDistrictServices) by chronofanz ([workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=2303997489))
* [Customize It Extended](https://github.com/Celisuis/CustomizeItExtended) by Celisuis ([workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=1806759255))
* [Change Loading Screen 2](https://github.com/bloodypenguin/ChangeLoadingImage) by bloodypenguin ([workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=1818482110))

A big thanks to these modders and all those others for making their code available for education and re-use.

### Disclaimer
I'm not an experienced programmer. I knew some programming fundamentals and taught myself C# with online tutorials, reading other peoples code and lots of experimenting. My code might be sloppy and inefficient. I'm open to suggestions and constructive feedback in the discussions.