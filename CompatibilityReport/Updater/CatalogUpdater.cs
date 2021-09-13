using System;
using System.Collections.Generic;
using System.IO;
using CompatibilityReport.CatalogData;
using CompatibilityReport.Util;

// CatalogUpdater uses information gathered by WebCrawler and FileImporter to update the catalog and save it as a new version, with auto generated change notes.

namespace CompatibilityReport.Updater
{
    public static class CatalogUpdater
    {
        private static bool hasRun;
        private static DateTime reviewDate;


        // Gather new information and save a new, updated catalog.
        public static void Start()
        {
            // Todo 0.7 Read updater settings file
            if (hasRun || !ModSettings.UpdaterEnabled)
            {
                return;
            }

            Logger.Log("Catalog Updater started. See separate logfile for details.");

            Catalog catalog = Catalog.Load();

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

            catalog.NewVersion(DateTime.Now);

            Toolkit.DeleteFile(Path.Combine(ModSettings.WorkPath, ModSettings.TempCsvCombinedFileName));

            if (ModSettings.WebCrawlerEnabled)
            {
                WebCrawler.Start(catalog);
            }
            
            FileImporter.Start(catalog);

            UpdateAuthorRetirement(catalog);

            UpdateCompatibilityModNames(catalog);

            if (catalog.Version == 2 && catalog.Note == ModSettings.FirstCatalogNote)
            {
                SetNote(catalog, "");
            }

            string unknownAssets = catalog.GetUnknownAssetsString();
            if (!string.IsNullOrEmpty(unknownAssets))
            {
                Logger.UpdaterLog($"CSV action for adding assets to the catalog (after verification): Add_RequiredAssets { unknownAssets }");
            }

            DataDumper.Start(catalog);

            SaveCatalog(catalog);

            Logger.UpdaterLog("Catalog Updater has finished.");
            Logger.Log("Catalog Updater has finished.\n");
        }


        // Save the new catalog with change notes, combined CSV file and updater log.
        public static void SaveCatalog(Catalog catalog)
        {
            if (!catalog.ChangeNotes.Any())
            {
                Logger.UpdaterLog("No changes or new additions found. No new catalog created.");
            }
            else
            {
                string partialPath = Path.Combine(ModSettings.UpdaterPath, $"{ ModSettings.InternalName }_Catalog_v{ catalog.VersionString() }");

                // Save the new catalog
                if (catalog.Save(partialPath + ".xml"))
                {
                    Toolkit.SaveToFile(catalog.ChangeNotes.Combined(catalog), partialPath + "_ChangeNotes.txt");
                    Toolkit.MoveFile(Path.Combine(ModSettings.WorkPath, ModSettings.TempCsvCombinedFileName), partialPath + "_Imports.csv.txt");

                    Logger.UpdaterLog($"New catalog { catalog.VersionString() } created and change notes saved.");
                    Toolkit.CopyFile(Path.Combine(ModSettings.UpdaterPath, ModSettings.UpdaterLogFileName), partialPath + "_Updater.log");
                }
                else
                {
                    Logger.UpdaterLog("Could not save the new catalog. All updates were lost.", Logger.Error);
                }
            }

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

            catalog.ChangeNotes.CatalogChanges.AppendLine($"Catalog note { change }.");
        }


        // Set a new header text for the catalog
        public static void SetHeaderText(Catalog catalog, string text)
        {
            string change = string.IsNullOrEmpty(text) ? "removed" : string.IsNullOrEmpty(catalog.ReportHeaderText) ? "added" : "changed";

            catalog.Update(reportHeaderText: text);

            catalog.ChangeNotes.CatalogChanges.AppendLine($"Catalog header text { change }.");
        }


        // Set a new footer text for the catalog
        public static void SetFooterText(Catalog catalog, string text)
        {
            string change = string.IsNullOrEmpty(text) ? "removed" : string.IsNullOrEmpty(catalog.ReportFooterText) ? "added" : "changed";
            
            catalog.Update(reportFooterText: text);

            catalog.ChangeNotes.CatalogChanges.AppendLine($"Catalog footer text { change }.");
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
            newMod.AddChangeNote($"{ Toolkit.DateString(catalog.UpdateDate) }: added");

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

            catalog.ChangeNotes.NewMods.AppendLine($"{ modType } added: { newMod.ToString() }");

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

            catalog.ChangeNotes.AddUpdatedMod(catalogMod, (string.IsNullOrEmpty(addedChangeNote) ? "" : addedChangeNote.Substring(2)));

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
                UpdateAuthor(catalog, modAuthor, lastSeen: catalogMod.Updated);
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

                catalog.ChangeNotes.NewGroups.AppendLine($"Group added: { newGroup.ToString() }");
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
                catalog.ChangeNotes.RemovedGroups.AppendLine($"Group removed: { oldGroup.ToString() }");

                // Remove group members to get change notes on all former group members
                foreach (ulong groupMember in oldGroup.GroupMembers)
                {
                    catalog.ChangeNotes.AddUpdatedMod(catalog.GetMod(groupMember), $"removed from { oldGroup.ToString() }");
                }
            }

            return success;
        }


        // Add a group member
        public static void AddGroupMember(Catalog catalog, Group group, ulong groupMember)
        {
            group.GroupMembers.Add(groupMember);

            AddGroupAsRequiredMod(catalog, groupMember);

            catalog.ChangeNotes.AddUpdatedMod(catalog.GetMod(groupMember), $"added to { group.ToString() }");
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

            catalog.ChangeNotes.AddUpdatedMod(catalog.GetMod(groupMember), $"removed from { group.ToString() }");

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

            catalog.ChangeNotes.NewCompatibilities.AppendLine($"Compatibility added between { firstModID, 10 } and { secondModID, 10 }: { compatibilityStatus }" +
                (string.IsNullOrEmpty(compatibilityNote) ? "" : ", " + compatibilityNote));
        }


        // Remove a compatibility
        public static bool RemoveCompatibility(Catalog catalog, Compatibility catalogCompatibility)
        {
            if (!catalog.Compatibilities.Remove(catalogCompatibility))
            {
                return false;
            }

            catalog.ChangeNotes.RemovedCompatibilities.AppendLine($"Compatibility removed between { catalogCompatibility.FirstSteamID } and " +
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
            catalogAuthor.AddChangeNote($"{ Toolkit.DateString(catalog.UpdateDate) }: added");

            catalog.ChangeNotes.NewAuthors.AppendLine($"Author added: { catalogAuthor.ToString() }");

            return catalogAuthor;
        }


        // Update an author with newly found information, including exclusions
        public static void UpdateAuthor(Catalog catalog, 
                                        Author catalogAuthor,
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

            catalog.ChangeNotes.AddUpdatedAuthor(catalogAuthor, (string.IsNullOrEmpty(addedChangeNote) ? "" : addedChangeNote.Substring(2)));

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
                        catalog.ChangeNotes.AddUpdatedAuthor(catalogAuthor, "no longer has mods on the Steam Workshop");

                        UpdateAuthor(catalog, catalogAuthor, retired: true);
                    }
                }

                else if (catalogAuthor.LastSeen != default && catalogAuthor.LastSeen.AddMonths(ModSettings.MonthsOfInactivityToRetireAuthor) < DateTime.Today)
                {
                    // Authors that are retired based on last seen date
                    UpdateAuthor(catalog, catalogAuthor, retired: true);

                    catalogAuthor.Update(exclusionForRetired: false);
                }

                else if (!catalogAuthor.ExclusionForRetired)
                {
                    // Authors that have mods in the Steam Workshop, and are recently enough seen, and don't have an exclusion for retired
                    UpdateAuthor(catalog, catalogAuthor, retired: false);
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
        public static void AddStatus(Catalog catalog, Mod catalogMod, Enums.Status status, bool updatedByWebCrawler = false)
        {
            if (status == default || catalogMod.Statuses.Contains(status))
            {
                return;
            }

            catalogMod.Statuses.Add(status);

            catalog.ChangeNotes.AddUpdatedMod(catalogMod, $"{ status } added");

            // Remove conflicting statuses, and change some exclusions
            if (status == Enums.Status.UnlistedInWorkshop)
            {
                RemoveStatus(catalog, catalogMod, Enums.Status.RemovedFromWorkshop);
            }
            else if (status == Enums.Status.RemovedFromWorkshop)
            {
                RemoveStatus(catalog, catalogMod, Enums.Status.UnlistedInWorkshop);
                RemoveStatus(catalog, catalogMod, Enums.Status.NoCommentSection);
                RemoveStatus(catalog, catalogMod, Enums.Status.NoDescription);
                catalogMod.UpdateExclusions(exclusionForNoDescription: false);
            }
            else if (status == Enums.Status.NoDescription && !updatedByWebCrawler)
            {
                // Exclusion is only needed if this status was set by the FileImporter
                catalogMod.UpdateExclusions(exclusionForNoDescription: true);
            }
            else if (status == Enums.Status.NoLongerNeeded)
            {
                RemoveStatus(catalog, catalogMod, Enums.Status.Deprecated);
                RemoveStatus(catalog, catalogMod, Enums.Status.Abandoned);
            }
            else if (status == Enums.Status.Deprecated)
            {
                RemoveStatus(catalog, catalogMod, Enums.Status.NoLongerNeeded);
                RemoveStatus(catalog, catalogMod, Enums.Status.Abandoned);
            }
            else if (status == Enums.Status.Abandoned)
            {
                RemoveStatus(catalog, catalogMod, Enums.Status.NoLongerNeeded);
                RemoveStatus(catalog, catalogMod, Enums.Status.Deprecated);
            }
            else if (status == Enums.Status.MusicCopyrighted)
            {
                RemoveStatus(catalog, catalogMod, Enums.Status.MusicCopyrightFree);
                RemoveStatus(catalog, catalogMod, Enums.Status.MusicCopyrightUnknown);
            }
            else if (status == Enums.Status.MusicCopyrightFree)
            {
                RemoveStatus(catalog, catalogMod, Enums.Status.MusicCopyrighted);
                RemoveStatus(catalog, catalogMod, Enums.Status.MusicCopyrightUnknown);
            }
            else if (status == Enums.Status.MusicCopyrightUnknown)
            {
                RemoveStatus(catalog, catalogMod, Enums.Status.MusicCopyrighted);
                RemoveStatus(catalog, catalogMod, Enums.Status.MusicCopyrightFree);
            }
        }


        // Remove a mod status
        public static bool RemoveStatus(Catalog catalog, Mod catalogMod, Enums.Status status, bool updatedByWebCrawler = false)
        {
            bool success = catalogMod.Statuses.Remove(status);

            if (success)
            {
                catalog.ChangeNotes.AddUpdatedMod(catalogMod, $"{ status } removed");

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
        public static void AddRequiredDLC(Catalog catalog, Mod catalogMod, Enums.Dlc requiredDLC)
        {
            if (requiredDLC != default && !catalogMod.RequiredDlcs.Contains(requiredDLC))
            {
                catalogMod.RequiredDlcs.Add(requiredDLC);

                catalogMod.AddExclusion(requiredDLC);

                catalog.ChangeNotes.AddUpdatedMod(catalogMod, $"required DLC { Toolkit.ConvertDlcToString(requiredDLC) } added");
            }
        }


        // Remove a required DLC
        public static void RemoveRequiredDLC(Catalog catalog, Mod catalogMod, Enums.Dlc requiredDLC)
        {
            if (catalogMod.RequiredDlcs.Remove(requiredDLC))
            {
                catalogMod.ExclusionForRequiredDlc.Remove(requiredDLC);

                catalog.ChangeNotes.AddUpdatedMod(catalogMod, $"required DLC { Toolkit.ConvertDlcToString(requiredDLC) } removed");
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
                    catalog.ChangeNotes.AddUpdatedMod(catalogMod, $"required group { requiredID } added");
                }
                else
                {
                    // requiredID is a mod
                    catalog.ChangeNotes.AddUpdatedMod(catalogMod, $"required mod { requiredID } added");

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

            // If the requiredID is not a known ID, it's probably an asset. This gives "false" positives if an asset is also added through CSV in the same update session.
            else if (catalog.IsValidID(requiredID, shouldExist: false) && !catalog.RequiredAssets.Contains(requiredID))
            {
                if (catalog.AddUnknownAsset(requiredID))
                {
                    Logger.UpdaterLog($"Required item not found, probably an asset: { Toolkit.GetWorkshopUrl(requiredID) } (for { catalogMod.ToString() }).");
                }
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
                    catalog.ChangeNotes.AddUpdatedMod(catalogMod, $"required Group { requiredID } removed");
                }
                else
                {
                    // requiredID is a mod
                    catalog.ChangeNotes.AddUpdatedMod(catalogMod, $"required Mod { requiredID } removed");

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
        public static void AddSuccessor(Catalog catalog, Mod catalogMod, ulong successorID)
        {
            if (!catalogMod.Successors.Contains(successorID))
            {
                catalogMod.Successors.Add(successorID);

                catalog.ChangeNotes.AddUpdatedMod(catalogMod, $"successor { successorID } added");
            }
        }


        // Remove a successor, including change notes
        public static void RemoveSuccessor(Catalog catalog, Mod catalogMod, ulong successorID)
        {
            if (catalogMod.Successors.Remove(successorID))
            {
                catalog.ChangeNotes.AddUpdatedMod(catalogMod, $"successor { successorID } removed");
            }
        }


        // Add an alternative, including change notes
        public static void AddAlternative(Catalog catalog, Mod catalogMod, ulong alternativeID)
        {
            if (!catalogMod.Alternatives.Contains(alternativeID))
            {
                catalogMod.Alternatives.Add(alternativeID);

                catalog.ChangeNotes.AddUpdatedMod(catalogMod, $"alternative { alternativeID } added");
            }
        }


        // Remove an alternative, including change notes
        public static void RemoveAlternative(Catalog catalog, Mod catalogMod, ulong alternativeID)
        {
            if (catalogMod.Alternatives.Remove(alternativeID))
            {
                catalog.ChangeNotes.AddUpdatedMod(catalogMod, $"alternative { alternativeID } removed");
            }
        }


        // Add a recommendation, including change notes
        public static void AddRecommendation(Catalog catalog, Mod catalogMod, ulong recommendationID)
        {
            if (!catalogMod.Recommendations.Contains(recommendationID))
            {
                catalogMod.Recommendations.Add(recommendationID);

                catalog.ChangeNotes.AddUpdatedMod(catalogMod, $"successor { recommendationID } added");
            }
        }


        // Remove a recommendation, including change notes
        public static void RemoveRecommendation(Catalog catalog, Mod catalogMod, ulong recommendationID)
        {
            if (catalogMod.Recommendations.Remove(recommendationID))
            {
                catalog.ChangeNotes.AddUpdatedMod(catalogMod, $"successor { recommendationID } removed");
            }
        }
    }
}
