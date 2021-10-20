# Compatibility Report

Compatibility Report mod for [Cities: Skylines](https://steamcommunity.com/app/255710/workshop/). This will report compatibility issues for your subscribed mods.

### Current status
This is still in early alpha stage and not yet available on the Steam Workshop. It has not been thoroughly tested yet and has only very limited data at this time. This mod aims to become a successor for the [Mod Compatibility Checker](https://steamcommunity.com/sharedfiles/filedetails/?id=2034713132) by aubergine.

Due to the early development stage and very frequent changes to the code, pull requests are discouraged right now. Please create an [issue](https://github.com/Finwickle/CompatibilityReport/issues) or use the [Discussions section](https://github.com/Finwickle/CompatibilityReport/discussions) for questions and feedback.

### Implemented features
* Detection of subscribed and local mods.
* XML catalog with basic mod information and compatibility information.
* Review of subscribed mods with the catalog information.
* Text report, sorted by mod name. Split into multiple categories, based on issue severity.
* Automatic download of a new catalog. No need for a mod update for every new mod or compatibility.
* Support for mod groups, to allow different editions (e.g. stable and test) of mods as mod requirement.
* Catalog Updater method (for mod maintainer only), based on web crawling the Steam Workshop and CSV import.
  * Automatically detects new mods and changes in mod information: name, required DLC/mods, source URL, ...
  * Easy catalog maintenance with simple CSV files for updated mod and compatibility information. This allows for catalog maintenance by multiple people in the future.
  * Automatic change notes and catalog versioning.
  * Manual upload of a new catalog, after a quality assurance check.

### Roadmap towards version 1.0 (subject to change)
* 0.5 - Alpha release on Steam Workshop
  * Catalog with mod compatibilities and dependencies for most mods.
  * Steam pinned discussions for submissions by mod authors and others.
  * Alpha testing; hopefully also some Mac and Linux testing.
  * Gather feedback from modders.
* 0.6 - Bugfixing and performance testing
  * Loading time and download time analysis.
  * Implement feedback where possible, or plan for future versions.
* 0.7 - Settings UI, Settings XML file
  * when to scan, on-demand scanning, report sorting, what to include in report, 'open report' button, ...
* 0.8 - Standalone Updater tool for easier scheduling
* 0.9 - Beta release
  * Code cleanup.
  * Completing the catalog with info from other sources: feedback, MCC mod, guides, forum, discord, ...
  * Public release on the Steam Workshop.
  * Gather feedback from users.
  * Public location for all catalog versions with their change notes.
  * Regular new catalog releases (weekly?).
  * Test branch on GitHub.
* 1.0 - Stable release

### Future ideas (might not happen)
* 1.1 HTML report; mod setting for text or HTML report.
* 1.2 In-game popup with summary and button to open report.
* 'Second load' detection with warning popup.
* Interface for getting info about an unsubscribed mod, to review compatibility with currently subscribed mods.
* Detect Steam mods that are not updated locally (already done by [Loading Order Mod](https://steamcommunity.com/sharedfiles/filedetails/?id=2448824112)).
* Integration with [Loading Order Mod](https://steamcommunity.com/sharedfiles/filedetails/?id=2448824112)?
* Localization. Part of the report text is in the CSV import, making it harder to keep translations up to date.
* Version check before (down)loading a full catalog.
* In-game catalog update procedure and upload/merge it with the online catalog.
* Online catalog update procedure, supporting multiple simultaneous contributors.
* Reviewing local mods.
* Replace WebCrawler by Steam API, for better performance, easier update detection and more reliable author links.
* Detect missing required mods for assets, like ETST, NExt2, Trolleybus Trailer AI, ... (needs Steam API).
* TLS 1.2 support for download, if possible (needs .NET 4.6+).

### Credits
This mod is inspired by and uses code from [Mod Compatibility Checker](https://github.com/CitiesSkylinesMods/AutoRepair) by aubergine. It also uses code snippets from:
* **Enhanced District Services** by chronofanz a.k.a. Tim ([GitHub](https://github.com/chronofanz/EnhancedDistrictServices) | [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=2303997489))
* **Change Loading Image 2** by BloodyPenguin ([GitHub](https://github.com/bloodypenguin/ChangeLoadingImage) | [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=1818482110))

A big thanks to these modders and all those others for making their code available for education and re-use.

Special thanks to:
* **LemonsterOG** for being super helpful and patient on almost every mod page in the Workshop. Lemon often used the old MCC for supporting users, and was an inspiration for creating this successor.
* **asterisk** for feedback and testing.
* **Chamëleon** for feedback and testing.
* All above and many others for providing a friendly atmosphere on Discord.

### Disclaimer
I'm not an experienced programmer. I knew programming fundamentals and taught myself C# with online tutorials, reading other peoples code, browsing Stack Overflow and lots of experimenting. My code is surely sloppy, inefficient and ignoring lots of conventions. I'm open to suggestions and constructive feedback in the [Discussions section](https://github.com/Finwickle/CompatibilityReport/discussions).
