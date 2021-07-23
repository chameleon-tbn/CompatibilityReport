# Mod Checker - ManualUpdater Guide

The manual updater updates the catalog with manual changes and additions from CSV files. The files should be placed in the Updater folder where the updated catalogs are written as well. Update actions can be bundled in one CSV file or split into multiple files. Filenames are not relevant, but they should end in .csv. After updating, the processed CSV files will be combined into one file with the name of the new catalog and ending in '_ManualUpdates.txt'. The processed CSV files are renamed to .txt to avoid duplicate additions to the catalog.

The lines in the CSV files all start with an action, often followed by a steam ID (for the mod, group or author), often followed by additional data. Some actions will create exclusions in the catalog, to prevent the AutoUpdater from overwriting these manual updates. Actions and parameters are not case sensitive (except for names etc. that appears in reports). Spaces around comma separators are ignored. Commas in mod name and notes are supported.

Lines starting with a '#' are considered comments and will be ignored by the updater. They will be copied to the combined file though.

### Available mod actions
Parameters enclosed in square brackets are optional. The symbol :zap: means an exclusion will be created.
* Add_Mod, \<mod ID\> [, \<author ID\> [, \<mod name\>] ] *(mod will have the 'unlisted' status)*
* Add_ArchiveURL, \<mod ID\>, \<url\>
* Add_SourceURL, \<mod ID\>, \<url\> :zap:
* Add_GameVersion, \<mod ID\>, \<game version string\> :zap: *(exclusion only if Workshop has different game version tag)*
* Add_RequiredDLC, \<mod ID\>, \<single DLC string\> :zap:
* Add_RequiredMod, \<mod ID\>, \<required mod or group ID\> :zap:
* Add_Successor, \<mod ID\>, \<successor mod ID\>
* Add_Alternative, \<mod ID\>, \<alternative mod ID\>
* Add_Status, \<mod ID\>, \<single status string\> :zap: *(exclusion only for Unlisted status)*
* Add_Note, \<mod ID\>, \<note\>
* Remove_Mod, \<mod ID\> *(only for unlisted and removed mods)*
* Remove_ArchiveURL, \<mod ID\>
* Remove_SourceURL, \<mod ID\>
* Remove_GameVersion, \<mod ID\>
* Remove_RequiredDLC, \<mod ID\>, \<single DLC string\>
* Remove_RequiredMod, \<mod ID\>, \<required mod or group ID\>
* Remove_Successor, \<mod ID\>, \<successor mod ID\>
* Remove_Alternative, \<mod ID\>, \<alternative mod ID\>
* Remove_Status, \<mod ID\>, \<single status\>
* Remove_Note, \<mod ID\>

### Available compatibility actions
* Add_Compatibility, \<single compatibility\>, \<mod ID\>, \<mod ID\> [, \<mod ID\>, ...]
* Remove_Compatibility, \<single compatibility\>, \<mod ID\>, \<mod ID\> [, \<mod ID\>, ...]

### Available mod group actions
* Add_Group, \<name\>, \<mod ID\>, \<mod ID\> [, \<mod ID\>, ...]
* Add_GroupMember, \<group ID\>, \<mod ID\>
* Remove_Group, \<group ID\>
* Remove_GroupMember, \<group ID\>, \<mod ID\>
*A mod can only be a member of one group, and you cannot remove a mod from one group and add it to another in the same update run.*

### Available author actions
* Add_AuthorID, \<author custom URL\>, \<author ID\>
* Add_AuthorURL, \<author ID\>, \<author custom URL\>
* Add_LastSeen, \<author ID | author custom URL\>, \<date: yyyy-mm-dd\>
* Add_Retired, \<author ID | author custom URL\>
* Remove_AuthorURL, \<author ID\>
* Remove_Retired, \<author ID | author custom URL\>

### Available miscellaneous actions
* Remove_Exclusion, \<mod ID\>, [\<mod ID | DLC appid\>,] \<exclusion type\>
* Add_CatalogGameVersion, \<game version string\>
* Add_CatalogNote, \<note\>
* Remove_CatalogNote


*See https://github.com/Finwickle/ModChecker/blob/dev/ModChecker/DataTypes/Enums.cs for available statuses, compatibilities and DLCs.*  [Todo 0.3] change url