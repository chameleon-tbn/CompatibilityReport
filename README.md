# Compatibility Report

Compatibility Report mod for [Cities: Skylines](https://steamcommunity.com/app/255710/workshop/). This will report compatibility issues for your subscribed mods.

### Current status
This is still in early alpha stage and not yet available on the Steam Workshop. This mod aims to become a successor for the [Mod Compatibility Checker](https://steamcommunity.com/sharedfiles/filedetails/?id=2034713132) by aubergine.

Due to the early development stage and very frequent changes to all parts of the code, pull requests are discouraged right now. The [Discussions section](https://github.com/Finwickle/CompatibilityReport/discussions) is open for questions and feedback.

### Implemented features
* Detection of subscribed and local mods
* XML catalog with basic mod information and compatibility information
* Review of subscribed mods with the catalog information
* Text report, sorted by mod name; split into reviewed and non-reviewed mods
* Automatic download of a new catalog; no need for a mod update for every new mod or compatibility
* Support for mod groups,  to allow different editions (e.g. stable and test) of mods as mod requirement
* Catalog Updater method (for mod maintainer only), based on web crawling the Steam Workshop and CSV import
  * Detects new mods and changes in mod information (name, required dlc/mods, source url, etc.)
  * Easy catalog maintenance with simple CSV files for updated mod and compatibility information. This allows for catalog maintenance by multiple people.
  * Automatic change notes and catalog versioning
  * Manual upload of a new catalog, after quality assurance checks
* Note: The current catalog only contains basic mod information and no reviews yet

### Roadmap towards version 1.0 (subject to change)
* 0.4 - Code revision & cleanup
* 0.5 - Alpha release on GitHub and Steam Workshop (unlisted)
  * Catalog with mod compatibilities and dependencies for most mods
  * Steam pinned discussions for submissions by mod authors and others
  * Alpha testing; hopefully also some Mac and Linux testing
  * Gather feedback from modders
* 0.6 - Bugfixing and performance testing
  * Loading time and download time analysis
  * Implement feedback where possible, or plan for future versions
* 0.7 - Settings UI, Settings XML file
  * when to scan, on-demand scanning, report sorting, what to include in report, ...
* 0.8 - Standalone Updater tool for easier scheduling
* 0.9 - Beta release
  * Code cleanup
  * Completing the catalog with info from other sources (MCC mod, compatibility guides, forum, discords, ...)
  * Public release on the Steam Workshop
  * Public location for all catalog versions with their change notes
  * Test branch on GitHub
  * Gather feedback from users
  * Regular new catalog releases (weekly?)
* 1.0 - Stable release

### Future ideas (might not happen)
* 1.1 HTML report, categorized by severity; mod setting for text or HTML report
* 1.2 In-game popup with summary and button to open report
* 2.0 Localization
* Version check before (down)loading a full catalog
* Detect Steam mods that are not updated locally (already done by [Loading Order Mod](https://steamcommunity.com/sharedfiles/filedetails/?id=2448824112))
* 'Second load' detection with warning popup
* Reviewing local mods
* Online catalog update procedure, supporting multiple simultaneous contributors
* Interface for getting info about an unsubscribed mod, to review compatibility before subscribing
* Replace web crawler updater method by Steam API, for much better performance, easier update detection and more reliable author links
* Detect missing mods for subscribed assets, like ETST, NExt2, Additive Shader, Trolleybus Trailer AI, etc. (needs Steam API)
* TLS 1.2 support for download, if possible (needs .NET 4)

### Credits
This mod is inspired by [Mod Compatibility Checker](https://github.com/CitiesSkylinesMods/AutoRepair) by aubergine.

This mod uses code snippets from:
* **Mod Compatibility Checker** by aubergine10 a.k.a. aubergine18 ([GitHub](https://github.com/CitiesSkylinesMods/AutoRepair) | [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=2034713132))
* **Enhanced District Services** by chronofanz a.k.a. Tim ([GitHub](https://github.com/chronofanz/EnhancedDistrictServices) | [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=2303997489))
* **Change Loading Screen 2** by BloodyPenguin ([GitHub](https://github.com/bloodypenguin/ChangeLoadingImage) | [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=1818482110))

A big thanks to these modders and all those others for making their code available for education and re-use.

### Disclaimer
I'm not an experienced programmer. I knew programming fundamentals and taught myself C# with online tutorials, reading other peoples code, browsing Stack Overflow and lots of experimenting. My code is surely sloppy, inefficient and ignoring lots of conventions. I'm open to suggestions and constructive feedback in the [Discussions section](https://github.com/Finwickle/CompatibilityReport/discussions).