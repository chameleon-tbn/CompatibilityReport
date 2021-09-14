using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            // Todo 0.7 Read updater settings file.
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
            Toolkit.DeleteFile(Path.Combine(ModSettings.WorkPath, ModSettings.TempCsvCombinedFileName));

            Logger.UpdaterLog($"Catalog Updater started. { ModSettings.ModName } version { ModSettings.FullVersion }. " +
                $"Game version { Toolkit.ConvertGameVersionToString(Toolkit.CurrentGameVersion()) }. " +
                $"Current catalog version { catalog.VersionString() }, created on { catalog.UpdateDate:D}, { catalog.UpdateDate:t}.");

            catalog.NewVersion(DateTime.Now);

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

            SaveCatalog(catalog);

            DataDumper.Start(catalog);

            Logger.UpdaterLog("Catalog Updater has finished.");
            Logger.Log("Catalog Updater has finished.\n");

            Logger.CloseUpdateLog();
        }


        // Save the new catalog with change notes, combined CSV file and updater log.
        public static void SaveCatalog(Catalog catalog)
        {
            if (!catalog.ChangeNotes.Any())
            {
                Logger.UpdaterLog("No changes or new additions found. No new catalog created.");
                return;
            }
                
            string partialPath = Path.Combine(ModSettings.UpdaterPath, $"{ ModSettings.InternalName }_Catalog_v{ catalog.VersionString() }");

            if (catalog.Save(partialPath + ".xml"))
            {
                Logger.UpdaterLog($"New catalog { catalog.VersionString() } created and change notes saved.");

                Toolkit.SaveToFile(catalog.ChangeNotes.Combined(catalog), partialPath + "_ChangeNotes.txt");
                Toolkit.MoveFile(Path.Combine(ModSettings.WorkPath, ModSettings.TempCsvCombinedFileName), partialPath + "_Imports.csv.txt");
                Toolkit.CopyFile(Path.Combine(ModSettings.UpdaterPath, ModSettings.UpdaterLogFileName), partialPath + "_Updater.log");
            }
            else
            {
                Logger.UpdaterLog("Could not save the new catalog. All updates were lost.", Logger.Error);
            }
        }


        // Set the review date used in mod ReviewDate and AutoReviewDate fields.
        public static string SetReviewDate(DateTime newDate)
        {
            reviewDate = newDate;
            return "";
        }


        // Set a new note for the catalog.
        public static void SetNote(Catalog catalog, string newCatalogNote)
        {
            catalog.ChangeNotes.CatalogChanges.AppendLine($"Catalog note { Change(catalog.Note, newCatalogNote) }.");
            catalog.Update(note: newCatalogNote);
        }


        // Set a new report header text for the catalog.
        public static void SetHeaderText(Catalog catalog, string newText)
        {
            catalog.ChangeNotes.CatalogChanges.AppendLine($"Catalog header text { Change(catalog.ReportHeaderText, newText) }.");
            catalog.Update(reportHeaderText: newText);
        }


        // Set a new report footer text for the catalog.
        public static void SetFooterText(Catalog catalog, string newText)
        {
            catalog.ChangeNotes.CatalogChanges.AppendLine($"Catalog footer text { Change(catalog.ReportFooterText, newText) }.");
            catalog.Update(reportFooterText: newText);
        }


        // Add a mod.
        public static Mod AddMod(Catalog catalog, ulong steamID, string name, bool incompatible = false, bool unlisted = false, bool removed = false)
        {
            Mod newMod = catalog.AddMod(steamID);
            newMod.Update(name: name);
            newMod.AddChangeNote($"{ Toolkit.DateString(catalog.UpdateDate) }: added");

            string modType = "Mod";

            if (incompatible)
            {
                newMod.Update(stability: Enums.Stability.IncompatibleAccordingToWorkshop);
                modType = "Incompatible mod";
            }

            if (unlisted || removed)
            {
                newMod.Statuses.Add(unlisted ? Enums.Status.UnlistedInWorkshop : Enums.Status.RemovedFromWorkshop);
                modType = (unlisted ? "Unlisted " : "Removed ") + modType.ToLower();
            }

            catalog.ChangeNotes.NewMods.AppendLine($"{ modType } added: { newMod.ToString() }");
            return newMod;
        }


        // Update a mod with newly found information. This also updates some exclusions, the author last seen date and the ReviewDate or AutoReviewDate.
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
                                     bool updatedByImporter = false)
        {
            // Collect change notes for all changed values. For stability note only if stability itself is unchanged.
            string addedChangeNote =
                (name == null || name == catalogMod.Name ? "" : $", mod name { Change(catalogMod.Name, name) }") +
                // No change note for published
                (updated == default || updated == catalogMod.Updated || catalogMod.AddedThisSession ? "" : ", new update added") +
                (authorID == 0 || authorID == catalogMod.AuthorID || catalogMod.AuthorID != 0 || catalogMod.AddedThisSession ? "" : ", author ID added") +
                (authorUrl == null || authorUrl == catalogMod.AuthorUrl || catalogMod.AddedThisSession ? "" : $", author URL { Change(catalogMod.AuthorUrl, authorUrl) }") +
                (sourceURL == null || sourceURL == catalogMod.SourceUrl ? "" : $", source URL { Change(catalogMod.SourceUrl, sourceURL) }") +
                (compatibleGameVersionString == null || compatibleGameVersionString == catalogMod.CompatibleGameVersionString ? "" : 
                    $", compatible game version { Change(catalogMod.CompatibleGameVersion(), Toolkit.ConvertToGameVersion(compatibleGameVersionString)) }") +
                (stability != default && stability != catalogMod.Stability ? ", stability changed" :
                    stabilityNote == null || stabilityNote == catalogMod.StabilityNote ? "" : $", stability note { Change(catalogMod.StabilityNote, stabilityNote) }") +
                (genericNote == null || genericNote == catalogMod.GenericNote ? "" : $", generic note { Change(catalogMod.GenericNote, genericNote) }");

            catalog.ChangeNotes.AddUpdatedMod(catalogMod.SteamID, (string.IsNullOrEmpty(addedChangeNote) ? "" : addedChangeNote.Substring(2)));

            DateTime modReviewDate = (!string.IsNullOrEmpty(addedChangeNote) || alwaysUpdateReviewDate) && updatedByImporter ? reviewDate : default;
            DateTime modAutoReviewDate = (!string.IsNullOrEmpty(addedChangeNote) || alwaysUpdateReviewDate) && !updatedByImporter ? reviewDate : default;

            if (updatedByImporter && sourceURL != null && sourceURL != catalogMod.SourceUrl)
            {
                // Add exclusion on new or changed URL, and swap exclusion on removal.
                catalogMod.UpdateExclusions(exclusionForSourceUrl: sourceURL != "" || !catalogMod.ExclusionForSourceUrl);
            }
            if (updatedByImporter && compatibleGameVersionString != null && compatibleGameVersionString != catalogMod.CompatibleGameVersionString)
            {
                // Add exclusion on new or changed game version, and remove the exclusion on removal of the game version.
                catalogMod.UpdateExclusions(exclusionForGameVersion: compatibleGameVersionString != "");
            }

            catalogMod.Update(name, published, updated, authorID, authorUrl, sourceURL, compatibleGameVersionString, stability, stabilityNote, genericNote, 
                reviewDate: modReviewDate, autoReviewDate: modAutoReviewDate);

            // Update the authors last seen date if the mod had a new update.
            Author modAuthor = catalog.GetAuthor(catalogMod.AuthorID, catalogMod.AuthorUrl);
            if (modAuthor != null && catalogMod.Updated > modAuthor.LastSeen)
            {
                UpdateAuthor(catalog, modAuthor, lastSeen: catalogMod.Updated);
            }
        }


        // Add a group.
        public static void AddGroup(Catalog catalog, string groupName, List<ulong> groupMembers)
        {
            Group newGroup = catalog.AddGroup(groupName);
            catalog.ChangeNotes.NewGroups.AppendLine($"Group added: { newGroup.ToString() }");

            // Add group members separately to get change notes on all group members.
            foreach (ulong groupMember in groupMembers)
            {
                AddGroupMember(catalog, newGroup, groupMember);
            }
        }


        // Remove a group.
        public static bool RemoveGroup(Catalog catalog, Group oldGroup)
        {
            foreach (Mod catalogMod in catalog.Mods)
            {
                catalogMod.RequiredMods.Remove(oldGroup.GroupID);
                catalogMod.Recommendations.Remove(oldGroup.GroupID);
            }

            bool success = catalog.RemoveGroup(oldGroup);

            if (success)
            {
                catalog.ChangeNotes.RemovedGroups.AppendLine($"Group removed: { oldGroup.ToString() }");

                foreach (ulong groupMember in oldGroup.GroupMembers)
                {
                    catalog.ChangeNotes.AddUpdatedMod(groupMember, $"removed from { oldGroup.ToString() }");
                }
            }

            return success;
        }


        // Add a group member. Also add the group as required and recommended mods where the new group member is already used.
        public static void AddGroupMember(Catalog catalog, Group catalogGroup, ulong groupMember)
        {
            catalogGroup.GroupMembers.Add(groupMember);
            catalog.ChangeNotes.AddUpdatedMod(groupMember, $"added to { catalogGroup.ToString() }");

            List<Mod> requiredModList = catalog.Mods.FindAll(x => x.RequiredMods.Contains(groupMember) && !x.RequiredMods.Contains(catalogGroup.GroupID));
            foreach (Mod catalogMod in requiredModList)
            {
                catalogMod.RequiredMods.Add(catalogGroup.GroupID);
                Logger.UpdaterLog($"Added { catalogGroup.ToString() } as required mod for { catalogMod.ToString() }.");
            }

            List<Mod> recommendedModList = catalog.Mods.FindAll(x => x.Recommendations.Contains(groupMember) && !x.Recommendations.Contains(catalogGroup.GroupID));
            foreach (Mod catalogMod in recommendedModList)
            {
                catalogMod.Recommendations.Add(catalogGroup.GroupID);
                Logger.UpdaterLog($"Added { catalogGroup.ToString() } as recommended mod for { catalogMod.ToString() }.");
            }
        }


        // Remove a group member. Also remove the group if empty, and remove the group as required and recommended mods where no group member is used anymore.
        public static bool RemoveGroupMember(Catalog catalog, Group catalogGroup, ulong groupMember)
        {
            if (!catalogGroup.GroupMembers.Remove(groupMember))
            {
                return false;
            }

            catalog.ChangeNotes.AddUpdatedMod(groupMember, $"removed from { catalogGroup.ToString() }");

            if (!catalogGroup.GroupMembers.Any() && RemoveGroup(catalog, catalogGroup)) 
            {
                return true;
            }

            List<Mod> requiredModList = catalog.Mods.FindAll(x => x.RequiredMods.Contains(groupMember));
            foreach (Mod catalogMod in requiredModList)
            {
                bool removeGroup = true;

                foreach (ulong otherGroupMember in catalogGroup.GroupMembers)
                {
                    removeGroup = removeGroup && !catalogMod.RequiredMods.Contains(otherGroupMember);
                }

                if (removeGroup)
                {
                    catalogMod.RequiredMods.Remove(catalogGroup.GroupID);
                    Logger.UpdaterLog($"Removed { catalogGroup.ToString() } as required mod for { catalogMod.ToString() }.");
                }
            }

            List<Mod> recommendedModList = catalog.Mods.FindAll(x => x.Recommendations.Contains(groupMember));
            foreach (Mod catalogMod in recommendedModList)
            {
                bool removeGroup = true;

                foreach (ulong otherGroupMember in catalogGroup.GroupMembers)
                {
                    removeGroup = removeGroup && !catalogMod.Recommendations.Contains(otherGroupMember);
                }

                if (removeGroup)
                {
                    catalogMod.Recommendations.Remove(catalogGroup.GroupID);
                    Logger.UpdaterLog($"Removed { catalogGroup.ToString() } as recommended mod for { catalogMod.ToString() }.");
                }
            }

            return true;
        }


        // Add a compatibility.
        public static void AddCompatibility(Catalog catalog, ulong firstModID, ulong secondModID, Enums.CompatibilityStatus compatibilityStatus, string compatibilityNote)
        {
            catalog.AddCompatibility(firstModID, secondModID, compatibilityStatus, compatibilityNote);

            catalog.ChangeNotes.NewCompatibilities.AppendLine($"Compatibility added between { firstModID, 10 } and { secondModID, 10 }: { compatibilityStatus }" +
                (string.IsNullOrEmpty(compatibilityNote) ? "" : ", " + compatibilityNote));
        }


        // Remove a compatibility.
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


        // Add an author.
        public static Author AddAuthor(Catalog catalog, ulong authorID, string authorUrl, string authorName, bool retired = false)
        {
            // Two authors with the same name could be an existing author we missed when a Custom URL has changed. There are Steam authors with the same name though.
            Author namesake = catalog.Authors.Find(x => x.Name == authorName);
            if (namesake != default)
            {
                string authors = $"{ (authorID == 0 ? authorUrl : $"{ authorID }") } and { (namesake.SteamID == 0 ? namesake.CustomUrl : $"{ namesake.SteamID }") }";
                Logger.UpdaterLog($"Found two authors with the name \"{ authorName }\": { authors }. This could be a coincidence or an error.", Logger.Warning);
            }

            Author catalogAuthor = catalog.AddAuthor(authorID, authorUrl);
            catalogAuthor.Update(name: authorName, retired: retired);

            catalogAuthor.AddChangeNote($"{ Toolkit.DateString(catalog.UpdateDate) }: added{ (retired ? " as retired" : "") }");
            catalog.ChangeNotes.NewAuthors.AppendLine($"{ (retired ? "Retired author" : "Author") } added: { catalogAuthor.ToString() }");

            return catalogAuthor;
        }


        // Update an author with newly found information.
        public static void UpdateAuthor(Catalog catalog, Author catalogAuthor, ulong authorID = 0, string authorUrl = null, string name = null, 
            DateTime lastSeen = default, bool? retired = null)
        {
            string oldURL = catalogAuthor.CustomUrl;

            // Collect change notes for all changed values.
            string addedChangeNote =
                (authorID == 0 || authorID == catalogAuthor.SteamID || catalogAuthor.SteamID != 0 ? "" : ", Steam ID added") +
                (authorUrl == null || authorUrl == catalogAuthor.CustomUrl ? "" : $", Custom URL { Change(authorUrl, catalogAuthor.CustomUrl) }") +
                (name == null || name == catalogAuthor.Name ? "" : ", name") +
                (lastSeen == default || lastSeen == catalogAuthor.LastSeen || catalogAuthor.AddedThisSession ? "" : 
                    $", last seen date { Change(lastSeen, catalogAuthor.LastSeen) }") +
                (retired == null || retired == catalogAuthor.Retired || catalogAuthor.AddedThisSession ? "" : $", { (retired == true ? "now" : "no longer") } retired");

            // Set an exclusion if retired was set to true here or reset it if retired was set to false. Exclusion will be re-evaluated at UpdateAuthorRetirement().
            catalogAuthor.Update(authorID, authorUrl, name, lastSeen, retired, exclusionForRetired: retired);
            catalog.ChangeNotes.AddUpdatedAuthor(catalogAuthor, (string.IsNullOrEmpty(addedChangeNote) ? "" : addedChangeNote.Substring(2)));

            if (addedChangeNote.Contains("Steam ID") || addedChangeNote.Contains("Custom URL"))
            {
                // Update the author ID and URL for all mods from this author, without additional changes notes.
                List<Mod> AuthorMods = catalog.Mods.FindAll(x => x.AuthorID == catalogAuthor.SteamID || x.AuthorUrl == catalogAuthor.CustomUrl || x.AuthorUrl == oldURL);
                foreach (Mod AuthorMod in AuthorMods)
                {
                    AuthorMod.Update(authorID: catalogAuthor.SteamID, authorUrl: catalogAuthor.CustomUrl);
                }
            }
        }


        // Retire authors eligible due to last seen date, and authors without mods in the Steam Workshop. Unretire others. Reset exclusion where no longer needed.
        private static void UpdateAuthorRetirement(Catalog catalog)
        {
            List<ulong> IDsOfAuthorsWithMods = new List<ulong>();
            List<string> UrlsOfAuthorsWithMods = new List<string>();

            foreach (Mod catalogMod in catalog.Mods)
            {
                if (!catalogMod.Statuses.Contains(Enums.Status.RemovedFromWorkshop))
                {
                    if (catalogMod.AuthorID != 0 && !IDsOfAuthorsWithMods.Contains(catalogMod.AuthorID))
                    {
                        IDsOfAuthorsWithMods.Add(catalogMod.AuthorID);
                    }
                    else if (!string.IsNullOrEmpty(catalogMod.AuthorUrl) && !UrlsOfAuthorsWithMods.Contains(catalogMod.AuthorUrl))
                    {
                        UrlsOfAuthorsWithMods.Add(catalogMod.AuthorUrl);
                    }
                }
            }

            foreach (Author catalogAuthor in catalog.Authors)
            {
                bool oldEnoughForRetirement = catalogAuthor.LastSeen.AddMonths(ModSettings.MonthsOfInactivityToRetireAuthor) < DateTime.Today;
                bool authorWithoutMods = !IDsOfAuthorsWithMods.Contains(catalogAuthor.SteamID) && !UrlsOfAuthorsWithMods.Contains(catalogAuthor.CustomUrl);

                if (authorWithoutMods)
                {
                    if (!catalogAuthor.Retired)
                    {
                        catalog.ChangeNotes.AddUpdatedAuthor(catalogAuthor, "no longer has mods on the Steam Workshop");
                        UpdateAuthor(catalog, catalogAuthor, retired: true);
                    }
                    catalogAuthor.Update(exclusionForRetired: false);
                }
                else if (oldEnoughForRetirement)
                {
                    if (!catalogAuthor.Retired)
                    {
                        UpdateAuthor(catalog, catalogAuthor, retired: true);
                    }
                    catalogAuthor.Update(exclusionForRetired: false);
                }
                else if (!oldEnoughForRetirement && catalogAuthor.Retired && !catalogAuthor.ExclusionForRetired)
                {
                    UpdateAuthor(catalog, catalogAuthor, retired: false);
                }
                else if (!oldEnoughForRetirement && !catalogAuthor.Retired)
                {
                    catalogAuthor.Update(exclusionForRetired: false);
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
        public static void AddStatus(Catalog catalog, Mod catalogMod, Enums.Status status, bool updatedByImporter = false)
        {
            if (catalogMod.Statuses.Contains(status))
            {
                return;
            }

            // Remove conflicting statuses, and set the NoDescription exclusion.
            if (status == Enums.Status.UnlistedInWorkshop)
            {
                catalogMod.Statuses.Remove(Enums.Status.RemovedFromWorkshop);
            }
            else if (status == Enums.Status.RemovedFromWorkshop)
            {
                catalogMod.Statuses.Remove(Enums.Status.UnlistedInWorkshop);
                catalogMod.Statuses.Remove(Enums.Status.NoCommentSection);
                catalogMod.Statuses.Remove(Enums.Status.NoDescription);
                catalogMod.UpdateExclusions(exclusionForNoDescription: false);
            }
            else if (status == Enums.Status.NoDescription && updatedByImporter)
            {
                catalogMod.UpdateExclusions(exclusionForNoDescription: true);
            }
            else if (status == Enums.Status.NoLongerNeeded || status == Enums.Status.Deprecated || status == Enums.Status.Abandoned)
            {
                catalogMod.Statuses.Remove(Enums.Status.NoLongerNeeded);
                catalogMod.Statuses.Remove(Enums.Status.Deprecated);
                catalogMod.Statuses.Remove(Enums.Status.Abandoned);
            }
            else if (status == Enums.Status.MusicCopyrighted || status == Enums.Status.MusicCopyrightFree || status == Enums.Status.MusicCopyrightUnknown)
            {
                catalogMod.Statuses.Remove(Enums.Status.MusicCopyrighted);
                catalogMod.Statuses.Remove(Enums.Status.MusicCopyrightFree);
                catalogMod.Statuses.Remove(Enums.Status.MusicCopyrightUnknown);
            }

            catalogMod.Statuses.Add(status);
            catalog.ChangeNotes.AddUpdatedMod(catalogMod.SteamID, $"{ status } added");
        }


        // Remove a mod status.
        public static bool RemoveStatus(Catalog catalog, Mod catalogMod, Enums.Status status, bool updatedByImporter = false)
        {
            bool success = catalogMod.Statuses.Remove(status);

            if (success)
            {
                catalog.ChangeNotes.AddUpdatedMod(catalogMod.SteamID, $"{ status } removed");

                if (status == Enums.Status.NoDescription && updatedByImporter)
                {
                    // If there was an exclusion, remove it, otherwise add it.
                    catalogMod.UpdateExclusions(exclusionForNoDescription: !catalogMod.ExclusionForNoDescription);
                }
            }

            return success;
        }


        // Add a required DLC, including exclusion.
        public static void AddRequiredDLC(Catalog catalog, Mod catalogMod, Enums.Dlc requiredDLC, bool updatedByImporter = false)
        {
            if (!catalogMod.RequiredDlcs.Contains(requiredDLC))
            {
                catalogMod.RequiredDlcs.Add(requiredDLC);
                catalog.ChangeNotes.AddUpdatedMod(catalogMod.SteamID, $"required DLC { Toolkit.ConvertDlcToString(requiredDLC) } added");

                if (updatedByImporter)
                {
                    catalogMod.AddExclusion(requiredDLC);
                }
            }
        }


        // Remove a required DLC, including exclusion.
        public static void RemoveRequiredDLC(Catalog catalog, Mod catalogMod, Enums.Dlc requiredDLC)
        {
            if (catalogMod.RequiredDlcs.Remove(requiredDLC))
            {
                catalog.ChangeNotes.AddUpdatedMod(catalogMod.SteamID, $"required DLC { Toolkit.ConvertDlcToString(requiredDLC) } removed");
                catalogMod.ExclusionForRequiredDlc.Remove(requiredDLC);
            }
        }


        /// <summary>Add a required mod and its group. Set an exclusion if added by the FileImporter.</summary>
        /// <remarks>If a unknown Steam ID is found, it will be added to the UnknownAssets list for later evaluation.</remarks>
        public static void AddRequiredMod(Catalog catalog, Mod catalogMod, ulong requiredID, bool updatedByImporter)
        {
            if (catalog.IsValidID(requiredID, allowGroup: true) && !catalogMod.RequiredMods.Contains(requiredID))
            {
                catalogMod.RequiredMods.Add(requiredID);

                if (catalog.GetGroup(requiredID) == null)
                {
                    catalog.ChangeNotes.AddUpdatedMod(catalogMod.SteamID, $"required mod { requiredID } added");

                    catalogMod.Successors.Remove(requiredID);
                    catalogMod.Alternatives.Remove(requiredID);
                    catalogMod.Recommendations.Remove(requiredID);

                    if (updatedByImporter)
                    {
                        catalogMod.AddExclusion(requiredID);
                    }

                    if (catalog.IsGroupMember(requiredID))
                    {
                        // Also add the group that requiredID is a member of.
                        AddRequiredMod(catalog, catalogMod, catalog.GetThisModsGroup(requiredID).GroupID, updatedByImporter);
                    }
                }
                else
                {
                    catalog.ChangeNotes.AddUpdatedMod(catalogMod.SteamID, $"required group { requiredID } added");
                }
            }

            else if (catalog.IsValidID(requiredID, shouldExist: false) && !catalog.RequiredAssets.Contains(requiredID))
            {
                if (catalog.AddUnknownAsset(requiredID))
                {
                    Logger.UpdaterLog($"Required item not found, probably an asset: { Toolkit.GetWorkshopUrl(requiredID) } (for { catalogMod.ToString() }).");
                }
            }
        }


        /// <summary>Remove a required mod, including exclusion, group and change notes.</summary>
        public static void RemoveRequiredMod(Catalog catalog, Mod catalogMod, ulong requiredID)
        {
            if (catalogMod.RequiredMods.Remove(requiredID))
            {
                if (catalog.GetGroup(requiredID) == null)
                {
                    catalog.ChangeNotes.AddUpdatedMod(catalogMod.SteamID, $"required Mod { requiredID } removed");

                    // If an exclusion exists remove it, otherwise add it to prevent the required mod from returning.
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
                        // Remove the group the required mod was a member of, if none of the other group members is a required mod.
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
                else
                {
                    catalog.ChangeNotes.AddUpdatedMod(catalogMod.SteamID, $"required Group { requiredID } removed");
                }
            }
        }


        // Add a successor.
        public static void AddSuccessor(Catalog catalog, Mod catalogMod, ulong successorID)
        {
            if (!catalogMod.Successors.Contains(successorID))
            {
                catalogMod.Successors.Add(successorID);
                catalog.ChangeNotes.AddUpdatedMod(catalogMod.SteamID, $"successor { successorID } added");

                catalogMod.RequiredMods.Remove(successorID);
                catalogMod.Alternatives.Remove(successorID);
                catalogMod.Recommendations.Remove(successorID);
            }
        }


        // Remove a successor.
        public static void RemoveSuccessor(Catalog catalog, Mod catalogMod, ulong successorID)
        {
            if (catalogMod.Successors.Remove(successorID))
            {
                catalog.ChangeNotes.AddUpdatedMod(catalogMod.SteamID, $"successor { successorID } removed");
            }
        }


        // Add an alternative.
        public static void AddAlternative(Catalog catalog, Mod catalogMod, ulong alternativeID)
        {
            if (!catalogMod.Alternatives.Contains(alternativeID))
            {
                catalogMod.Alternatives.Add(alternativeID);
                catalog.ChangeNotes.AddUpdatedMod(catalogMod.SteamID, $"alternative { alternativeID } added");

                catalogMod.RequiredMods.Remove(alternativeID);
                catalogMod.Successors.Remove(alternativeID);
                catalogMod.Recommendations.Remove(alternativeID);
            }
        }


        // Remove an alternative.
        public static void RemoveAlternative(Catalog catalog, Mod catalogMod, ulong alternativeID)
        {
            if (catalogMod.Alternatives.Remove(alternativeID))
            {
                catalog.ChangeNotes.AddUpdatedMod(catalogMod.SteamID, $"alternative { alternativeID } removed");
            }
        }


        // Add a recommendation.
        public static void AddRecommendation(Catalog catalog, Mod catalogMod, ulong recommendationID)
        {
            if (!catalogMod.Recommendations.Contains(recommendationID))
            {
                catalogMod.Recommendations.Add(recommendationID);
                catalog.ChangeNotes.AddUpdatedMod(catalogMod.SteamID, $"recommendation { recommendationID } added");

                catalogMod.RequiredMods.Remove(recommendationID);
                catalogMod.Successors.Remove(recommendationID);
                catalogMod.Alternatives.Remove(recommendationID);
            }
        }


        // Remove a recommendation.
        public static void RemoveRecommendation(Catalog catalog, Mod catalogMod, ulong recommendationID)
        {
            if (catalogMod.Recommendations.Remove(recommendationID))
            {
                catalog.ChangeNotes.AddUpdatedMod(catalogMod.SteamID, $"recommendation { recommendationID } removed");

                Group group = catalog.GetThisModsGroup(recommendationID);

                if (group != null)
                {
                    // Remove the group the recommended mod was a member of, if none of the other group members is a recommendation.
                    bool canRemoveGroup = true;

                    foreach (ulong groupMember in group.GroupMembers)
                    {
                        canRemoveGroup = canRemoveGroup && !catalogMod.Recommendations.Contains(groupMember);
                    }

                    if (canRemoveGroup)
                    {
                        RemoveRequiredMod(catalog, catalogMod, group.GroupID);
                    }
                }
            }
        }


        /// <summary>Determines the kind of change between an old and a new value.</summary>
        /// <returns>"added", "removed" or "changed".</returns>
        private static string Change(object oldValue, object newValue)
        {
            return oldValue == default ? "added" : newValue == default ? "removed" : "changed";
        }


        /// <summary>Determines the kind of change between an old and a new value.</summary>
        /// <returns>"added", "removed" or "changed".</returns>
        private static string Change(string oldValue, string newValue)
        {
            return string.IsNullOrEmpty(oldValue) ? "added" : string.IsNullOrEmpty(newValue) ? "removed" : "changed";
        }
    }
}
