using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CompatibilityReport.CatalogData;
using CompatibilityReport.Util;

// CatalogUpdater uses information gathered by WebCrawler and FileImporter to update the catalog and save this as a new version, with auto generated change notes.

namespace CompatibilityReport.Updater
{
    public static class CatalogUpdater
    {
        // Todo 0.4 move some actions from FileImporter to here; move some actions between here and Catalog

        // Did we run already this session (successful or not)
        private static bool hasRun;

        // Date of the catalog creation, always 'today'. This is used in mod/author change notes.
        private static string catalogDateString;

        // Date of the review update for affected mods. This can be set in the CSV file and is used in mod review dates.
        private static DateTime reviewDate;

        // Stringbuilder to collect new required assets we found
        // Todo 0.4 What to do with the static stringbuilders/dictionaries?
        private static StringBuilder UnknownRequiredAssets;

        // Stringbuilder to gather the combined CSVs, to be saved with the new catalog
        public static StringBuilder CSVCombined;

        // Change notes, in separate parts and combined
        private static StringBuilder changeNotesCatalog;
        private static StringBuilder changeNotesNewMods;
        private static StringBuilder changeNotesNewGroups;
        private static StringBuilder changeNotesNewCompatibilities;
        private static StringBuilder changeNotesNewAuthors;
        private static Dictionary<ulong, string> changeNotesUpdatedMods;
        private static Dictionary<ulong, string> changeNotesUpdatedAuthorsByID;
        private static Dictionary<string, string> changeNotesUpdatedAuthorsByURL;
        private static StringBuilder changeNotesRemovedMods;
        private static StringBuilder changeNotesRemovedGroups;
        private static StringBuilder changeNotesRemovedCompatibilities;
        private static string changeNotes;


        // Update the catalog with the found information; returns the partial path of the new catalog
        public static void Start()
        {
            // Todo 0.7 Read updater settings file
            if (hasRun || !ModSettings.UpdaterEnabled)
            {
                return;
            }

            Logger.Log("Catalog Updater started. See separate logfile for details.");

            Catalog catalog = Catalog.Load();

            // Init the catalog. If we can't, then create a first catalog if it doesn't exist yet.
            if (catalog == null)
            {
                FirstCatalog.Create();
                catalog = Catalog.Load();

                if (catalog == null)
                {
                    return;
                }
            }

            hasRun = true;

            Logger.UpdaterLog($"Catalog Updater started. { ModSettings.ModName } version { ModSettings.FullVersion }. " +
                $"Game version { Toolkit.ConvertGameVersionToString(Toolkit.CurrentGameVersion()) }. " +
                $"Current catalog version { catalog.VersionString() }, created on { catalog.UpdateDate:D}, { catalog.UpdateDate:t}.");

            // Increase the catalog version and update date
            catalog.NewVersion(DateTime.Now);

            catalogDateString = Toolkit.DateString(catalog.UpdateDate.Date);

            EmptyStringbuildersAndDictionaries();

            if (ModSettings.WebCrawlerEnabled)
            {
                WebCrawler.Start(catalog);
            }
            
            FileImporter.Start(catalog);

            UpdateAuthorRetirement(catalog);

            UpdateCompatibilityModNames(catalog);

            // Set a special catalog note for version 2, and reset it again for version 3
            if (catalog.Version == 2 && catalog.Note == ModSettings.FirstCatalogNote)
            {
                SetNote(catalog, ModSettings.SecondCatalogNote);
            }
            else if (catalog.Version == 3 && catalog.Note == ModSettings.SecondCatalogNote)
            {
                SetNote(catalog, "");
            }

            // Log a CSV action for required assets that are missing in the catalog
            if (UnknownRequiredAssets.Length > 0)
            {
                Logger.UpdaterLog("CSV action for adding assets to the catalog (after verification): Add_RequiredAssets" + UnknownRequiredAssets.ToString());
            }

            // Run the DataDumper
            DataDumper.Start(catalog);

            // Only continue with catalog update if we found any changes to update the catalog (ignoring the pure catalog changes)
            if (changeNotesNewMods.Length + changeNotesNewGroups.Length + changeNotesNewCompatibilities.Length + changeNotesNewAuthors.Length + 
                changeNotesUpdatedMods.Count + changeNotesUpdatedAuthorsByID.Count + changeNotesUpdatedAuthorsByURL.Count + 
                changeNotesRemovedMods.Length + changeNotesRemovedGroups.Length + changeNotesRemovedCompatibilities.Length == 0)
            {
                Logger.UpdaterLog("No changes or new additions found. No new catalog created.");
            }
            else
            {
                UpdateChangeNotes(catalog);

                string partialPath = Path.Combine(ModSettings.UpdaterPath, $"{ ModSettings.InternalName }_Catalog_v{ catalog.VersionString() }");

                // Save the new catalog
                if (catalog.Save(partialPath + ".xml"))
                {
                    // Save change notes, in the same folder as the new catalog
                    Toolkit.SaveToFile(changeNotes.ToString(), partialPath + "_ChangeNotes.txt");

                    // Save the combined CSVs, in the same folder as the new catalog
                    Toolkit.SaveToFile(CSVCombined.ToString(), partialPath + "_Imports.csv.txt");

                    Logger.UpdaterLog($"New catalog { catalog.VersionString() } created and change notes saved.");

                    // Copy the updater logfile to the same folder as the new catalog
                    Toolkit.CopyFile(ModSettings.UpdaterLogfileFullPath, partialPath + "_Updater.log");
                }
                else
                {
                    Logger.UpdaterLog("Could not save the new catalog. All updates were lost.", Logger.Error);
                }
            }

            // Empty the dictionaries and stringbuilders to free memory
            EmptyStringbuildersAndDictionaries();

            Logger.UpdaterLog("Catalog Updater has finished.");

            Logger.Log("Catalog Updater has finished.\n");
        }


        // Set all static StringBuilders, Strings and Dictionaries to the empty state
        private static void EmptyStringbuildersAndDictionaries()
        {
            UnknownRequiredAssets = new StringBuilder();

            CSVCombined = new StringBuilder();

            changeNotesCatalog = new StringBuilder();
            changeNotesNewMods = new StringBuilder();
            changeNotesNewGroups = new StringBuilder();
            changeNotesNewCompatibilities = new StringBuilder();
            changeNotesNewAuthors = new StringBuilder();
            changeNotesUpdatedMods = new Dictionary<ulong, string>();
            changeNotesUpdatedAuthorsByID = new Dictionary<ulong, string>();
            changeNotesUpdatedAuthorsByURL = new Dictionary<string, string>();
            changeNotesRemovedMods = new StringBuilder();
            changeNotesRemovedGroups = new StringBuilder();
            changeNotesRemovedCompatibilities = new StringBuilder();
            changeNotes = "";
        }


        // Update change notes in the mod and author change note fields, and combine the change notes for the change notes file
        private static void UpdateChangeNotes(Catalog catalog)
        {
            StringBuilder changeNotesUpdatedModsCombined = new StringBuilder();

            StringBuilder changeNotesUpdatedAuthorsCombined = new StringBuilder();

            foreach (KeyValuePair<ulong, string> modNotes in changeNotesUpdatedMods)
            {
                if (!string.IsNullOrEmpty(modNotes.Value))
                {
                    string cleanedChangeNote = modNotes.Value.Substring(2);

                    catalog.GetMod(modNotes.Key).AddChangeNote($"{ catalogDateString }: { cleanedChangeNote }");

                    changeNotesUpdatedModsCombined.AppendLine($"Mod { catalog.GetMod(modNotes.Key).ToString() }: " +
                        $"{ cleanedChangeNote }");
                }
            }

            foreach (KeyValuePair<ulong, string> authorNotes in changeNotesUpdatedAuthorsByID)
            {
                string cleanedChangeNote = authorNotes.Value.Substring(2);

                catalog.GetAuthor(authorNotes.Key, "").AddChangeNote($"{ catalogDateString }: { cleanedChangeNote }");

                changeNotesUpdatedAuthorsCombined.AppendLine($"Author { catalog.GetAuthor(authorNotes.Key, "").ToString() }: " +
                    $"{ cleanedChangeNote }");
            }

            foreach (KeyValuePair<string, string> authorNotes in changeNotesUpdatedAuthorsByURL)
            {
                string cleanedChangeNote = authorNotes.Value.Substring(2);

                catalog.GetAuthor(0, authorNotes.Key).AddChangeNote($"{ catalogDateString }: { cleanedChangeNote }");

                changeNotesUpdatedAuthorsCombined.AppendLine($"Author { catalog.GetAuthor(0, authorNotes.Key).ToString() }: " +
                    $"{ cleanedChangeNote }");
            }

            // Combine the total change notes
            changeNotes = $"Change Notes for Catalog { catalog.VersionString() }\n" +
                "-------------------------------\n" +
                $"{ catalog.UpdateDate:D}, { catalog.UpdateDate:t}\n" +
                "These change notes were automatically created by the updater process.\n" +
                "\n" +
                (changeNotesCatalog.Length == 0 ? "" :
                    "*** CATALOG CHANGES: ***\n" +
                    changeNotesCatalog.ToString() +
                    "\n") +
                (changeNotesNewMods.Length + changeNotesNewGroups.Length + changeNotesNewAuthors.Length == 0 ? "" :
                    "*** ADDED: ***\n" +
                    changeNotesNewMods.ToString() +
                    changeNotesNewGroups.ToString() +
                    changeNotesNewCompatibilities.ToString() +
                    changeNotesNewAuthors.ToString() +
                    "\n") +
                (changeNotesUpdatedMods.Count + changeNotesUpdatedAuthorsByID.Count + changeNotesUpdatedAuthorsByURL.Count == 0 ? "" :
                    "*** UPDATED: ***\n" +
                    changeNotesUpdatedModsCombined.ToString() +
                    changeNotesUpdatedAuthorsCombined.ToString() +
                    "\n") +
                (changeNotesRemovedMods.Length + changeNotesRemovedGroups.Length + changeNotesRemovedCompatibilities.Length == 0 ? "" :
                    "*** REMOVED: ***\n" +
                    changeNotesRemovedMods.ToString() +
                    changeNotesRemovedGroups.ToString() +
                    changeNotesRemovedCompatibilities.ToString());
        }


        // Set the review date
        public static string SetReviewDate(DateTime newDate)
        {
            if (newDate == default)
            {
                return "Invalid Date.";
            }

            reviewDate = newDate;

            return "";
        }


        // Set a new note for the catalog
        public static void SetNote(Catalog catalog, string newCatalogNote)
        {
            string change = string.IsNullOrEmpty(newCatalogNote) ? "removed" : string.IsNullOrEmpty(catalog.Note) ? "added" : "changed";

            catalog.Update(note: newCatalogNote);

            AddCatalogChangeNote($"Catalog note { change }.");
        }


        // Set a new header text for the catalog
        public static void SetHeaderText(Catalog catalog, string text)
        {
            string change = string.IsNullOrEmpty(text) ? "removed" : string.IsNullOrEmpty(catalog.ReportHeaderText) ? "added" : "changed";

            catalog.Update(reportHeaderText: text);

            AddCatalogChangeNote($"Catalog header text { change }.");
        }


        // Set a new footer text for the catalog
        public static void SetFooterText(Catalog catalog, string text)
        {
            string change = string.IsNullOrEmpty(text) ? "removed" : string.IsNullOrEmpty(catalog.ReportFooterText) ? "added" : "changed";
            
            catalog.Update(reportFooterText: text);

            AddCatalogChangeNote($"Catalog footer text { change }.");
        }


        // Add a mod. A review date is not set, that is only done on UpdateMod()
        public static Mod AddMod(Catalog catalog, ulong steamID, string name, bool incompatible = false, bool unlisted = false, bool removed = false)
        {
            // New mod. Log an empty mod name, which might be an error, although there is a Steam Workshop mod without a name (ofcourse there is)
            if (name == "")
            {
                Logger.UpdaterLog($"Mod name not found for { steamID }. This could be an actual unnamed mod, or a Steam error.", Logger.Warning);
            }
                
            Mod newMod = catalog.AddMod(steamID);

            newMod.Update(name: name);
            newMod.AddChangeNote($"{ catalogDateString }: added");

            string modType = "Mod";

            // Add incompatible status if needed
            if (incompatible)
            {
                newMod.Update(stability: Enums.Stability.IncompatibleAccordingToWorkshop);

                modType = "Incompatible mod";
            }

            // Add removed or unlisted status if needed
            if (unlisted || removed)
            {
                newMod.Statuses.Add(unlisted ? Enums.Status.UnlistedInWorkshop : Enums.Status.RemovedFromWorkshop);

                modType = (unlisted ? "Unlisted " : "Removed ") + modType.ToLower();
            }

            changeNotesNewMods.AppendLine($"{ modType } added: { newMod.ToString() }");

            return newMod;
        }


        // Update a mod with newly found information, including exclusions
        public static void UpdateMod(Catalog catalog, 
                                     Mod catalogMod,
                                     string name = null,
                                     DateTime published = default,
                                     DateTime updated = default,
                                     ulong authorID = 0,
                                     string authorUrl = null,
                                     string sourceURL = null,
                                     string compatibleGameVersionString = null,
                                     Enums.Stability stability = default,
                                     string stabilityNote = null,
                                     string genericNote = null,
                                     bool alwaysUpdateReviewDate = false,
                                     bool updatedByWebCrawler = false)
        {
            if (catalogMod == null)
            {
                return;
            }

            // Set the change note for all changed values
            string addedChangeNote =
                (name == null || name == catalogMod.Name ? "" : ", mod name changed") +
                (updated == default || updated == catalogMod.Updated || catalogMod.AddedThisSession ? "" : ", new update") +
                (authorID == 0 || authorID == catalogMod.AuthorID || catalogMod.AuthorID != 0 || catalogMod.AddedThisSession ? "" : ", author ID added") +
                (authorUrl == null || authorUrl == catalogMod.AuthorUrl || catalogMod.AddedThisSession ? "" : ", author URL") +
                (sourceURL == null || sourceURL == catalogMod.SourceUrl ? "" : ", source URL") +
                (compatibleGameVersionString == null || compatibleGameVersionString == catalogMod.CompatibleGameVersionString ? "" : ", compatible game version") +
                (stability == default || stability == catalogMod.Stability ? "" : ", stability") +
                // Todo 0.4 Mention stability note only if stability is unchanged
                (stabilityNote == null || stabilityNote == catalogMod.StabilityNote ? "" : ", stability note") +
                (genericNote == null || genericNote == catalogMod.GenericNote ? "" : ", generic note");

            AddUpdatedModChangeNote(catalogMod, addedChangeNote);

            // Set the update date
            DateTime modReviewDate = (!string.IsNullOrEmpty(addedChangeNote) || alwaysUpdateReviewDate) && !updatedByWebCrawler ? reviewDate : default;
            DateTime modAutoReviewDate = (!string.IsNullOrEmpty(addedChangeNote) || alwaysUpdateReviewDate) && updatedByWebCrawler ? reviewDate : default;

            // Update exclusions on certain imported changes.
            if (!updatedByWebCrawler && sourceURL != null && sourceURL != catalogMod.SourceUrl)
            {
                // Add exclusion on new or changed URL, and swap exclusion on removal.
                catalogMod.UpdateExclusions(exclusionForSourceUrl: sourceURL != "" || !catalogMod.ExclusionForSourceUrl);
            }

            if (!updatedByWebCrawler && compatibleGameVersionString != null && compatibleGameVersionString != catalogMod.CompatibleGameVersionString)
            {
                // Add exclusion on new or changed game version, and remove the exclusion on removal of the game version.
                catalogMod.UpdateExclusions(exclusionForGameVersion: compatibleGameVersionString != "");
            }

            // Update the mod
            catalogMod.Update(name, published, updated, authorID, authorUrl, sourceURL, compatibleGameVersionString, stability, stabilityNote, genericNote, 
                reviewDate: modReviewDate, autoReviewDate: modAutoReviewDate);

            // Update the authors last seen date if the mod had a new update
            Author modAuthor = catalog.GetAuthor(catalogMod.AuthorID, catalogMod.AuthorUrl);

            if (modAuthor != null && catalogMod.Updated > modAuthor.LastSeen)
            {
                UpdateAuthor(modAuthor, lastSeen: catalogMod.Updated);
            }
        }


        // Add a group
        public static void AddGroup(Catalog catalog, string groupName, List<ulong> groupMembers)
        {
            Group newGroup = catalog.AddGroup(groupName);

            if (newGroup != null)
            {
                // Add group members separately to get change notes on all group members
                foreach (ulong groupMember in groupMembers)
                {
                    AddGroupMember(catalog, newGroup, groupMember);
                }

                changeNotesNewGroups.AppendLine($"Group added: { newGroup.ToString() }");
            }
        }


        // Remove a group
        public static bool RemoveGroup(Catalog catalog, Group oldGroup)
        {
            // Remove the group from all required mod lists
            foreach (Mod catalogMod in catalog.Mods)
            {
                catalogMod.RequiredMods.Remove(oldGroup.GroupID);
            }

            // Todo 0.4 Needs logic to check for groups used as recommendation.

            bool success = catalog.RemoveGroup(oldGroup);

            if (success)
            {
                changeNotesRemovedGroups.AppendLine($"Group removed: { oldGroup.ToString() }");

                // Remove group members to get change notes on all former group members
                foreach (ulong groupMember in oldGroup.GroupMembers)
                {
                    AddUpdatedModChangeNote(catalog.GetMod(groupMember), $"removed from { oldGroup.ToString() }");
                }
            }

            return success;
        }


        // Add a group member
        public static void AddGroupMember(Catalog catalog, Group group, ulong groupMember)
        {
            group.GroupMembers.Add(groupMember);

            AddGroupAsRequiredMod(catalog, groupMember);

            AddUpdatedModChangeNote(catalog.GetMod(groupMember), $"added to { group.ToString() }");
        }


        // Remove a group member
        public static bool RemoveGroupMember(Catalog catalog, Group group, ulong groupMember)
        {
            if (!group.GroupMembers.Remove(groupMember))
            {
                return false;
            }

            // Get all mods that have this former groupmember as required mod
            List<Mod> modList = catalog.Mods.FindAll(x => x.RequiredMods.Contains(groupMember));

            // Remove the group as required mod if no other group member is required
            foreach (Mod mod in modList)
            {
                bool removeRequiredGroup = true;

                foreach (ulong otherGroupMember in group.GroupMembers)
                {
                    removeRequiredGroup = removeRequiredGroup && !mod.RequiredMods.Contains(otherGroupMember);
                }

                if (removeRequiredGroup)
                {
                    mod.RequiredMods.Remove(group.GroupID);

                    Logger.UpdaterLog($"Removed { group.ToString() } as required mod for { mod.ToString() }.");
                }
            }

            // Todo 0.4 Needs logic for groups as recommendations.

            AddUpdatedModChangeNote(catalog.GetMod(groupMember), $"removed from { group.ToString() }");

            return true;
        }


        // Add a group as required mod for all mods that have the given group member as required mod
        public static void AddGroupAsRequiredMod(Catalog catalog, ulong requiredModID)
        {
            Group requiredGroup = catalog.GetThisModsGroup(requiredModID);

            // Exit if this mod is not in a group
            if (requiredGroup == default)
            {
                return;
            }

            // Get all mods that have this required mod
            List<Mod> modList = catalog.Mods.FindAll(x => x.RequiredMods.Contains(requiredModID));

            foreach (Mod mod in modList)
            {
                // Add the group ID
                if (!mod.RequiredMods.Contains(requiredGroup.GroupID))
                {
                    mod.RequiredMods.Add(requiredGroup.GroupID);

                    Logger.UpdaterLog($"Added { requiredGroup.ToString() } as required mod for { mod.ToString() }.");
                }
            }
        }


        // Add a compatibility
        public static void AddCompatibility(Catalog catalog, ulong firstModID, ulong secondModID, Enums.CompatibilityStatus compatibilityStatus, string compatibilityNote)
        {
            catalog.AddCompatibility(firstModID, secondModID, compatibilityStatus, compatibilityNote);

            changeNotesNewCompatibilities.AppendLine($"Compatibility added between { firstModID, 10 } and { secondModID, 10 }: { compatibilityStatus }" +
                (string.IsNullOrEmpty(compatibilityNote) ? "" : ", " + compatibilityNote));
        }


        // Remove a compatibility
        public static bool RemoveCompatibility(Catalog catalog, Compatibility catalogCompatibility)
        {
            if (!catalog.Compatibilities.Remove(catalogCompatibility))
            {
                return false;
            }

            changeNotesRemovedCompatibilities.AppendLine($"Compatibility removed between { catalogCompatibility.FirstSteamID } and " +
                $"{ catalogCompatibility.SecondSteamID }: \"{ catalogCompatibility.Status }\"");

            return true;
        }


        // Add an author
        public static Author AddAuthor(Catalog catalog, ulong authorID, string authorUrl, string authorName)
        {
            // Log if the author name is empty or equal to the author ID. Could be an error, although some authors have their ID as name (ofcourse they do)
            if (string.IsNullOrEmpty(authorName))
            {
                Logger.UpdaterLog($"Author found without a name: { (authorID == 0 ? "Custom URL " + authorUrl : "Steam ID " + authorID.ToString()) }.", Logger.Error);
            }
            else if (authorName == authorID.ToString() && authorID != 0)
            {
                Logger.UpdaterLog($"Author found with Steam ID as name: { authorID }. Some authors do this, but it could also be a Steam error.", Logger.Warning);
            }

            // Log if we have two authors with the same name, which could be an existing author we missed when a Custom URL has changed
            Author namesakeAuthor = catalog.Authors.Find(x => x.Name == authorName);
                
            if (namesakeAuthor != default)
            {
                string authors = (authorID == 0 ? authorUrl : authorID.ToString()) + " and " + 
                    (namesakeAuthor.SteamID == 0 ? namesakeAuthor.CustomUrl : namesakeAuthor.SteamID.ToString());

                Logger.UpdaterLog($"Found two authors with the name \"{ authorName }\": { authors }. This could be a coincidence or an error.", Logger.Warning);
            }

            Author catalogAuthor = catalog.AddAuthor(authorID, authorUrl, authorName);
            catalogAuthor.AddChangeNote($"{ catalogDateString }: added");

            changeNotesNewAuthors.AppendLine($"Author added: { catalogAuthor.ToString() }");

            return catalogAuthor;
        }


        // Update an author with newly found information, including exclusions
        public static void UpdateAuthor(Author catalogAuthor,
                                          ulong authorID = 0,
                                          string authorUrl = null,
                                          string name = null,
                                          DateTime lastSeen = default,
                                          bool? retired = null)
        {
            if (catalogAuthor == null)
            {
                return;
            }

            // Set the change note for all changed values
            // Todo 0.4 Causes duplicates in change notes, especially for last seen
            string addedChangeNote =
                (authorID == 0 || authorID == catalogAuthor.SteamID || catalogAuthor.SteamID != 0 ? "" : ", Steam ID added") +
                (authorUrl == null || authorUrl == catalogAuthor.CustomUrl ? "" : ", Custom URL") +
                (name == null || name == catalogAuthor.Name ? "" : ", name") +
                (lastSeen == default || lastSeen == catalogAuthor.LastSeen || catalogAuthor.AddedThisSession ? "" : ", last seen date") +
                (retired == null || retired == catalogAuthor.Retired ? "" : $", { (retired == true ? "now" : "no longer") } retired");

            AddUpdatedAuthorChangeNote(catalogAuthor, addedChangeNote);

            // Update the author
            catalogAuthor.Update(authorID, authorUrl, name, lastSeen, retired, exclusionForRetired: catalogAuthor.ExclusionForRetired || retired == true);

            // Todo 0.4 Not implemented yet: distribute new ID or changed URL to all mods; check for ID as name (see AddOrGetAuthor())
        }


        // Retire authors that are now eligible due to last seen date, and authors that don't have a mod in the Steam Workshop anymore
        private static void UpdateAuthorRetirement(Catalog catalog)
        {
            // Make a temporary lists of all authors that have at least one Steam Workshop mod.
            List<ulong> ActiveAuthorIDs = new List<ulong>();

            List<string> ActiveAuthorURLs = new List<string>();

            foreach (Mod catalogMod in catalog.Mods)
            {
                if (!catalogMod.Statuses.Contains(Enums.Status.RemovedFromWorkshop))
                {
                    if (catalogMod.AuthorID != 0 && !ActiveAuthorIDs.Contains(catalogMod.AuthorID))
                    {
                        ActiveAuthorIDs.Add(catalogMod.AuthorID);
                    }

                    if (!string.IsNullOrEmpty(catalogMod.AuthorUrl) && !ActiveAuthorURLs.Contains(catalogMod.AuthorUrl))
                    {
                        ActiveAuthorURLs.Add(catalogMod.AuthorUrl);
                    }
                }
            }

            // Check and update retirement for all authors
            foreach (Author catalogAuthor in catalog.Authors)
            {
                // Set exclusion for early retirement and remove it otherwise
                if (catalogAuthor.Retired && catalogAuthor.LastSeen.AddMonths(ModSettings.MonthsOfInactivityToRetireAuthor) >= DateTime.Today)
                {
                    catalogAuthor.Update(exclusionForRetired: true);
                }
                else
                {
                    catalogAuthor.Update(exclusionForRetired: false);
                }

                // Set retirement
                if (!ActiveAuthorIDs.Contains(catalogAuthor.SteamID) && !ActiveAuthorURLs.Contains(catalogAuthor.CustomUrl))
                {
                    // Authors without a mod in the Steam Workshop
                    if (!catalogAuthor.Retired)
                    {
                        // Only update if they weren't set to retired yet
                        AddUpdatedAuthorChangeNote(catalogAuthor, "no longer has mods on the Steam Workshop");

                        UpdateAuthor(catalogAuthor, retired: true);
                    }
                }

                else if (catalogAuthor.LastSeen != default && catalogAuthor.LastSeen.AddMonths(ModSettings.MonthsOfInactivityToRetireAuthor) < DateTime.Today)
                {
                    // Authors that are retired based on last seen date
                    UpdateAuthor(catalogAuthor, retired: true);

                    catalogAuthor.Update(exclusionForRetired: false);
                }

                else if (!catalogAuthor.ExclusionForRetired)
                {
                    // Authors that have mods in the Steam Workshop, and are recently enough seen, and don't have an exclusion for retired
                    UpdateAuthor(catalogAuthor, retired: false);
                }
            }
        }


        // Update the mod names in all compatilibities.
        private static void UpdateCompatibilityModNames(Catalog catalog)
        {
            foreach (Compatibility compatibility in catalog.Compatibilities)
            {
                compatibility.UpdateModNames(catalog.GetMod(compatibility.FirstSteamID).Name, catalog.GetMod(compatibility.SecondSteamID).Name);
            }
        }


        // Add a mod status, including exclusions and removing conflicting statuses.
        public static void AddStatus(Mod catalogMod, Enums.Status status, bool updatedByWebCrawler = false)
        {
            if (status == default || catalogMod.Statuses.Contains(status))
            {
                return;
            }

            catalogMod.Statuses.Add(status);

            AddUpdatedModChangeNote(catalogMod, $"{ status } added");

            // Remove conflicting statuses, and change some exclusions
            if (status == Enums.Status.UnlistedInWorkshop)
            {
                RemoveStatus(catalogMod, Enums.Status.RemovedFromWorkshop);
            }
            else if (status == Enums.Status.RemovedFromWorkshop)
            {
                RemoveStatus(catalogMod, Enums.Status.UnlistedInWorkshop);
                RemoveStatus(catalogMod, Enums.Status.NoCommentSection);
                RemoveStatus(catalogMod, Enums.Status.NoDescription);
                catalogMod.UpdateExclusions(exclusionForNoDescription: false);
            }
            else if (status == Enums.Status.NoDescription && !updatedByWebCrawler)
            {
                // Exclusion is only needed if this status was set by the FileImporter
                catalogMod.UpdateExclusions(exclusionForNoDescription: true);
            }
            else if (status == Enums.Status.NoLongerNeeded)
            {
                RemoveStatus(catalogMod, Enums.Status.Deprecated);
                RemoveStatus(catalogMod, Enums.Status.Abandoned);
            }
            else if (status == Enums.Status.Deprecated)
            {
                RemoveStatus(catalogMod, Enums.Status.NoLongerNeeded);
                RemoveStatus(catalogMod, Enums.Status.Abandoned);
            }
            else if (status == Enums.Status.Abandoned)
            {
                RemoveStatus(catalogMod, Enums.Status.NoLongerNeeded);
                RemoveStatus(catalogMod, Enums.Status.Deprecated);
            }
            else if (status == Enums.Status.MusicCopyrighted)
            {
                RemoveStatus(catalogMod, Enums.Status.MusicCopyrightFree);
                RemoveStatus(catalogMod, Enums.Status.MusicCopyrightUnknown);
            }
            else if (status == Enums.Status.MusicCopyrightFree)
            {
                RemoveStatus(catalogMod, Enums.Status.MusicCopyrighted);
                RemoveStatus(catalogMod, Enums.Status.MusicCopyrightUnknown);
            }
            else if (status == Enums.Status.MusicCopyrightUnknown)
            {
                RemoveStatus(catalogMod, Enums.Status.MusicCopyrighted);
                RemoveStatus(catalogMod, Enums.Status.MusicCopyrightFree);
            }
        }


        // Remove a mod status
        public static bool RemoveStatus(Mod catalogMod, Enums.Status status, bool updatedByWebCrawler = false)
        {
            bool success = catalogMod.Statuses.Remove(status);

            if (success)
            {
                AddUpdatedModChangeNote(catalogMod, $"{ status } removed");

                // Add or remove exclusion for some statuses
                if (status == Enums.Status.NoDescription && !updatedByWebCrawler)
                {
                    // Only if the status is removed by the FileImporter: if there was an exclusion, remove it, otherwise add it.
                    catalogMod.UpdateExclusions(exclusionForNoDescription: !catalogMod.ExclusionForNoDescription);
                }
            }

            return success;
        }


        // Add a required DLC
        public static void AddRequiredDLC(Mod catalogMod, Enums.Dlc requiredDLC)
        {
            if (requiredDLC != default && !catalogMod.RequiredDlcs.Contains(requiredDLC))
            {
                catalogMod.RequiredDlcs.Add(requiredDLC);

                catalogMod.AddExclusion(requiredDLC);

                AddUpdatedModChangeNote(catalogMod, $"required DLC { Toolkit.ConvertDlcToString(requiredDLC) } added");
            }
        }


        // Remove a required DLC
        public static void RemoveRequiredDLC(Mod catalogMod, Enums.Dlc requiredDLC)
        {
            if (catalogMod.RequiredDlcs.Remove(requiredDLC))
            {
                catalogMod.ExclusionForRequiredDlc.Remove(requiredDLC);

                AddUpdatedModChangeNote(catalogMod, $"required DLC { Toolkit.ConvertDlcToString(requiredDLC) } removed");
            }
        }


        // Todo 0.4 If Steam ID added to one of required/successor/alternative/recommendation, remove it from the other three?

        // Add a required mod, including exclusion, required group and change notes
        public static void AddRequiredMod(Catalog catalog, Mod catalogMod, ulong requiredID, bool updatedByWebCrawler)
        {
            if (catalog.IsValidID(requiredID, allowGroup: true) && !catalogMod.RequiredMods.Contains(requiredID))
            {
                catalogMod.RequiredMods.Add(requiredID);

                if (catalog.GetGroup(requiredID) != null)
                {
                    // requiredID is a group
                    AddUpdatedModChangeNote(catalogMod, $"required group { requiredID } added");
                }
                else
                {
                    // requiredID is a mod
                    AddUpdatedModChangeNote(catalogMod, $"required mod { requiredID } added");

                    if (!updatedByWebCrawler)
                    {
                        catalogMod.AddExclusion(requiredID);
                    }

                    if (catalog.IsGroupMember(requiredID))
                    {
                        // Also add the group that requiredID is a member of
                        AddRequiredMod(catalog, catalogMod, catalog.GetThisModsGroup(requiredID).GroupID, updatedByWebCrawler);
                    }
                }
            }

            // If the requiredID is not a known ID, it's probably an asset.
            // Todo 0.4 This still gives warnings if the asset is added by add_asset in CSV
            else if (catalog.IsValidID(requiredID, shouldExist: false) && !catalog.RequiredAssets.Contains(requiredID))
            {
                UnknownRequiredAssets.Append($", { requiredID }");
                
                Logger.UpdaterLog($"Required item not found, probably an asset: { Toolkit.GetWorkshopUrl(requiredID) } (for { catalogMod.ToString() }).");
            }
        }


        // Remove a required mod from a mod, including exclusion, group and change notes
        public static void RemoveRequiredMod(Catalog catalog, Mod catalogMod, ulong requiredID)
        {
            if (catalogMod.RequiredMods.Remove(requiredID))
            {
                if (catalog.GetGroup(requiredID) != null)
                {
                    // requiredID is a group
                    AddUpdatedModChangeNote(catalogMod, $"required Group { requiredID } removed");
                }
                else
                {
                    // requiredID is a mod
                    AddUpdatedModChangeNote(catalogMod, $"required Mod { requiredID } removed");

                    // If an exclusion exists (it was added by FileImporter) remove it, otherwise (added by WebCrawler) add it to prevent the required mod from returning
                    if (catalogMod.ExclusionForRequiredMods.Contains(requiredID))
                    {
                        catalogMod.ExclusionForRequiredMods.Remove(requiredID);
                    }
                    else
                    {
                        catalogMod.ExclusionForRequiredMods.Add(requiredID);
                    }

                    Group group = catalog.GetThisModsGroup(requiredID);

                    if (group != null)
                    {
                        // Check if none of the other group members is a required mod, so we can remove the group as required mod
                        bool canRemoveGroup = true;

                        foreach (ulong groupMember in group.GroupMembers)
                        {
                            canRemoveGroup = canRemoveGroup && !catalogMod.RequiredMods.Contains(groupMember);
                        }

                        if (canRemoveGroup)
                        {
                            RemoveRequiredMod(catalog, catalogMod, group.GroupID);
                        }
                    }
                }
            }
        }


        // Add a successor, including change notes
        public static void AddSuccessor(Mod catalogMod, ulong successorID)
        {
            if (!catalogMod.Successors.Contains(successorID))
            {
                catalogMod.Successors.Add(successorID);

                AddUpdatedModChangeNote(catalogMod, $"successor { successorID } added");
            }
        }


        // Remove a successor, including change notes
        public static void RemoveSuccessor(Mod catalogMod, ulong successorID)
        {
            if (catalogMod.Successors.Remove(successorID))
            {
                AddUpdatedModChangeNote(catalogMod, $"successor { successorID } removed");
            }
        }


        // Add an alternative, including change notes
        public static void AddAlternative(Mod catalogMod, ulong alternativeID)
        {
            if (!catalogMod.Alternatives.Contains(alternativeID))
            {
                catalogMod.Alternatives.Add(alternativeID);

                AddUpdatedModChangeNote(catalogMod, $"alternative { alternativeID } added");
            }
        }


        // Remove an alternative, including change notes
        public static void RemoveAlternative(Mod catalogMod, ulong alternativeID)
        {
            if (catalogMod.Alternatives.Remove(alternativeID))
            {
                AddUpdatedModChangeNote(catalogMod, $"alternative { alternativeID } removed");
            }
        }


        // Add a recommendation, including change notes
        public static void AddRecommendation(Mod catalogMod, ulong recommendationID)
        {
            if (!catalogMod.Recommendations.Contains(recommendationID))
            {
                catalogMod.Recommendations.Add(recommendationID);

                AddUpdatedModChangeNote(catalogMod, $"successor { recommendationID } added");
            }
        }


        // Remove a recommendation, including change notes
        public static void RemoveRecommendation(Mod catalogMod, ulong recommendationID)
        {
            if (catalogMod.Recommendations.Remove(recommendationID))
            {
                AddUpdatedModChangeNote(catalogMod, $"successor { recommendationID } removed");
            }
        }


        // Add a change note for an updated mod.
        private static void AddUpdatedModChangeNote(Mod catalogMod, string extraChangeNote)
        {
            if (catalogMod == null || string.IsNullOrEmpty(extraChangeNote))
            {
                return;
            }

            // Add a separator if needed. The separator at the start of the final change note will be stripped before it is written.
            if (extraChangeNote[0] != ',')
            {
                extraChangeNote = ", " + extraChangeNote;
            }

            // Add the new change note to the dictionary
            if (changeNotesUpdatedMods.ContainsKey(catalogMod.SteamID))
            {
                changeNotesUpdatedMods[catalogMod.SteamID] += extraChangeNote;
            }
            else
            {
                changeNotesUpdatedMods.Add(catalogMod.SteamID, extraChangeNote);
            }
        }


        // Add a change note for a removed mod
        public static void AddRemovedModChangeNote(Mod catalogMod)
        {
            changeNotesRemovedMods.AppendLine($"Mod removed: { catalogMod.ToString() }");
        }
        
        
        // Add a change note for an updated author.
        private static void AddUpdatedAuthorChangeNote(Author catalogAuthor, string extraChangeNote)
        {
            if (catalogAuthor == null || string.IsNullOrEmpty(extraChangeNote))
            {
                return;
            }

            // Add a separator if needed. The separator at the start of the final change note will be stripped before it is written.
            if (extraChangeNote[0] != ',')
            {
                extraChangeNote = ", " + extraChangeNote;
            }

            // Add the new change note to the dictionary
            if (catalogAuthor.SteamID != 0)
            {
                if (changeNotesUpdatedAuthorsByID.ContainsKey(catalogAuthor.SteamID))
                {
                    changeNotesUpdatedAuthorsByID[catalogAuthor.SteamID] += extraChangeNote;
                }
                else
                {
                    changeNotesUpdatedAuthorsByID.Add(catalogAuthor.SteamID, extraChangeNote);
                }
            }
            else
            {
                if (changeNotesUpdatedAuthorsByURL.ContainsKey(catalogAuthor.CustomUrl))
                {
                    changeNotesUpdatedAuthorsByURL[catalogAuthor.CustomUrl] += extraChangeNote;
                }
                else
                {
                    changeNotesUpdatedAuthorsByURL.Add(catalogAuthor.CustomUrl, extraChangeNote);
                }
            }
        }


        // Add a change note for a catalog change
        public static void AddCatalogChangeNote(string extraLine)
        {
            changeNotesCatalog.AppendLine(extraLine);
        }
    }
}
