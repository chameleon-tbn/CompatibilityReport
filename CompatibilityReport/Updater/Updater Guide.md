# Compatibility Report - ManualUpdater Guide

The ManualUpdater updates the catalog with manual changes and additions. It complements the AutoUpdater, which gathers information by crawling the Steam Workshop, including mod name, author, published/update dates, required DLC/mods, source URL, compatible game version and some statuses (removed from workshop, no description, author retirement). The ManualUpdater complements this with information that cannot be found by an automated proces, like compatibility information and suggestions for successor or alternative mods. It also allows the creation of groups to replace a required mod.

The ManualUpdater gathers its information from CSV files. These should be placed in the Updater folder where the updated catalogs are written as well. Update actions can be bundled in one CSV file or split into multiple files. Multiple files will be read in alphabetical order. Filenames are not relevant, but should end in .csv. After updating, the processed CSV files will be combined into one file with the name of the new catalog and ending in '_ManualUpdates.txt'. The processed CSV files are then renamed to .txt to avoid duplicate additions to the catalog on a next updater run.

Groups are used for mod requirements. A group will replace every group member as required mod, both for current mod requirements in the catalog and for mod requirements for new mods found in the future. For example, a group with both a stable and test version of the same mod, will accept the test version as valid if the stable version is set as required mod. This prevents unjustified 'required mod not found' messages in the report. A mod can only be a member of one group, and the ManualUpdater cannot remove a mod from one group and add it to another in the same update run. Groups cannot be used for compatibilities, successors or alternatives. Group IDs are automaticaly assigned.

The lines in the CSV files all start with an action, often followed by a steam ID (for the mod, group or author), often followed by additional data. Some actions will create exclusions in the catalog, to prevent the AutoUpdater from overwriting these manual updates. Actions and parameters are not case sensitive (except for names etc. that appears in reports). Commas are the only allowed separators and spaces around the separators are ignored. Commas in mod name and notes are supported.

Lines starting with a '#' are considered comments and will be ignored by the updater. They will be copied to the combined file. Extra parameters not used on an action will be ignored as well and can be used for comments (except for Add_Mod, Add_Note and Add_CatalogNote).

### Available mod actions
Parameters enclosed in square brackets are optional. The symbol :zap: means an exclusion will be created.
* Add_Mod, \<mod ID\> [, \<author ID | author custom URL\> [, \<mod name\>] ] *(mod will have the 'unlisted' status)*
* Add_ArchiveURL, \<mod ID\>, \<url\>
* Add_SourceURL, \<mod ID\>, \<url\> :zap:
* Add_GameVersion, \<mod ID\>, \<game version string\> :zap: *(exclusion only if Workshop has different game version tag)*
* Add_RequiredDLC, \<mod ID\>, \<DLC string\> :zap:
* Add_RequiredMod, \<mod ID\>, \<required mod or group ID\> :zap:
* Add_Successor, \<mod ID\>, \<successor mod ID\>
* Add_Alternative, \<mod ID\>, \<alternative mod ID\>
* Add_Status, \<mod ID\>, \<status string\> :zap: *(exclusion only for NoDescription and SourceUnavailable status)*
  * *Adding a SourceUnavailable status will remove the SourceURL from the mod*
* Add_Note, \<mod ID\>, \<text\> *(this will add the text to the end of the note, if a note already exists)*
* Add_ReviewDate, \<mod ID\> *(use for reviews without changes to the mod itself)*
* Remove_Mod, \<mod ID\> *(only works on unlisted and removed mods)*
* Remove_ArchiveURL, \<mod ID\>
* Remove_SourceURL, \<mod ID\> :zap:
* Remove_GameVersion, \<mod ID\> *(only works if an exclusion exists)*
* Remove_RequiredDLC, \<mod ID\>, \<DLC string\> *(only works if an exclusion exists)*
* Remove_RequiredMod, \<mod ID\>, \<required mod or group ID\> *(only works if an exclusion exists)*
* Remove_Successor, \<mod ID\>, \<successor mod ID\>
* Remove_Alternative, \<mod ID\>, \<alternative mod ID\>
* Remove_Status, \<mod ID\>, \<status string\>
* Remove_Note, \<mod ID\>

### Available compatibility actions (will not change reviewed date for included mods)
* Add_Compatibility, \<first mod ID\>, \<second mod ID\>, \<compatibility status\>[, \<note\>]
  * *The note will only be mentioned in the report for the first mod*
* Add_CompatibilitiesForOne, \<first mod ID\>, \<compatibility status\>, \<mod ID\>, \<mod ID\> [, \<mod ID\>, ...]
  * *This will create compatibilities between the first mod and each of the other mods*
* Add_CompatibilitiesForAll, \<compatibility status\>, \<mod ID\>, \<mod ID\>, \<mod ID\> [, \<mod ID\>, ...]
  * *This will create many compatibilities, between each of these mods in pairs (1 with 2, 1 with 3, 2 with 3, etc.)*
* Remove_Compatibility, \<first mod ID\>, \<second mod ID\>, \<compatibility status\>

### Available group actions (will not change reviewed date for included mods)
* Add_Group, \<name\>, \<mod ID\>, \<mod ID\> [, \<mod ID\>, ...]
* Add_GroupMember, \<group ID\>, \<mod ID\>
* Remove_Group, \<group ID\>, \<replacement mod ID\>
* Remove_GroupMember, \<group ID\>, \<mod ID\>

### Available author actions (use the author ID if the custom URL is a number)
* Add_Author, \<<author ID | author custom URL\>, \<author name\> *(only need for 'removed' mods)*
* Add_AuthorID, \<author custom URL\>, \<author ID\>
* Add_AuthorURL, \<author ID\>, \<author custom URL\>
* Add_LastSeen, \<author ID | author custom URL\>, \<date: yyyy-mm-dd\>
  * *Adding a last seen date reassesses the retired status*
* Add_Retired, \<author ID | author custom URL\>
* Remove_AuthorURL, \<author ID\>
* Remove_Retired, \<author ID | author custom URL\>

### Available miscellaneous actions
* Remove_Exclusion, \<mod ID\>, [\<mod ID | DLC appid\>,] \<exclusion category\>
* Add_CatalogGameVersion, \<game version string\>
* Add_CatalogNote, \<note\>
* Remove_CatalogNote
* UpdateDate, \<date: yyyy-mm-dd\> 
  * *Used as review update date; uses 'now' if omitted*


*See https://github.com/Finwickle/ModChecker/blob/dev/ModChecker/DataTypes/Enums.cs for available status, compatibility and DLC strings.*  [Todo 0.3] change url to main