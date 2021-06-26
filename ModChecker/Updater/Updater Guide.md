# Mod Checker - ManualUpdater Guide

The manual updater updates the catalog with manual changes and additions from CSV files. The files should be placed in the Updater folder where the updated catalogs are written as well. Update actions can be bundled in one CSV file or split into multiple files. After updating, the processed CSV files will be combined into one file, named after the new catalog (ModCheckerCatalog_v#.####_ManualUpdates.csv). Filenames for the CSV files are not relevant, except that any ending in '_ManualUpdates' will be ignored. The CSV files are removed after processing to avoid duplicate additions to the catalog.

The lines in the CSV files all start with an action, followed by a steam ID (for the mod, group, author, etc. involved), often followed by additional data. Some actions will create exclusions in the catalog, to avoid the updates being overwritten by the AutoUpdater.
Lines starting with a '#' are considered comments and will be ignored by the updater. They will be copied to the combined file though.

### Available actions
*Parameters enclosed in square brackets are optional. Bold parameters are defaults. :zap: means an exclusion will be created.*
* Add_Mod, \<mod steam ID\>, [**unlisted** | removed]
* Add_ArchiveURL, \<mod steam ID\>, \<url\>
* Add_SourceURL, \<mod steam ID\>, \<url\> :zap:
* Add_GameVersion, \<mod steam ID\>, \<game version string\> :zap: (exclusion only if Workshop has different game version tag)
* Add_RequiredDLC, \<mod steam ID\>, \<single DLC string\> :zap:
* Add_Status, \<mod steam ID\>, \<status\> :zap: (exclusion only for statuses Removed and Unlisted)
* Add_Note, \<mod steam ID\>, \<note\>
* Add_RequiredMod, \<mod or group ID\>, \<steam ID\> :zap:
* Add_NeededFor, \<mod steam ID\>, \<steam ID\>
* Add_Successor, \<mod steam ID\>, \<steam ID\>
* Add_Alternative, \<mod steam ID\>, \<steam ID\>
* ...
* Remove_Mod, \<mod steam ID\>
* ...
