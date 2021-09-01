using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CompatibilityReport.DataTypes;
using CompatibilityReport.Util;


// CatalogUpdater uses information gathered by WebCrawler and FileImporter to update the catalog and save this as a new version, with auto generated change notes.


namespace CompatibilityReport.Updater
{
    internal static class CatalogUpdater    // [Todo 0.4] move some actions from FileImporter to here; move some actions between here and Catalog
    {
        // Did we run already this session (successful or not)
        private static bool hasRun;

        private static Catalog ActiveCatalog;

        // Date of the catalog creation, always 'today'. This is used in mod/author change notes.
        private static string catalogDateString;

        // Date of the review update for affected mods. This can be set in the CSV file and is used in mod review dates.
        private static DateTime reviewDate;

        // Stringbuilder to collect new required assets we found
        private static StringBuilder UnknownRequiredAssets;

        // Stringbuilder to gather the combined CSVs, to be saved with the new catalog
        internal static StringBuilder CSVCombined;

        // Change notes, separate parts and combined
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

        
        // Update the active catalog with the found information; returns the partial path of the new catalog
        internal static void Start()
        {
            if (hasRun || !ModSettings.UpdaterEnabled)
            {
                return;
            }

            Logger.Log("Catalog Updater started. See separate logfile for details.");

            ActiveCatalog = Catalog.InitActive();

            // Init the active catalog. If we can't, then create a first catalog if it doesn't exist yet and init the active catalog again
            if (ActiveCatalog == null)
            {
                ActiveCatalog = FirstCatalog.Create();

                if (ActiveCatalog == null)
                {
                    return;
                }
            }

            hasRun = true;

            Logger.UpdaterLog($"Catalog Updater started. { ModSettings.modName } version { ModSettings.fullVersion }. " +
                $"Game version { Toolkit.ConvertGameVersionToString(Toolkit.CurrentGameVersion) }. Current catalog version { ActiveCatalog.VersionString() }.");

            Init();

            if (ModSettings.WebCrawlerEnabled)
            {
                WebCrawler.Start(ActiveCatalog);
            }
            
            FileImporter.Start(ActiveCatalog);

            UpdateAuthorRetirement();

            // Set a special catalog note for version 2, and reset it again for version 3
            if (ActiveCatalog.Version == 2 && ActiveCatalog.Note == ModSettings.firstCatalogNote)
            {
                SetNote(ModSettings.secondCatalogNote);
            }
            else if (ActiveCatalog.Version == 3 && ActiveCatalog.Note == ModSettings.secondCatalogNote)
            {
                SetNote("");
            }

            // Only continue with catalog update if we found any changes to update the catalog (ignoring the pure catalog changes)
            if (changeNotesNewMods.Length + changeNotesNewGroups.Length + changeNotesNewCompatibilities.Length + changeNotesNewAuthors.Length + 
                changeNotesUpdatedMods.Count + changeNotesUpdatedAuthorsByID.Count + changeNotesUpdatedAuthorsByURL.Count + 
                changeNotesRemovedMods.Length + changeNotesRemovedGroups.Length + changeNotesRemovedCompatibilities.Length == 0)
            {
                Logger.UpdaterLog("No changes or new additions found. No new catalog created.");
            }
            else
            {
                UpdateChangeNotes();

                string partialPath = Path.Combine(ModSettings.updaterPath, $"{ ModSettings.internalName }_Catalog_v{ ActiveCatalog.VersionString() }");

                // Save the new catalog
                if (ActiveCatalog.Save(partialPath + ".xml"))
                {
                    // Save change notes, in the same folder as the new catalog
                    Toolkit.SaveToFile(changeNotes.ToString(), partialPath + "_ChangeNotes.txt");

                    // Save the combined CSVs, in the same folder as the new catalog
                    Toolkit.SaveToFile(CSVCombined.ToString(), partialPath + "_Imports.csv.txt");

                    Logger.UpdaterLog($"New catalog { ActiveCatalog.VersionString() } created and change notes saved.");

                    // Copy the updater logfile to the same folder as the new catalog
                    Toolkit.CopyFile(ModSettings.updaterLogfileFullPath, partialPath + "_Updater.log");
                }
                else
                {
                    Logger.UpdaterLog("Could not save the new catalog. All updates were lost.", Logger.error);
                }
            }

            // Log a CSV action for required assets that are missing in the catalog
            if (UnknownRequiredAssets.Length > 0)
            {
                Logger.UpdaterLog("CSV action for adding assets to the catalog (after verification): Add_RequiredAssets" + UnknownRequiredAssets.ToString());
            }

            // Empty the dictionaries and change notes to free memory
            Init();

            // Run the DataDumper
            DataDumper.Start(ActiveCatalog);

            // Close the active catalog to get rid of loose ends, if any
            Logger.UpdaterLog("Closing the active catalog.");

            ActiveCatalog = null;

            Catalog.CloseActive();

            Logger.UpdaterLog("Catalog Updater has finished.", extraLine: true, duplicateToRegularLog: true);
        }


        // Get all variables and the catalog ready for updating
        private static void Init()
        {
            UnknownRequiredAssets = new StringBuilder();

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

            CSVCombined = new StringBuilder();

            // Increase the catalog version and update date
            ActiveCatalog.NewVersion(DateTime.Now);

            catalogDateString = Toolkit.DateString(ActiveCatalog.UpdateDate.Date);
        }


        // Update change notes in the mod and author change note fields, and combine the change notes for the change notes file
        private static void UpdateChangeNotes()
        {
            StringBuilder changeNotesUpdatedModsCombined = new StringBuilder();

            StringBuilder changeNotesUpdatedAuthorsCombined = new StringBuilder();

            foreach (KeyValuePair<ulong, string> modNotes in changeNotesUpdatedMods)
            {
                if (!string.IsNullOrEmpty(modNotes.Value))
                {
                    string cleanedChangeNote = modNotes.Value.Substring(2);

                    ActiveCatalog.ModDictionary[modNotes.Key].Update(extraChangeNote: $"{ catalogDateString }: { cleanedChangeNote }");

                    changeNotesUpdatedModsCombined.AppendLine($"Mod { ActiveCatalog.ModDictionary[modNotes.Key].ToString() }: " +
                        $"{ cleanedChangeNote }");
                }
            }

            foreach (KeyValuePair<ulong, string> authorNotes in changeNotesUpdatedAuthorsByID)
            {
                string cleanedChangeNote = authorNotes.Value.Substring(2);

                ActiveCatalog.AuthorIDDictionary[authorNotes.Key].Update(extraChangeNote: $"{ catalogDateString }: { cleanedChangeNote }");

                changeNotesUpdatedAuthorsCombined.AppendLine($"Author { ActiveCatalog.AuthorIDDictionary[authorNotes.Key].ToString() }: " +
                    $"{ cleanedChangeNote }");
            }

            foreach (KeyValuePair<string, string> authorNotes in changeNotesUpdatedAuthorsByURL)
            {
                string cleanedChangeNote = authorNotes.Value.Substring(2);

                ActiveCatalog.AuthorURLDictionary[authorNotes.Key].Update(extraChangeNote: $"{ catalogDateString }: { cleanedChangeNote }");

                changeNotesUpdatedAuthorsCombined.AppendLine($"Author { ActiveCatalog.AuthorURLDictionary[authorNotes.Key].ToString() }: " +
                    $"{ cleanedChangeNote }");
            }

            // Combine the total change notes
            changeNotes = $"Change Notes for Catalog { ActiveCatalog.VersionString() }\n" +
                "-------------------------------\n" +
                $"{ ActiveCatalog.UpdateDate:D}, { ActiveCatalog.UpdateDate:t}\n" +
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
        internal static string SetReviewDate(DateTime newDate)
        {
            if (newDate == default)
            {
                return "Invalid Date.";
            }

            reviewDate = newDate;

            return "";
        }


        // Set a new note for the catalog
        internal static void SetNote(string newCatalogNote)
        {
            string change = string.IsNullOrEmpty(newCatalogNote) ? "removed" : string.IsNullOrEmpty(ActiveCatalog.Note) ? "added" : "changed";

            ActiveCatalog.Update(note: newCatalogNote);

            AddCatalogChangeNote($"Catalog note { change }.");
        }


        // Set a new header text for the catalog
        internal static void SetHeaderText(string text)
        {
            string change = string.IsNullOrEmpty(text) ? "removed" : string.IsNullOrEmpty(ActiveCatalog.ReportHeaderText) ? "added" : "changed";

            ActiveCatalog.Update(reportHeaderText: text);

            AddCatalogChangeNote($"Catalog header text { change }.");
        }


        // Set a new footer text for the catalog
        internal static void SetFooterText(string text)
        {
            string change = string.IsNullOrEmpty(text) ? "removed" : string.IsNullOrEmpty(ActiveCatalog.ReportFooterText) ? "added" : "changed";
            
            ActiveCatalog.Update(reportFooterText: text);

            AddCatalogChangeNote($"Catalog footer text { change }.");
        }


        // Add or get a mod. When adding, a mod name, incompatible stability and/or unlisted/removed status can be supplied. On existing mods this is ignored.
        // A review date is not set, that is only done on UpdateMod()
        internal static Mod GetOrAddMod(ulong steamID,
                                        string name,
                                        bool incompatible = false,
                                        bool unlisted = false,
                                        bool removed = false)
        {
            Mod catalogMod;

            // Get the mod from the catalog, or add a new one
            if (ActiveCatalog.ModDictionary.ContainsKey(steamID))
            {
                catalogMod = ActiveCatalog.ModDictionary[steamID];
            }
            else
            {
                // Log an empty mod name. This could be an error, although there is a workshop mod without a name (ofcourse there is)
                if (name == "")
                {
                    Logger.UpdaterLog($"Mod name not found for { steamID }. This could be an actual unnamed mod, or a Steam error.", Logger.warning);
                }
                
                catalogMod = ActiveCatalog.AddOrUpdateMod(steamID, name);

                string modType = "Mod";

                // Add incompatible status if needed
                if (incompatible)
                {
                    catalogMod.Update(stability: Enums.ModStability.IncompatibleAccordingToWorkshop);

                    modType = "Incompatible mod";
                }

                // Add removed or unlisted status if needed
                if (unlisted || removed)
                {
                    catalogMod.Statuses.Add(unlisted ? Enums.ModStatus.UnlistedInWorkshop : Enums.ModStatus.RemovedFromWorkshop);

                    modType = (unlisted ? "Unlisted " : "Removed ") + modType.ToLower();
                }

                catalogMod.Update(extraChangeNote: $"{ catalogDateString }: added");

                changeNotesNewMods.AppendLine($"{ modType } added: { catalogMod.ToString() }");
            }

            return catalogMod;
        }


        // Update a mod with newly found information, including exclusions
        internal static void UpdateMod(Mod catalogMod,
                                       string name = null,
                                       DateTime? published = null,
                                       DateTime? updated = null,
                                       ulong authorID = 0,
                                       string authorURL = null,
                                       string archiveURL = null,
                                       string sourceURL = null,
                                       string compatibleGameVersionString = null,
                                       Enums.ModStability stability = default,
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
                (updated == null || updated == catalogMod.Updated || catalogMod.AddedThisSession ? "" : ", new update") +
                (authorID == 0 || authorID == catalogMod.AuthorID || catalogMod.AuthorID != 0 || catalogMod.AddedThisSession ? "" : ", author ID added") +
                (authorURL == null || authorURL == catalogMod.AuthorURL || catalogMod.AddedThisSession ? "" : ", author URL") +
                (archiveURL == null || archiveURL == catalogMod.ArchiveURL ? "" : ", archive URL") +
                (sourceURL == null || sourceURL == catalogMod.SourceURL ? "" : ", source URL") +
                (compatibleGameVersionString == null || compatibleGameVersionString == catalogMod.CompatibleGameVersionString ? "" : ", compatible game version") +
                (stability == default | stability == catalogMod.Stability ? "" : ", stability") +
                (stabilityNote == null || stabilityNote == catalogMod.StabilityNote ? "" : ", stability note") +
                (genericNote == null || genericNote == catalogMod.GenericNote ? "" : ", generic note");

            AddUpdatedModChangeNote(catalogMod, addedChangeNote);

            // Set the update date
            DateTime? modReviewDate = null;

            DateTime? modAutoReviewDate = null;

            if (!string.IsNullOrEmpty(addedChangeNote) || alwaysUpdateReviewDate)
            {
                modReviewDate = !updatedByWebCrawler ? reviewDate : modReviewDate;

                modAutoReviewDate = updatedByWebCrawler ? reviewDate : modAutoReviewDate;
            }

            // Update exclusions on certain imported changes
            if (!updatedByWebCrawler && sourceURL != null && sourceURL != catalogMod.SourceURL)
            {
                // Add exclusion on new or changed url, and swap exclusion on removal
                catalogMod.Update(exclusionForSourceURL: sourceURL != "" || !catalogMod.ExclusionForSourceURL);
            }

            if (!updatedByWebCrawler && compatibleGameVersionString != null && compatibleGameVersionString != catalogMod.CompatibleGameVersionString)
            {
                catalogMod.Update(exclusionForGameVersion: true);
            }

            // Update the mod
            catalogMod.Update(name, published, updated, authorID, authorURL, archiveURL, sourceURL, compatibleGameVersionString, requiredDLC: null, requiredMods: null,
            successors: null, alternatives: null, recommendations: null, stability, stabilityNote, statuses: null, genericNote, 
            reviewDate: modReviewDate, autoReviewDate: modAutoReviewDate);

            // Update the authors last seen date if the mod had a new update
            Author modAuthor = ActiveCatalog.GetAuthor(catalogMod.AuthorID, catalogMod.AuthorURL);

            if (modAuthor != null && catalogMod.Updated > modAuthor.LastSeen)
            {
                UpdateAuthor(modAuthor, lastSeen: catalogMod.Updated);
            }
        }


        // Add a group
        internal static void AddGroup(string groupName, List<ulong> groupMembers)
        {
            Group newGroup = ActiveCatalog.AddGroup(groupName, new List<ulong>());

            if (newGroup != null)
            {
                // Add group members separately to get change notes on all group members
                foreach (ulong groupMember in groupMembers)
                {
                    AddGroupMember(newGroup, groupMember);
                }

                changeNotesNewGroups.AppendLine($"Group added: { newGroup.ToString() }");
            }
        }


        // Remove a group
        internal static void RemoveGroup(ulong groupID)
        {
            // Remove the group from all required mod lists
            foreach (Mod catalogMod in ActiveCatalog.Mods)
            {
                catalogMod.RequiredMods.Remove(groupID);
            }

            Group oldGroup = ActiveCatalog.GroupDictionary[groupID];

            if (ActiveCatalog.Groups.Remove(oldGroup))
            {
                ActiveCatalog.GroupDictionary.Remove(groupID);

                changeNotesRemovedGroups.AppendLine($"Group removed: { oldGroup.ToString() }");

                // Remove group members to get change notes on all former group members
                foreach (ulong groupMember in oldGroup.GroupMembers)
                {
                    AddUpdatedModChangeNote(ActiveCatalog.ModDictionary[groupMember], $"removed from { oldGroup.ToString() }");
                }
            }
        }


        // Add a group member
        internal static void AddGroupMember(Group group, ulong groupMember)
        {
            group.GroupMembers.Add(groupMember);

            ActiveCatalog.AddRequiredGroup(groupMember);

            AddUpdatedModChangeNote(ActiveCatalog.ModDictionary[groupMember], $"added to { group.ToString() }");
        }


        // Remove a group member
        internal static bool RemoveGroupMember(Group group, ulong groupMember)
        {
            if (!group.GroupMembers.Remove(groupMember))
            {
                return false;
            }

            // Get all mods that have this former groupmember as required mod
            List<Mod> modList = ActiveCatalog.Mods.FindAll(x => x.RequiredMods.Contains(groupMember));

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

            AddUpdatedModChangeNote(ActiveCatalog.ModDictionary[groupMember], $"removed from { group.ToString() }");

            return true;
        }


        // Add a compatibility
        internal static void AddCompatibility(ulong firstModID, string firstModname, ulong secondModID, string secondModName, 
            Enums.CompatibilityStatus compatibilityStatus, string note)
        {
            Compatibility compatibility = new Compatibility(firstModID, firstModname, secondModID, secondModName, compatibilityStatus, note);

            ActiveCatalog.Compatibilities.Add(compatibility);

            changeNotesNewCompatibilities.AppendLine($"Compatibility added between { firstModID, 10 } and { secondModID, 10 }: { compatibilityStatus }" +
                (string.IsNullOrEmpty(note) ? "" : ", " + note));
        }


        // Remove a compatibility
        internal static bool RemoveCompatibility(ulong firstModID, ulong secondModID, Enums.CompatibilityStatus compatibilityStatus)
        {
            Compatibility catalogCompatibility = ActiveCatalog.Compatibilities.Find(x => x.FirstModID == firstModID && x.SecondModID == secondModID &&
                x.Status == compatibilityStatus);

            if (!ActiveCatalog.Compatibilities.Remove(catalogCompatibility))
            {
                return false;
            }

            changeNotesRemovedCompatibilities.AppendLine($"Compatibility removed between { firstModID } and { secondModID }: \"{ compatibilityStatus }\"");

            return true;
        }


        // Add or get an author
        internal static Author GetOrAddAuthor(ulong authorID, string authorURL, string authorName)
        {
            Author catalogAuthor = ActiveCatalog.GetAuthor(authorID, authorURL);

            // Get the author from the catalog, or add a new one
            if (catalogAuthor != null)
            {
                // Existing author. Update the name, if needed.
                UpdateAuthor(catalogAuthor, name: authorName);
            }
            else
            {
                // Log if the author name is equal to the author ID. Could be an error, although some authors have their ID as name (ofcourse they do)
                if (authorID != 0 && authorName == authorID.ToString())
                {
                    Logger.UpdaterLog($"Author found with profile ID as name: { authorID }. Some authors do this, but it could also be a Steam error.", Logger.warning);
                }

                // Log if we have two authors with the same name, which could an existing author we missed when a custom URL has changed
                Author namesakeAuthor = ActiveCatalog.Authors.Find(x => x.Name == authorName);
                
                if (namesakeAuthor != default)
                {
                    string authors = (authorID == 0 ? authorURL : authorID.ToString()) + " and " + 
                        (namesakeAuthor.ProfileID == 0 ? namesakeAuthor.CustomURL : namesakeAuthor.ProfileID.ToString());

                    Logger.UpdaterLog($"Found two authors with the name \"{ authorName }\": { authors }. This could be a coincidence or an error.", Logger.warning);
                }

                catalogAuthor = ActiveCatalog.AddAuthor(authorID, authorURL, authorName);

                catalogAuthor.Update(extraChangeNote: $"{ catalogDateString }: added");

                changeNotesNewAuthors.AppendLine($"Author added: { catalogAuthor.ToString() }");
            }

            return catalogAuthor;
        }


        // Update an author with newly found information, including exclusions
        internal static void UpdateAuthor(Author catalogAuthor,
                                          ulong authorID = 0,
                                          string authorURL = null,
                                          string name = null,
                                          DateTime? lastSeen = null,
                                          bool? retired = null)
        {
            if (catalogAuthor == null)
            {
                return;
            }

            // Set the change note for all changed values   [Todo 0.4] causes duplicates in change notes, especially for last seen
            string addedChangeNote =
                (authorID == 0 || authorID == catalogAuthor.ProfileID || catalogAuthor.ProfileID != 0 ? "" : ", profile ID added") +
                (authorURL == null || authorURL == catalogAuthor.CustomURL ? "" : ", custom URL") +
                (name == null || name == catalogAuthor.Name ? "" : ", name") +
                (lastSeen == null || lastSeen == catalogAuthor.LastSeen || catalogAuthor.AddedThisSession ? "" : ", last seen date") +
                (retired == null || retired == catalogAuthor.Retired ? "" : $", { (retired == true ? "now" : "no longer") } retired");

            AddUpdatedAuthorChangeNote(catalogAuthor, addedChangeNote);

            // Update the author
            catalogAuthor.Update(authorID, authorURL, name, lastSeen, retired, exclusionForRetired: catalogAuthor.ExclusionForRetired || retired == true);

            // [Todo 0.4] Not implemented yet: distribute new ID or changed URL to all mods
        }


        // Retire authors that are now eligible due to last seen date, and authors that don't have a mod in the Steam Workshop anymore
        private static void UpdateAuthorRetirement()
        {
            // Make temporary lists of all authors that have at least one Workshop mod
            List<ulong> ActiveAuthorIDs = new List<ulong>();

            List<string> ActiveAuthorURLs = new List<string>();

            foreach (Mod catalogMod in ActiveCatalog.Mods)
            {
                if (!catalogMod.Statuses.Contains(Enums.ModStatus.RemovedFromWorkshop))
                {
                    if (catalogMod.AuthorID != 0 && !ActiveAuthorIDs.Contains(catalogMod.AuthorID))
                    {
                        ActiveAuthorIDs.Add(catalogMod.AuthorID);
                    }

                    if (!string.IsNullOrEmpty(catalogMod.AuthorURL) && !ActiveAuthorURLs.Contains(catalogMod.AuthorURL))
                    {
                        ActiveAuthorURLs.Add(catalogMod.AuthorURL);
                    }
                }
            }

            // Check and update retirement for all authors
            foreach (Author catalogAuthor in ActiveCatalog.Authors)
            {
                // Set exclusion for early retirement and remove it otherwise
                if (catalogAuthor.Retired && catalogAuthor.LastSeen.AddMonths(ModSettings.monthsOfInactivityToRetireAuthor) >= DateTime.Today)
                {
                    catalogAuthor.Update(exclusionForRetired: true);
                }
                else
                {
                    catalogAuthor.Update(exclusionForRetired: false);
                }

                // Set retirement
                if (!ActiveAuthorIDs.Contains(catalogAuthor.ProfileID) && !ActiveAuthorURLs.Contains(catalogAuthor.CustomURL))
                {
                    // Authors without a mod in the Workshop
                    if (!catalogAuthor.Retired)
                    {
                        // Only update if they weren't set to retired yet
                        AddUpdatedAuthorChangeNote(catalogAuthor, "no longer has mods on the workshop");

                        UpdateAuthor(catalogAuthor, retired: true);
                    }
                }

                else if (catalogAuthor.LastSeen != default && catalogAuthor.LastSeen.AddMonths(ModSettings.monthsOfInactivityToRetireAuthor) < DateTime.Today)
                {
                    // Authors that are retired based on last seen date
                    UpdateAuthor(catalogAuthor, retired: true);

                    catalogAuthor.Update(exclusionForRetired: false);
                }

                else if (!catalogAuthor.ExclusionForRetired)
                {
                    // Authors that have mods in the Workshop, and are recently enough seen, and don't have an exclusion for retired
                    UpdateAuthor(catalogAuthor, retired: false);
                }
            }
        }


        // Add a mod status, including exclusions and removing conflicting statuses.
        internal static void AddStatus(Mod catalogMod, Enums.ModStatus status, bool updatedByWebCrawler = false)
        {
            if (status == default || catalogMod.Statuses.Contains(status))
            {
                return;
            }

            catalogMod.Statuses.Add(status);

            AddUpdatedModChangeNote(catalogMod, $"{ status } added");

            // Remove conflicting statuses, and change some exclusions
            if (status == Enums.ModStatus.UnlistedInWorkshop)
            {
                RemoveStatus(catalogMod, Enums.ModStatus.RemovedFromWorkshop);
            }
            else if (status == Enums.ModStatus.RemovedFromWorkshop)
            {
                RemoveStatus(catalogMod, Enums.ModStatus.UnlistedInWorkshop);
                RemoveStatus(catalogMod, Enums.ModStatus.NoCommentSectionOnWorkshop);
                RemoveStatus(catalogMod, Enums.ModStatus.NoDescription);
                catalogMod.Update(exclusionForNoDescription: false);
            }
            else if (status == Enums.ModStatus.NoDescription && !updatedByWebCrawler)
            {
                // Exclusion is only needed if this status was set by the FileImporter
                catalogMod.Update(exclusionForNoDescription: true);
            }
            else if (status == Enums.ModStatus.NoLongerNeeded)
            {
                RemoveStatus(catalogMod, Enums.ModStatus.Deprecated);
                RemoveStatus(catalogMod, Enums.ModStatus.Abandoned);
            }
            else if (status == Enums.ModStatus.Deprecated)
            {
                RemoveStatus(catalogMod, Enums.ModStatus.NoLongerNeeded);
                RemoveStatus(catalogMod, Enums.ModStatus.Abandoned);
            }
            else if (status == Enums.ModStatus.Abandoned)
            {
                RemoveStatus(catalogMod, Enums.ModStatus.NoLongerNeeded);
                RemoveStatus(catalogMod, Enums.ModStatus.Deprecated);
            }
            else if (status == Enums.ModStatus.SourceUnavailable)
            {
                RemoveStatus(catalogMod, Enums.ModStatus.SourceBundled);
                RemoveStatus(catalogMod, Enums.ModStatus.SourceNotUpdated);
                RemoveStatus(catalogMod, Enums.ModStatus.SourceObfuscated);

                if (!string.IsNullOrEmpty(catalogMod.SourceURL))
                {
                    UpdateMod(catalogMod, sourceURL: "", updatedByWebCrawler: updatedByWebCrawler);

                    catalogMod.Update(exclusionForSourceURL: true);
                }
            }
            else if (status == Enums.ModStatus.SourceBundled || status == Enums.ModStatus.SourceNotUpdated || status == Enums.ModStatus.SourceObfuscated)
            {
                RemoveStatus(catalogMod, Enums.ModStatus.SourceUnavailable);
            }
            else if (status == Enums.ModStatus.MusicCopyrighted)
            {
                RemoveStatus(catalogMod, Enums.ModStatus.MusicCopyrightFree);
                RemoveStatus(catalogMod, Enums.ModStatus.MusicCopyrightUnknown);
            }
            else if (status == Enums.ModStatus.MusicCopyrightFree)
            {
                RemoveStatus(catalogMod, Enums.ModStatus.MusicCopyrighted);
                RemoveStatus(catalogMod, Enums.ModStatus.MusicCopyrightUnknown);
            }
            else if (status == Enums.ModStatus.MusicCopyrightUnknown)
            {
                RemoveStatus(catalogMod, Enums.ModStatus.MusicCopyrighted);
                RemoveStatus(catalogMod, Enums.ModStatus.MusicCopyrightFree);
            }
        }


        // Remove a mod status
        internal static bool RemoveStatus(Mod catalogMod, Enums.ModStatus status, bool updatedByWebCrawler = false)
        {
            bool success = catalogMod.Statuses.Remove(status);

            if (success)
            {
                AddUpdatedModChangeNote(catalogMod, $"{ status } removed");

                // Add or remove exclusion for some statuses
                if (status == Enums.ModStatus.NoDescription && !updatedByWebCrawler)
                {
                    // Only if the status is removed by the FileImporter: if there was an exclusion, remove it, otherwise add it.
                    catalogMod.Update(exclusionForNoDescription: !catalogMod.ExclusionForNoDescription);
                }
                else if (status == Enums.ModStatus.SourceUnavailable)
                {
                    catalogMod.Update(exclusionForSourceURL: false);
                }

            }

            return success;
        }


        // Add a required DLC
        internal static void AddRequiredDLC(Mod catalogMod, Enums.DLC requiredDLC)
        {
            if (requiredDLC != default && !catalogMod.RequiredDLC.Contains(requiredDLC))
            {
                catalogMod.RequiredDLC.Add(requiredDLC);

                catalogMod.AddExclusionForRequiredDLC(requiredDLC);

                AddUpdatedModChangeNote(catalogMod, $"required DLC { Toolkit.ConvertDLCtoString(requiredDLC) } added");
            }
        }


        // Remove a required DLC
        internal static void RemoveRequiredDLC(Mod catalogMod, Enums.DLC requiredDLC)
        {
            if (catalogMod.RequiredDLC.Remove(requiredDLC))
            {
                catalogMod.ExclusionForRequiredDLC.Remove(requiredDLC);

                AddUpdatedModChangeNote(catalogMod, $"required DLC { Toolkit.ConvertDLCtoString(requiredDLC) } removed");
            }
        }


        // Add a required mod, including exclusion, required group and change notes
        internal static void AddRequiredMod(Mod catalogMod, ulong requiredID)
        {
            if (ActiveCatalog.IsValidID(requiredID, allowGroup: true) && !catalogMod.RequiredMods.Contains(requiredID))
            {
                catalogMod.RequiredMods.Add(requiredID);

                if (ActiveCatalog.GroupDictionary.ContainsKey(requiredID))
                {
                    // requiredID is a group
                    AddUpdatedModChangeNote(catalogMod, $"required group { requiredID } added");
                }
                else
                {
                    // requiredID is a mod
                    AddUpdatedModChangeNote(catalogMod, $"required mod { requiredID } added");

                    catalogMod.AddExclusionForRequiredMods(requiredID);

                    if (ActiveCatalog.IsGroupMember(requiredID))
                    {
                        // Also add the group that requiredID is a member of
                        AddRequiredMod(catalogMod, ActiveCatalog.GetGroup(requiredID).GroupID);
                    }
                }
            }

            // If the requiredID is not a known ID, it's probably an asset. [Todo 0.4] This still gives warnings if the asset is added by add_asset in CSV
            else if (ActiveCatalog.IsValidID(requiredID, allowBuiltin: false, shouldExist: false) && !ActiveCatalog.RequiredAssets.Contains(requiredID))
            {
                UnknownRequiredAssets.Append($", { requiredID }");
                
                Logger.UpdaterLog($"Required item not found, probably an asset: { Toolkit.GetWorkshopURL(requiredID) } (for { catalogMod.ToString() }).");
            }
        }


        // Remove a required mod from a mod, including exclusion, group and change notes
        internal static void RemoveRequiredMod(Mod catalogMod, ulong requiredID)
        {
            if (catalogMod.RequiredMods.Remove(requiredID))
            {
                if (ActiveCatalog.GroupDictionary.ContainsKey(requiredID))
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

                    Group group = ActiveCatalog.GetGroup(requiredID);

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
                            RemoveRequiredMod(catalogMod, group.GroupID);
                        }
                    }
                }
            }
        }


        // Add a successor, including change notes
        internal static void AddSuccessor(Mod catalogMod, ulong successorID)
        {
            if (!catalogMod.Successors.Contains(successorID))
            {
                catalogMod.Successors.Add(successorID);

                AddUpdatedModChangeNote(catalogMod, $"successor { successorID } added");
            }
        }


        // Remove a successor, including change notes
        internal static void RemoveSuccessor(Mod catalogMod, ulong successorID)
        {
            if (catalogMod.Successors.Remove(successorID))
            {
                AddUpdatedModChangeNote(catalogMod, $"successor { successorID } removed");
            }
        }


        // Add an alternative, including change notes
        internal static void AddAlternative(Mod catalogMod, ulong alternativeID)
        {
            if (!catalogMod.Alternatives.Contains(alternativeID))
            {
                catalogMod.Alternatives.Add(alternativeID);

                AddUpdatedModChangeNote(catalogMod, $"alternative { alternativeID } added");
            }
        }


        // Remove an alternative, including change notes
        internal static void RemoveAlternative(Mod catalogMod, ulong alternativeID)
        {
            if (catalogMod.Alternatives.Remove(alternativeID))
            {
                AddUpdatedModChangeNote(catalogMod, $"alternative { alternativeID } removed");
            }
        }


        // Add a recommendation, including change notes
        internal static void AddRecommendation(Mod catalogMod, ulong recommendationID)
        {
            if (!catalogMod.Recommendations.Contains(recommendationID))
            {
                catalogMod.Recommendations.Add(recommendationID);

                AddUpdatedModChangeNote(catalogMod, $"successor { recommendationID } added");
            }
        }


        // Remove a successor, including change notes
        internal static void RemoveRecommendation(Mod catalogMod, ulong recommendationID)
        {
            if (catalogMod.Recommendations.Remove(recommendationID))
            {
                AddUpdatedModChangeNote(catalogMod, $"successor { recommendationID } removed");
            }
        }


        // Add a change note for an updated mod.
        internal static void AddUpdatedModChangeNote(Mod catalogMod, string extraChangeNote)
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
        internal static void AddRemovedModChangeNote(string extraLine)
        {
            changeNotesRemovedMods.AppendLine(extraLine);
        }
        
        
        // Add a change note for an updated author.
        internal static void AddUpdatedAuthorChangeNote(Author catalogAuthor, string extraChangeNote)
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
            if (catalogAuthor.ProfileID != 0)
            {
                if (changeNotesUpdatedAuthorsByID.ContainsKey(catalogAuthor.ProfileID))
                {
                    changeNotesUpdatedAuthorsByID[catalogAuthor.ProfileID] += extraChangeNote;
                }
                else
                {
                    changeNotesUpdatedAuthorsByID.Add(catalogAuthor.ProfileID, extraChangeNote);
                }
            }
            else
            {
                if (changeNotesUpdatedAuthorsByURL.ContainsKey(catalogAuthor.CustomURL))
                {
                    changeNotesUpdatedAuthorsByURL[catalogAuthor.CustomURL] += extraChangeNote;
                }
                else
                {
                    changeNotesUpdatedAuthorsByURL.Add(catalogAuthor.CustomURL, extraChangeNote);
                }
            }
        }


        // Add a change note for a catalog change
        internal static void AddCatalogChangeNote(string extraLine)
        {
            changeNotesCatalog.AppendLine(extraLine);
        }
    }
}
