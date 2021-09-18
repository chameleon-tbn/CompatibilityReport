using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CompatibilityReport.CatalogData;
using CompatibilityReport.Util;

namespace CompatibilityReport.Updater
{
    /// <summary>CatalogUpdater uses information gathered by the WebCrawler and FileImporter to update the catalog and save it as a new version, 
    ///          with auto generated change notes.</summary>
    public static class CatalogUpdater
    {
        private static bool hasRun;
        private static DateTime reviewDate;


        /// <summary>Starts the updater, which gathers new mod and compatibility information and save an updated catalog.</summary>
        public static void Start()
        {
            // Todo 0.7 Read updater settings file.
            if (hasRun || !ModSettings.UpdaterEnabled)
            {
                return;
            }

            Logger.Log("Catalog Updater started. See separate logfile for details.");
            Logger.UpdaterLog($"Catalog Updater started. { ModSettings.ModName } version { ModSettings.FullVersion }. " +
                $"Game version { Toolkit.ConvertGameVersionToString(Toolkit.CurrentGameVersion()) }.");

            hasRun = true;
            Toolkit.DeleteFile(Path.Combine(ModSettings.WorkPath, ModSettings.TempCsvCombinedFileName));

            Catalog catalog = Catalog.Load();

            if (catalog == null)
            {
                FirstCatalog.Create();
            }
            else
            {
                Logger.UpdaterLog($"Current catalog version { catalog.VersionString() }, created on { catalog.UpdateDate:D}, { catalog.UpdateDate:t}.");
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

                string potentialAssets = catalog.GetPotentialAssetsString();
                if (!string.IsNullOrEmpty(potentialAssets))
                {
                    Logger.UpdaterLog($"CSV action for adding assets to the catalog (after verification): Add_RequiredAssets { potentialAssets }");
                }

                SaveCatalog(catalog);

                DataDumper.Start(catalog);
            }

            Logger.UpdaterLog("Catalog Updater has finished.");
            Logger.Log("Catalog Updater has finished.\n");

            Logger.CloseUpdateLog();
        }


        /// <summary>Saves the updated catalog to disk.</summary>
        /// <remarks>The change notes, a combined CSV file and the updater log file are saved next to the catalog. 
        ///          This will not upload the catalog to the download location.</remarks>
        public static void SaveCatalog(Catalog catalog)
        {
            catalog.ChangeNotes.ConvertUpdated(catalog);

            if (!catalog.ChangeNotes.Any())
            {
                Logger.UpdaterLog("No changes or new additions found. No new catalog created.");
                return;
            }
            
            string partialPath = Path.Combine(ModSettings.UpdaterPath, $"{ ModSettings.InternalName }_Catalog_v{ catalog.VersionString() }");

            if (catalog.Save($"{ partialPath }.xml"))
            {
                Logger.UpdaterLog($"New catalog { catalog.VersionString() } created and change notes saved.");

                Toolkit.SaveToFile(catalog.ChangeNotes.Combined(catalog), $"{ partialPath }_ChangeNotes.txt");
                Toolkit.MoveFile(Path.Combine(ModSettings.WorkPath, ModSettings.TempCsvCombinedFileName), $"{ partialPath }_Imports.csv.txt");
                Toolkit.CopyFile(Path.Combine(ModSettings.UpdaterPath, ModSettings.UpdaterLogFileName), $"{ partialPath }_Updater.log");
            }
            else
            {
                Logger.UpdaterLog("Could not save the new catalog. All updates were lost.", Logger.Error);
            }
        }


        /// <summary>Sets the review date.</summary>
        /// <remarks>The review date is used for the mod ReviewDate and/or AutoReviewDate properties.</remarks>
        public static void SetReviewDate(DateTime newDate)
        {
            reviewDate = newDate == default ? reviewDate : newDate;
        }


        /// <summary>Sets a new catalog note.</summary>
        /// <remarks>Creates an entry for the change notes.</remarks>
        public static void SetNote(Catalog catalog, string newCatalogNote)
        {
            catalog.ChangeNotes.CatalogChanges.AppendLine($"Catalog note { Change(catalog.Note, newCatalogNote) }.");
            catalog.Update(note: newCatalogNote);
        }


        /// <summary>Sets a new report header text for the catalog.</summary>
        /// <remarks>Creates an entry for the change notes.</remarks>
        public static void SetHeaderText(Catalog catalog, string newText)
        {
            catalog.ChangeNotes.CatalogChanges.AppendLine($"Catalog header text { Change(catalog.ReportHeaderText, newText) }.");
            catalog.Update(reportHeaderText: newText);
        }


        /// <summary>Sets a new report footer text for the catalog.</summary>
        /// <remarks>Creates an entry for the change notes.</remarks>
        public static void SetFooterText(Catalog catalog, string newText)
        {
            catalog.ChangeNotes.CatalogChanges.AppendLine($"Catalog footer text { Change(catalog.ReportFooterText, newText) }.");
            catalog.Update(reportFooterText: newText);
        }


        /// <summary>Adds a mod to the catalog.</summary>
        /// <remarks>Creates an entry for the change notes.</remarks>
        /// <returns>A reference to the new mod.</returns>
        public static Mod AddMod(Catalog catalog, ulong steamID, string name, bool incompatible = false, bool unlisted = false, bool removed = false)
        {
            Mod newMod = catalog.AddMod(steamID);
            newMod.Update(name: name);
            newMod.AddChangeNote($"{ Toolkit.DateString(catalog.UpdateDate) }: added");

            string modType = "mod";

            if (incompatible)
            {
                newMod.Update(stability: Enums.Stability.IncompatibleAccordingToWorkshop);
                modType = "incompatible mod";
            }

            if (unlisted || removed)
            {
                newMod.Statuses.Add(unlisted ? Enums.Status.UnlistedInWorkshop : Enums.Status.RemovedFromWorkshop);
                modType = $"{ (unlisted ? "unlisted " : "removed ") }{ modType }";
            }

            catalog.ChangeNotes.NewMods.AppendLine($"Added { modType } { newMod.ToString() }");
            return newMod;
        }


        /// <summary>Updates one or more mod properties.</summary>
        /// <remarks>This also updates some exclusions and the authors last seen date. This also creates an entry for the change notes.</remarks>
        public static void UpdateMod(Catalog catalog,
                                     Mod catalogMod,
                                     string name = null,
                                     DateTime published = default,
                                     DateTime updated = default,
                                     ulong authorID = 0,
                                     string authorUrl = null,
                                     string sourceUrl = null,
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
                (sourceUrl == null || sourceUrl == catalogMod.SourceUrl ? "" : $", source URL { Change(catalogMod.SourceUrl, sourceUrl) }") +
                (compatibleGameVersionString == null || compatibleGameVersionString == catalogMod.CompatibleGameVersionString ? "" : 
                    $", compatible game version { Change(catalogMod.CompatibleGameVersion(), Toolkit.ConvertToVersion(compatibleGameVersionString)) }") +
                (stability != default && stability != catalogMod.Stability ? ", stability changed" :
                    (stabilityNote == null || stabilityNote == catalogMod.StabilityNote ? "" : $", stability note { Change(catalogMod.StabilityNote, stabilityNote) }")) +
                (genericNote == null || genericNote == catalogMod.GenericNote ? "" : $", generic note { Change(catalogMod.GenericNote, genericNote) }");

            catalog.ChangeNotes.ModUpdate(catalogMod.SteamID, (string.IsNullOrEmpty(addedChangeNote) ? "" : addedChangeNote.Substring(2)));

            DateTime modReviewDate = (!string.IsNullOrEmpty(addedChangeNote) || alwaysUpdateReviewDate) && updatedByImporter ? reviewDate : default;
            DateTime modAutoReviewDate = (!string.IsNullOrEmpty(addedChangeNote) || alwaysUpdateReviewDate) && !updatedByImporter ? reviewDate : default;

            if (updatedByImporter && sourceUrl != null && sourceUrl != catalogMod.SourceUrl)
            {
                // Add exclusion on new or changed URL, and swap exclusion on removal.
                catalogMod.UpdateExclusions(exclusionForSourceUrl: sourceUrl != "" || !catalogMod.ExclusionForSourceUrl);
            }
            if (updatedByImporter && compatibleGameVersionString != null && compatibleGameVersionString != catalogMod.CompatibleGameVersionString)
            {
                // Add exclusion on new or changed game version, and remove the exclusion on removal of the game version.
                catalogMod.UpdateExclusions(exclusionForGameVersion: compatibleGameVersionString != "");
            }

            catalogMod.Update(name, published, updated, authorID, authorUrl, sourceUrl, compatibleGameVersionString, stability, stabilityNote, genericNote, 
                reviewDate: modReviewDate, autoReviewDate: modAutoReviewDate);

            // Update the authors last seen date if the mod had a new update.
            Author modAuthor = catalog.GetAuthor(catalogMod.AuthorID, catalogMod.AuthorUrl);
            if (modAuthor != null && catalogMod.Updated > modAuthor.LastSeen)
            {
                UpdateAuthor(catalog, modAuthor, lastSeen: catalogMod.Updated);
            }
        }


        /// <summary>Adds a group to the catalog.</summary>
        /// <remarks>Creates an entries for the change notes, for the group and the group members.</remarks>
        public static void AddGroup(Catalog catalog, string groupName, List<ulong> groupMembers)
        {
            Group newGroup = catalog.AddGroup(groupName);
            catalog.ChangeNotes.NewGroups.AppendLine($"Added { newGroup.ToString() }");

            // Add group members separately to get change notes on all group members.
            foreach (ulong groupMember in groupMembers)
            {
                AddGroupMember(catalog, newGroup, groupMember);
            }
        }


        /// <summary>Removes a group from the catalog.</summary>
        /// <remarks>Creates an entry for the change notes, for the group and the group members.</remarks>
        /// <returns>True if removal succeeded, false if not.</returns>
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
                catalog.ChangeNotes.RemovedGroups.AppendLine($"Removed { oldGroup.ToString() }");

                foreach (ulong groupMember in oldGroup.GroupMembers)
                {
                    catalog.ChangeNotes.ModUpdate(groupMember, $"removed from { oldGroup.ToString() }");
                }
            }

            return success;
        }


        /// <summary>Adds a group member.</summary>
        /// <remarks>Also adds the group as required and recommended mods where the new group member is used. This also creates an entry for the change notes.</remarks>
        public static void AddGroupMember(Catalog catalog, Group catalogGroup, ulong groupMember)
        {
            catalogGroup.GroupMembers.Add(groupMember);
            catalog.ChangeNotes.ModUpdate(groupMember, $"added to { catalogGroup.ToString() }");

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


        /// <summary>Removes a group member and removes the group if it was the last group member.</summary>
        /// <remarks>Also removes the group as required and recommended mods where no group member is used anymore. 
        ///          This also creates an entry for the change notes.</remarks>
        /// <returns>True if removal succeeded, false if not.</returns>
        public static bool RemoveGroupMember(Catalog catalog, Group catalogGroup, ulong groupMember)
        {
            if (!catalogGroup.GroupMembers.Remove(groupMember))
            {
                return false;
            }

            catalog.ChangeNotes.ModUpdate(groupMember, $"removed from { catalogGroup.ToString() }");

            if (!catalogGroup.GroupMembers.Any()) 
            {
                return RemoveGroup(catalog, catalogGroup);
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


        /// <summary>Adds a compatibility to the catalog.</summary>
        /// <remarks>Creates an entry for the change notes.</remarks>
        public static void AddCompatibility(Catalog catalog, ulong firstModID, ulong secondModID, Enums.CompatibilityStatus compatibilityStatus, string compatibilityNote)
        {
            catalog.AddCompatibility(firstModID, secondModID, compatibilityStatus, compatibilityNote);

            catalog.ChangeNotes.NewCompatibilities.AppendLine($"Added compatibility between { firstModID, 10 } and { secondModID, 10 }: { compatibilityStatus }" +
                (string.IsNullOrEmpty(compatibilityNote) ? "" : $", { compatibilityNote }"));
        }


        /// <summary>Removes a compatibility from the catalog.</summary>
        /// <remarks>Creates an entry for the change notes.</remarks>
        /// <returns>True if removal succeeded, false if not.</returns>
        public static bool RemoveCompatibility(Catalog catalog, Compatibility catalogCompatibility)
        {
            if (!catalog.Compatibilities.Remove(catalogCompatibility))
            {
                return false;
            }

            catalog.ChangeNotes.RemovedCompatibilities.AppendLine($"Removed compatibility between { catalogCompatibility.FirstSteamID, 10 } and " +
                $"{ catalogCompatibility.SecondSteamID, 10 }: \"{ catalogCompatibility.Status }\"" +
                (string.IsNullOrEmpty(catalogCompatibility.Note) ? "" : $", { catalogCompatibility.Note }"));

            return true;
        }


        /// <summary>Adds an author to the catalog.</summary>
        /// <remarks>Creates an entry for the change notes.</remarks>
        /// <returns>A reference to the new author.</returns>
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
            catalog.ChangeNotes.NewAuthors.AppendLine($"Added { (retired ? "retired " : "") }author { catalogAuthor.ToString() }");

            return catalogAuthor;
        }


        /// <summary>Updates one or more author properties.</summary>
        /// <remarks>Creates an entry for the change notes.</remarks>
        public static void UpdateAuthor(Catalog catalog, Author catalogAuthor, ulong authorID = 0, string authorUrl = null, string name = null, 
            DateTime lastSeen = default, bool? retired = null)
        {
            string oldUrl = catalogAuthor.CustomUrl;

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
            catalog.ChangeNotes.AuthorUpdate(catalogAuthor, (string.IsNullOrEmpty(addedChangeNote) ? "" : addedChangeNote.Substring(2)));

            if (addedChangeNote.Contains("Steam ID") || addedChangeNote.Contains("Custom URL"))
            {
                // Update the author ID and URL for all mods from this author, without additional changes notes.
                List<Mod> AuthorMods = catalog.Mods.FindAll(x => x.AuthorID == catalogAuthor.SteamID || x.AuthorUrl == catalogAuthor.CustomUrl || x.AuthorUrl == oldUrl);
                foreach (Mod AuthorMod in AuthorMods)
                {
                    AuthorMod.Update(authorID: catalogAuthor.SteamID, authorUrl: catalogAuthor.CustomUrl);
                }
            }
        }


        /// <summary>Retires authors eligible due to last seen date, and authors without mods in the Steam Workshop. Un-retires others.</summary>
        /// <remarks>Resets exclusion when no longer needed. This creates change notes entries for authors that no longer have mods in the Steam Workshop.</remarks>
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
                        UpdateAuthor(catalog, catalogAuthor, retired: true);
                        catalog.ChangeNotes.AuthorUpdate(catalogAuthor, "no longer has mods on the Steam Workshop");
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


        /// <summary>Updates the mod names in all compatilibities.</summary>
        /// <remarks>The mod names in compatibilities are only for catalog readability, and are not used anywhere.</remarks>
        private static void UpdateCompatibilityModNames(Catalog catalog)
        {
            foreach (Compatibility compatibility in catalog.Compatibilities)
            {
                compatibility.UpdateModNames(catalog.GetMod(compatibility.FirstSteamID).Name, catalog.GetMod(compatibility.SecondSteamID).Name);
            }
        }


        /// <summary>Adds a status to a mod.</summary>
        /// <remarks>Removes conflicting statuses and (re)sets some exclusions where needed. This also creates an entry for the change notes.</remarks>
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
            catalog.ChangeNotes.ModUpdate(catalogMod.SteamID, $"{ status } added");
        }


        /// <summary>Removes a status from a mod.</summary>
        /// <remarks>This also (re)sets some exclusions when needed and creates an entry for the change notes.</remarks>
        /// <returns>True if removal succeeded, false if not.</returns>
        public static bool RemoveStatus(Catalog catalog, Mod catalogMod, Enums.Status status, bool updatedByImporter = false)
        {
            bool success = catalogMod.Statuses.Remove(status);

            if (success)
            {
                catalog.ChangeNotes.ModUpdate(catalogMod.SteamID, $"{ status } removed");

                if (status == Enums.Status.NoDescription && updatedByImporter)
                {
                    // If there was an exclusion, remove it, otherwise add it.
                    catalogMod.UpdateExclusions(exclusionForNoDescription: !catalogMod.ExclusionForNoDescription);
                }
            }

            return success;
        }


        /// <summary>Adds a required DLC.</summary>
        /// <remarks>This also sets an exclusion if needed and creates an entry for the change notes.</remarks>
        public static void AddRequiredDlc(Catalog catalog, Mod catalogMod, Enums.Dlc requiredDlc, bool updatedByImporter = false)
        {
            if (!catalogMod.RequiredDlcs.Contains(requiredDlc))
            {
                catalogMod.RequiredDlcs.Add(requiredDlc);
                catalog.ChangeNotes.ModUpdate(catalogMod.SteamID, $"required DLC { Toolkit.ConvertDlcToString(requiredDlc) } added");

                if (updatedByImporter)
                {
                    catalogMod.AddExclusion(requiredDlc);
                }
            }
        }


        /// <summary>Removes a required DLC.</summary>
        /// <remarks>This also removes the related exclusion and creates an entry for the change notes.</remarks>
        public static void RemoveRequiredDlc(Catalog catalog, Mod catalogMod, Enums.Dlc requiredDlc)
        {
            if (catalogMod.RequiredDlcs.Remove(requiredDlc))
            {
                catalog.ChangeNotes.ModUpdate(catalogMod.SteamID, $"required DLC { Toolkit.ConvertDlcToString(requiredDlc) } removed");
                catalogMod.RemoveExclusion(requiredDlc);
            }
        }


        /// <summary>Adds a required mod and its group to a mod.</summary>
        /// <remarks>Sets an exclusion if needed and creates an entry for the change notes. If a unknown Steam ID is found, 
        ///          it is probably an asset and will be added to the PotentialAssets list for later evaluation.</remarks>
        public static void AddRequiredMod(Catalog catalog, Mod catalogMod, ulong requiredID, bool updatedByImporter)
        {
            if (catalog.IsValidID(requiredID, allowGroup: true) && !catalogMod.RequiredMods.Contains(requiredID))
            {
                catalogMod.RequiredMods.Add(requiredID);

                if (catalog.GetGroup(requiredID) == null)
                {
                    catalog.ChangeNotes.ModUpdate(catalogMod.SteamID, $"required mod { requiredID } added");

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
                    catalog.ChangeNotes.ModUpdate(catalogMod.SteamID, $"required group { requiredID } added");
                }
            }

            else if (catalog.IsValidID(requiredID, shouldExist: false) && !catalog.RequiredAssets.Contains(requiredID))
            {
                if (catalog.AddPotentialAsset(requiredID))
                {
                    Logger.UpdaterLog($"Required item not found, probably an asset: { Toolkit.GetWorkshopUrl(requiredID) } (for { catalogMod.ToString() }).");
                }
            }
        }


        /// <summary>Removes a required mod from a mod.</summary>
        /// <remarks>(Re)sets the related exclusion and creates an entry for the change notes. 
        ///          This mods group is also removed if no other group member is required.</remarks>
        public static void RemoveRequiredMod(Catalog catalog, Mod catalogMod, ulong requiredID)
        {
            if (catalogMod.RequiredMods.Remove(requiredID))
            {
                if (catalog.GetGroup(requiredID) == null)
                {
                    catalog.ChangeNotes.ModUpdate(catalogMod.SteamID, $"required Mod { requiredID } removed");

                    // If an exclusion exists remove it, otherwise add it to prevent the required mod from returning.
                    if (catalogMod.ExclusionForRequiredMods.Contains(requiredID))
                    {
                        catalogMod.RemoveExclusion(requiredID);
                    }
                    else
                    {
                        catalogMod.AddExclusion(requiredID);
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
                    catalog.ChangeNotes.ModUpdate(catalogMod.SteamID, $"required Group { requiredID } removed");
                }
            }
        }


        /// <summary>Adds a successor to a mod.</summary>
        /// <remarks>Creates an entry for the change notes.</remarks>
        public static void AddSuccessor(Catalog catalog, Mod catalogMod, ulong successorID)
        {
            if (!catalogMod.Successors.Contains(successorID))
            {
                catalogMod.Successors.Add(successorID);
                catalog.ChangeNotes.ModUpdate(catalogMod.SteamID, $"successor { successorID } added");

                catalogMod.RequiredMods.Remove(successorID);
                catalogMod.Alternatives.Remove(successorID);
                catalogMod.Recommendations.Remove(successorID);
            }
        }


        /// <summary>Removes a successor from a mod.</summary>
        /// <remarks>Creates an entry for the change notes.</remarks>
        public static void RemoveSuccessor(Catalog catalog, Mod catalogMod, ulong successorID)
        {
            if (catalogMod.Successors.Remove(successorID))
            {
                catalog.ChangeNotes.ModUpdate(catalogMod.SteamID, $"successor { successorID } removed");
            }
        }


        /// <summary>Adds an alternative to a mod.</summary>
        /// <remarks>Creates an entry for the change notes.</remarks>
        public static void AddAlternative(Catalog catalog, Mod catalogMod, ulong alternativeID)
        {
            if (!catalogMod.Alternatives.Contains(alternativeID))
            {
                catalogMod.Alternatives.Add(alternativeID);
                catalog.ChangeNotes.ModUpdate(catalogMod.SteamID, $"alternative { alternativeID } added");

                catalogMod.RequiredMods.Remove(alternativeID);
                catalogMod.Successors.Remove(alternativeID);
                catalogMod.Recommendations.Remove(alternativeID);
            }
        }


        /// <summary>Removes an alternative from a mod.</summary>
        /// <remarks>Creates an entry for the change notes.</remarks>
        public static void RemoveAlternative(Catalog catalog, Mod catalogMod, ulong alternativeID)
        {
            if (catalogMod.Alternatives.Remove(alternativeID))
            {
                catalog.ChangeNotes.ModUpdate(catalogMod.SteamID, $"alternative { alternativeID } removed");
            }
        }


        /// <summary>Adds a recommended mod and its group to a mod.</summary>
        /// <remarks>Creates an entry for the change notes.</remarks>
        public static void AddRecommendation(Catalog catalog, Mod catalogMod, ulong recommendationID)
        {
            if (!catalogMod.Recommendations.Contains(recommendationID))
            {
                catalogMod.Recommendations.Add(recommendationID);
                catalog.ChangeNotes.ModUpdate(catalogMod.SteamID, $"recommendation { recommendationID } added");

                catalogMod.RequiredMods.Remove(recommendationID);
                catalogMod.Successors.Remove(recommendationID);
                catalogMod.Alternatives.Remove(recommendationID);
            }

            // Todo 0.4.1 Add the recommended mods group as recommended.
        }


        /// <summary>Removes a recommended mod from a mod.</summary>
        /// <remarks>This mods group is also removed if no other group member is required. Creates an entry for the change notes.</remarks>
        public static void RemoveRecommendation(Catalog catalog, Mod catalogMod, ulong recommendationID)
        {
            if (catalogMod.Recommendations.Remove(recommendationID))
            {
                catalog.ChangeNotes.ModUpdate(catalogMod.SteamID, $"recommendation { recommendationID } removed");

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
        /// <returns>The string "added", "removed" or "changed".</returns>
        private static string Change(object oldValue, object newValue)
        {
            return oldValue == default ? "added" : newValue == default ? "removed" : "changed";
        }


        /// <summary>Determines the kind of change between an old and a new value.</summary>
        /// <returns>The string "added", "removed" or "changed".</returns>
        private static string Change(string oldValue, string newValue)
        {
            return string.IsNullOrEmpty(oldValue) ? "added" : string.IsNullOrEmpty(newValue) ? "removed" : "changed";
        }


        /// <summary>Determines the kind of change between an old and a new value.</summary>
        /// <returns>The string "added", "removed" or "changed".</returns>
        private static string Change(Version oldValue, Version newValue)
        {
            return oldValue == default || oldValue == Toolkit.UnknownVersion() ? "added" : 
                newValue == default || newValue == Toolkit.UnknownVersion() ? "removed" : "changed";
        }
    }
}
