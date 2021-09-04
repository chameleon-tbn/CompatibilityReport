# Compatibility Report - Updater Guide

The Updater only runs when a settings xml file exists in the Updater folder, so it won't run for regular users. The updater has two parts. The WebCrawler crawls the Steam Workshop for mod information, including mod name, author, published/update dates, required DLC/mods, source URL, compatible game version and some statuses (removed from workshop, no description, author retirement). The FileImporter imports data from CSV files, including information that cannot be found by an automated process, like compatibility and suggested successor/alternative mods. It also allows the creation of groups to replace a required mod. The updater creates a new catalog that can be uploaded to the download location, so all mod users get this new catalog on the next game start. If you think about enabling the updater for yourself, mind that the WebCrawler downloads nearly 2000 webpages and takes about 15 minutes on a fast computer with decent internet connection.

The FileImporter gathers its information from CSV files. These should be placed in the Updater folder where the updated catalogs are written as well. Update actions can be bundled in one CSV file or split into multiple files. Multiple files will be read in alphabetical order. Filenames are not relevant, but should end in .csv. After updating, the processed CSV files are renamed to .txt to avoid duplicate additions to the catalog on a next updater run.

Groups are used for mod requirements. A group will replace every group member as required mod, both for current mod requirements in the catalog and for new mods found in the future. For example, a group with both a stable and test version of the same mod, will accept the test version as valid if the stable version is set as required mod. This prevents unjustified 'required mod not found' messages in the report. A mod can only be a member of one group. Groups cannot be used for compatibilities, successors or alternatives. Group IDs are automaticaly assigned.

The lines in the CSV files all start with an action, often followed by a steam ID (for the mod, group or author), often followed by additional data. Some actions will create exclusions in the catalog, to prevent the WebCrawler from overwriting these manual updates. Actions and parameters are not case sensitive. Commas are the only allowed separator and spaces around the separators are ignored. Commas are supported in the mod name, notes and header/footer texts, but not in other parameters.

Lines starting with a '#' are considered comments and will be ignored by the updater. Comments can also be added as an extra parameter at the end of any action other than Add_Mod, Add_Note, Add_Compatibility and most Catalog actions. Use a comma after the last parameter and start the comment with a '#'. Commas in these end-of-line comments are not supported.

### First action to use in any CSV file
* ReviewDate, \<date: yyyy-mm-dd\> 
  * *Used as review update date for any following Mod actions. Can be used multiple times if you want different review dates for different actions. Uses today if omitted.*

### Available mod actions
Parameters enclosed in square brackets are optional. The symbol :zap: means an exclusion will be created.
* Add_Mod, \<mod ID\>, Unlisted | Removed [, \<author ID | author custom URL\> [, \<mod name\>] ]
* Set_SourceURL, \<mod ID\>, \<url\> :zap:
* Set_GameVersion, \<mod ID\>, \<game version string\> :zap: *(will be overruled when a newer game version is found)*
* Add_RequiredDLC, \<mod ID\>, \<DLC string\> :zap:
* Add_RequiredMod, \<mod ID\>, \<required mod ID\> :zap:
* Add_Successor, \<mod ID\>, \<successor mod ID\>
* Add_Alternative, \<mod ID\>, \<alternative mod ID\>
* Add_Recommendation, \<mod ID\>, \<recommended mod ID\>
* Set_Stability, \<mod ID\>, \<stability string\>
* Set_StabilityNote, \<mod ID\>, \<text\>
* Add_Status, \<mod ID\>, \<status string\> :zap: *(exclusion only for NoDescription status)*
* Set_GenericNote, \<mod ID\>, \<text\>
* Update_Review, \<mod ID\> *(updates the review date; use for reviews without changes to the mod itself)*
* Remove_Mod, \<mod ID\> *(only works on mods that are removed from the Workshop)*
* Remove_SourceURL, \<mod ID\> :zap:
* Remove_GameVersion, \<mod ID\> *(only works if an exclusion exists)*
* Remove_RequiredDLC, \<mod ID\>, \<DLC string\> *(only works if an exclusion exists)*
* Remove_RequiredMod, \<mod ID\>, \<required mod ID\> :zap:
* Remove_Successor, \<mod ID\>, \<successor mod ID\>
* Remove_Alternative, \<mod ID\>, \<alternative mod ID\>
* Remove_Recommendation, \<mod ID\>, \<recommended mod ID\>
* Remove_StabilityNote, \<mod ID\>
* Remove_Status, \<mod ID\>, \<status string\>
* Remove_GenericNote, \<mod ID\>
* Remove_Exclusion, \<mod ID\>, \<exclusion category\> [,\<required DLC string | mod ID\>]
  * *Available categories: SourceURL, GameVersion, RequiredDLC, RequiredMod, NoDescription*

### Available compatibility actions (will not change reviewed date for included mods)
* Add_Compatibility, \<first mod ID\>, \<second mod ID\>, \<compatibility status\>[, \<note\>]
* Add_CompatibilitiesForOne, \<first mod ID\>, \<compatibility status\>, \<mod ID\>, \<mod ID\> [, \<mod ID\>, ...]
  * *This will create compatibilities between the first mod and each of the other mods. Not every status works.*
* Add_CompatibilitiesForAll, \<compatibility status\>, \<mod ID\>, \<mod ID\>, \<mod ID\> [, \<mod ID\>, ...]
  * *This will create compatibilities, between each of these mods in pairs (1 with 2, 1 with 3, 2 with 3, etc.). Not every status works.*
* Remove_Compatibility, \<first mod ID\>, \<second mod ID\>, \<compatibility status\>

### Available group actions (will not change reviewed date for included mods)
* Add_Group, \<name\>, \<mod ID\>, \<mod ID\> [, \<mod ID\>, ...]
* Add_GroupMember, \<group ID\>, \<mod ID\>
* Remove_Group, \<group ID\>
* Remove_GroupMember, \<group ID\>, \<mod ID\>
  * *Careful with this! Required mods and Exclusions might become a mess, so you might need some extra actions*

### Available author actions (author ID is much more reliable, and mandatory if the custom URL is a number)
* Add_Author, \<author ID | author custom URL\>, \<author name\> *(only needed for unknown authors of 'removed' mods, will be added as retired)*
* Set_AuthorID, \<author custom URL\>, \<author ID\>
* Set_AuthorURL, \<author ID\>, \<author custom URL\>
* Set_LastSeen, \<author ID | author custom URL\>, \<date: yyyy-mm-dd\> *(should be more recent than newest mod update)*
  * *Author will be assumed retired if not seen for a year*
* Set_Retired, \<author ID | author custom URL\> :zap:
* Remove_AuthorURL, \<author ID\>
* Remove_Retired, \<author ID | author custom URL\> *(only works if added manually before)*

### Available catalog actions
* Set_CatalogGameVersion, \<game version string\>
* Set_CatalogNote, \<text\>
* Set_CatalogHeaderText, \<text\>
* Set_CatalogFooterText, \<text\>
* Remove_CatalogNote
* Remove_CatalogHeaderText
* Remove_CatalogFooterText

### Available miscellaneous actions
* Add_RequiredAssets, \<asset ID\> [, \<asset ID\>, ...]
  * *Only needed to differentiate between a required asset and a required mod that isn't in the catalog*
* Remove_RequiredAssets, \<asset ID\> [, \<asset ID\>, ...]


*See https://github.com/Finwickle/CompatibilityReport/blob/dev/CompatibilityReport/DataTypes/Enums.cs for available stability, status, compatibility and DLC strings.*  [Todo 0.5] change url to main