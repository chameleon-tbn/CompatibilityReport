# Compatibility Report

Compatibility Report mod for [Cities: Skylines](https://steamcommunity.com/app/255710/workshop/). This will review your subscribed mods and report on compatibility issues.

### Current status
This is still in early alpha stage and not yet available on the Steam Workshop. This mod aims to become a successor for the [Mod Compatibility Checker](https://steamcommunity.com/sharedfiles/filedetails/?id=2034713132) by aubergine.

### Implemented features
* Detection of subscribed and local mods
* XML catalog with basic mod information and compatibility information
* Review of subscribed mods with the catalog information
* Text report, sorted by mod name; split into reviewed and non-reviewed mods
* Automatic download of a new catalog; no need for a mod update for every new mod or compatibility
* Use of mod groups, to allow different editions of mods as mod requirement
* Catalog Updater method (for the author only), based on web crawling the Steam Workshop and CSV import
  * Detects new mods and changes in mod information (name, required dlc/mods, source url, etc.)
  * Easy catalog maintenance with simple CSV files for updated mod and compatibility information
  * Automatic change notes and catalog versioning
  * Upload to download location is done manually, to allow for quality assurance
* Note: the current catalog only contains reviews for a limited set of mods

### Roadmap towards version 1.0 (subject to change)
* 0.4 - Alpha release on GitHub and Steam Workshop (unlisted)
  * Catalog with mod compatibilities and dependencies for all mods, from Workshop information
  * Steam pinned discussions for submissions by mod authors and others
  * Alpha testing; hopefully also some Mac and Linux testing
  * Gather feedback from modders
* 0.5 - Bugfixing and performance testing
  * Loading time and download time analysis
  * Revision of the Reporter class
  * Implement feedback if possible, or plan for future versions
* 0.6 - Settings UI, Settings XML file
  * when to scan, on-demand scanning, report sorting, what to include in report, ...
* 0.7 - Beta release
  * Code cleanup
  * Text revision
  * Completing the catalog with info from other sources (MCC mod, compatibility guides, forum, ...)
  * All catalog versions with their change notes will be publicly available
  * Public release on the Steam Workshop
  * Test branch on GitHub
  * Gather feedback from users
  * Regular new catalog releases (weekly?)

### Future ideas (might not happen)
* HTML report, categorized by severity; mod setting for text or HTML report
* In-game popup with summary and button to open report
* Version check before (down)loading a full catalog
* Reviewing local mods
* Detect Steam mods that are not updated locally (already done by [Loading Order Mod](https://steamcommunity.com/sharedfiles/filedetails/?id=2448824112))
* 'Second load' detection with warning popup
* Catalog updater based on Steam API for much better performance, easier update detection and more reliable author links
* Online catalog update procedure, supporting multiple simultaneous contributors
* TLS 1.2 support for download, if possible (probably need to switch to .NET 4)
* Detect missing mods for subscribed assets, like ETST, NExt2, Additive Shader, Trolleybus Trailer AI, etc.
* Localization

### Credits
This mod is inspired by [Mod Compatibility Checker](https://github.com/CitiesSkylinesMods/AutoRepair) by aubergine.

This mod uses code snippets from:
* **Mod Compatibility Checker** by aubergine10 a.k.a. aubergine18 ([GitHub](https://github.com/CitiesSkylinesMods/AutoRepair) | [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=2034713132))
* **Enhanced District Services** by chronofanz a.k.a. Tim ([GitHub](https://github.com/chronofanz/EnhancedDistrictServices) | [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=2303997489))
* **Customize It Extended** by Celisuis a.k.a. C# ([GitHub](https://github.com/Celisuis/CustomizeItExtended) | [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=1806759255))
* **Change Loading Screen 2** by BloodyPenguin ([GitHub](https://github.com/bloodypenguin/ChangeLoadingImage) | [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=1818482110))

A big thanks to these modders and all those others for making their code available for education and re-use.

### Disclaimer
I'm not an experienced programmer. I knew some programming fundamentals and taught myself C# with online tutorials, reading other peoples code and lots of experimenting. My code might be sloppy and inefficient. I'm open to suggestions and constructive feedback in the discussions.