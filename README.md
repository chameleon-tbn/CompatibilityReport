# Mod Checker

Mod Checker mod for Cities: Skylines. This will review your subscribed mods and report on compatibility issues.

### Current status
This is still in early alpha stage and not yet available on the Steam Workshop. This mod aims to become a successor for the [Mod Compatibility Checker](https://steamcommunity.com/sharedfiles/filedetails/?id=2034713132) by aubergine.

### Implemented features
* Detection of subscribed and local mods
* Loading of an xml catalog with mod and mod compatibility information (current catalog has only fake information)
* Investigation of all mods against the catalog 
* Text report, sorted by mod name
* Automatic download of a new catalog, saved for future sessions; no mod update for every new incompatibility
* Logging

### Roadmap towards version 1.0 (might change)
* 0.2 - Catalog update routine; probably based on webcrawling, not Steam API
  * Detection of changes to mod name, update date, version, author tag/name, contributors, ...
* 0.3 - HTML report, categorized by severity
* 0.4 - First catalog, with basic information about 1500+ mods and related authors
  * Set up download site
* 0.5 - Full catalog with mod compatibilities and dependencies; will not be complete yet
  * Steam Workshop upload, with pinned discussion for submissions by mod authors
  * Alpha testing; hopefully Mac and Linux testing
* 0.6 - Performance testing; decide when to run the check (probably make it a mod setting)
  * Code cleanup
  * Completing the catalog
* 0.7 - Settings UI and settings xml; when to scan, on-demand scanning, text or html report, report sorting, ...

### Future ideas (might not happen)
* In-game popup with summary and button to open report
* Version check before (down)loading a full catalog
* 'Second load' detection with warning popup
* Reviewing local mods
* Detect mods that are not updated
* Automatic catalog versioning, including change notes
* Online catalog update procedure; supporting multiple contributors
* TLS 1.2 support for download, if possible
* Detect missing mods for subscribed assets, like ETST, NExt2, Additive Shader, Trolleybus Trailer AI, etc.
* Localization

### Credits
This mod is based on [Mod Compatibility Checker / AutoRepair](https://github.com/CitiesSkylinesMods/AutoRepair) ([workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=2034713132)) by aubergine10.

This mod also uses code snippets from:
* [Enhanced District Services](https://github.com/chronofanz/EnhancedDistrictServices) by chronofanz ([workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=2303997489))
* [Customize It Extended](https://github.com/Celisuis/CustomizeItExtended) by Celisuis ([workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=1806759255))
* [Change Loading Screen 2](https://github.com/bloodypenguin/ChangeLoadingImage) by bloodypenguin ([workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=1818482110)).

A big thanks to these modders and many others for making their code available for education and re-use.

### Disclaimer
I'm not an experienced programmer. I knew some programming fundamentals and taught myself C# with online tutorials, reading other peoples code and lots of experimenting. My code might be sloppy and inefficient. I'm open to suggestions and constructive criticism in the discussions.