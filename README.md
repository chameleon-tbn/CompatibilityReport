# Compatibility Report

Compatibility Report mod for Cities: Skylines. This will review your subscribed mods and report on compatibility issues.

### Current status
This is still in early alpha stage and not yet available on the Steam Workshop. This mod aims to become a successor for the [Mod Compatibility Checker](https://steamcommunity.com/sharedfiles/filedetails/?id=2034713132) by aubergine.

### Implemented features
* Detection of subscribed and local mods
* XML catalog with basic mod information and compatibility information
* Review of subscribed mods with the catalog information
* Text report, sorted by mod name; split into reviewed and non-reviewed mods
* Automatic download of a new catalog; no need for a mod update for every new mod or compatibility
* Use of mod groups, to allow different editions of mods as mod requirement
* Catalog AutoUpdater method (for the author only), based on web crawling the Steam Workshop
  * Detects new mods and changes in mod information (name, required dlc/mods, source url, etc.)
  * Automatic change notes and catalog versioning
  * Upload to download location is done manually
  * All catalog versions with their change notes will be available for download
* Note: the current catalog contains basic information about all mods but no reviews yet

### Roadmap towards version 1.0 (might change)
* 0.3 - Catalog ManualUpdater method, based on CSV
  * Catalog containing mod compatibility information for a limited set of mods
* 0.4 - Alpha release on GitHub and Steam Workshop (unlisted)
  * Catalog with mod compatibilities and dependencies for all mods, from Workshop information
  * Review of the Reporter class
  * Steam pinned discussions for submissions by mod authors and others
  * Alpha testing; hopefully also Mac and Linux testing
  * Gather feedback from modders
* 0.5 - Bugfixing and performance testing
  * Loading time and download time analysis
* 0.6 - Settings UI, Settings XML file
  * when to scan, on-demand scanning, report sorting, what to include in report, ...
* 0.7 - Beta release
  * Code cleanup
  * Text revision
  * Completing the catalog with info from other sources (MCC mod, compatibility guides, forum, ...)
  * Test branch on GitHub
  * Public release on the Steam Workshop
  * Gather feedback from users
  * Regular new catalog releases (weekly?)

### Future ideas (might not happen)
* HTML report, categorized by severity; setting for text or HTML report
* In-game popup with summary and button to open report
* Version check before (down)loading a full catalog
* Reviewing local mods
* Detect Steam mods that are not updated locally
* 'Second load' detection with warning popup
* Catalog updater based on Steam API for better update detection and much better performance
* Online catalog update procedure, supporting multiple simultaneous contributors
* TLS 1.2 support for download, if possible (probably need to switch to .NET 4)
* Detect missing mods for subscribed assets, like ETST, NExt2, Additive Shader, Trolleybus Trailer AI, etc.
* Localization

### Credits
This mod is inspired by [Mod Compatibility Checker / AutoRepair](https://github.com/CitiesSkylinesMods/AutoRepair) by aubergine10.

This mod uses code snippets from:
* [Mod Compatibility Checker / AutoRepair](https://github.com/CitiesSkylinesMods/AutoRepair) by aubergine10 ([workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=2034713132))
* [Enhanced District Services](https://github.com/chronofanz/EnhancedDistrictServices) by chronofanz ([workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=2303997489))
* [Customize It Extended](https://github.com/Celisuis/CustomizeItExtended) by Celisuis ([workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=1806759255))
* [Change Loading Screen 2](https://github.com/bloodypenguin/ChangeLoadingImage) by bloodypenguin ([workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=1818482110))

A big thanks to these modders and all those others for making their code available for education and re-use.

### Disclaimer
I'm not an experienced programmer. I knew some programming fundamentals and taught myself C# with online tutorials, reading other peoples code and lots of experimenting. My code might be sloppy and inefficient. I'm open to suggestions and constructive feedback in the discussions.