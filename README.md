# Compatibility Report version 0.8

Compatibility Report mod for [Cities: Skylines](https://steamcommunity.com/app/255710/workshop/). This reports compatibility issues and missing dependencies for your subscribed mods.

### Current status
This mod is available at the [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=2633433869). This is a successor to the [Mod Compatibility Checker](https://github.com/CitiesSkylinesMods/AutoRepair) by aubergine.


### Implemented features
* Detection of subscribed and local mods.
* XML catalog with basic mod information and compatibility information.
* Review of subscribed mods with the catalog information.
* Text and/or HTML report, sorted by mod name. Split into multiple categories, based on issue severity.
* Automatic and on-demand download of a new catalog. No need for a mod update for every new mod or compatibility change.
* Mod options for easy access to reports and catalog download.
* Support for mod groups, to allow different editions (e.g. stable and test) of mods as mod requirement.
* Catalog Updater method (for mod maintainer only), based on web crawling the Steam Workshop and CSV import.
  * Automatically detects new mods and changes in mod information: name, required DLC/mods, source URL, ...
  * Easy catalog maintenance with simple CSV files for updated mod and compatibility information. This allows for catalog maintenance by multiple people in the future.
  * Automatic change notes and catalog versioning.
  * Dedicated UI for easier maintenance.
  * Gzipped catalog for lower bandwidth usage (partially implemented).
  * Manual upload of a new catalog, after a quality assurance check.

### Roadmap and future ideas
The roadmap towards version 1.0 and the future ideas have been moved to the [Steam Workshop page](https://steamcommunity.com/workshop/filedetails/discussion/2633433869/3162083441792162041/).

### Credits
This mod is inspired by and uses code from [Mod Compatibility Checker](https://github.com/CitiesSkylinesMods/AutoRepair) by aubergine. It also uses code snippets from:
* **Enhanced District Services** by chronofanz a.k.a. Tim ([GitHub](https://github.com/chronofanz/EnhancedDistrictServices) | [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=2303997489))
* **Change Loading Image 2** by BloodyPenguin ([GitHub](https://github.com/bloodypenguin/ChangeLoadingImage) | [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=1818482110))
* **Loading Screen Mod** by thale5: ([GitHub](https://github.com/thale5/LSM) | [SteamWorkshop](https://steamcommunity.com/sharedfiles/filedetails/?id=667342976))

A big thanks to these modders and all those others for making their code available for education and re-use.

Special thanks to:
* **Aubergine** for the awesome MCC mod.
* **Chamëleon** for feedback, testing and helping with support and communication.
* **Asterisk** for feedback, testing and patiently explaning a lot.
* **LemonsterOG** for being super helpful and patient on almost every mod page in the Workshop. Lemon often used the old MCC for supporting users, and was an inspiration for creating this successor.
* All above and many others for providing a friendly atmosphere on Discord.
* See the Steam Workshop for full credits.

### Disclaimer
I'm not an experienced programmer. I knew programming fundamentals and taught myself C# with online tutorials, reading other peoples code, browsing Stack Overflow and lots of experimenting. My code is surely sloppy, inefficient and ignoring lots of conventions. I'm open to suggestions and constructive feedback in the [Discussions section](https://github.com/Finwickle/CompatibilityReport/discussions).
