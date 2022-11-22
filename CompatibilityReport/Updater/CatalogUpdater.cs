using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CompatibilityReport.CatalogData;
using CompatibilityReport.Settings;
using CompatibilityReport.Settings.ConfigData;
using CompatibilityReport.UI;
using CompatibilityReport.Util;
using UnityEngine;
using Logger = CompatibilityReport.Util.Logger;

namespace CompatibilityReport.Updater
{
    /// <summary>CatalogUpdater uses information gathered by the WebCrawler and FileImporter to update the catalog and save it as a new version, 
    ///          with auto generated change notes.</summary>
    public static class CatalogUpdater
    {
        private static bool hasRun;
        private static DateTime reviewDate;


        /// <summary>Starts the updater, which gathers new mod and compatibility information and save an updated catalog.</summary>
        [Obsolete("Replaced to support reporting progress to UI")]
        public static void Start()
        {

            Logger.Log("Catalog Updater started.");
            Logger.UpdaterLog($"Catalog Updater started. { ModSettings.ModName } version { ModSettings.FullVersion }. " +
                $"Game version { Toolkit.ConvertGameVersionToString(Toolkit.CurrentGameVersion()) }.");

            hasRun = true;
            string tempCsvCombinedFullPath = Path.Combine(ModSettings.WorkPath, ModSettings.TempCsvCombinedFileName);

            // Download the feedback form. Download might fail because of TLS 1.2, but that doesn't matter because we only want to make sure the URL stays active.
            Toolkit.Download(ModSettings.FeedbackFormUrl, tempCsvCombinedFullPath);
            Toolkit.DeleteFile(tempCsvCombinedFullPath);

            Catalog catalog = Catalog.Load();

            if (catalog == null)
            {
                FirstCatalog.Create();
            }
            else
            {
                Logger.UpdaterLog($"Current catalog version { catalog.VersionString() }, created on { catalog.Updated.ToLocalTime():D}, " +
                    $"{ catalog.Updated.ToLocalTime():t}.");

                catalog.NewVersion(DateTime.Now);

                FileImporter.Start(catalog);

                if (!catalog.ChangeNotes.Any())
                {
                    Logger.UpdaterLog("No changes or new additions found. No new catalog created.");
                }
                else
                {
                    // Save the new catalog.
                    UpdateAuthorRetirement(catalog);
                    UpdateCompatibilityModNames(catalog);

                    if (catalog.Version == 2 && catalog.Note.Value == ModSettings.FirstCatalogNote)
                    {
                        SetNote(catalog, "");
                    }

                    string potentialAssets = catalog.GetPotentialAssetsString();
                    if (!string.IsNullOrEmpty(potentialAssets))
                    {
                        Logger.UpdaterLog($"CSV action for adding assets to the catalog (after verification): Add_RequiredAssets { potentialAssets }");
                    }

                    SaveCatalog(catalog);
                }
            }

            // Reload the catalog as a validity check of the newly created catalog. Also get the correct catalog version for the datadumper if no new catalog.
            Logger.Log("Reloading the catalog.");
            catalog = Catalog.Load();

            if (catalog == null)
            {
                Logger.Log("Catalog could not be reloaded.", Logger.Error);
            }
            else
            {
                DataDumper.Start(catalog);
            }

            Toolkit.DeleteFile(ModSettings.TempDownloadFullPath);
            Toolkit.DeleteFile(tempCsvCombinedFullPath);

            Logger.UpdaterLog("Catalog Updater has finished.");
            Logger.Log("Catalog Updater has finished. Closing the catalog.");

            Logger.CloseUpdaterLog();
        }

        /// <summary>Starts the updater, which gathers new mod and compatibility information and save an updated catalog.</summary>
        public static IEnumerator StartWithProgress(ProgressMonitor progressMonitor)
        {
            int maxSteps = 10;
            int currentStep = 1;

            Logger.Log("Catalog Updater started.");
            progressMonitor.PushMessage("Catalog Updater started");
            progressMonitor.UpdateStage(currentStep++, maxSteps);
            
            Logger.UpdaterLog($"Catalog Updater started. { ModSettings.ModName } version { ModSettings.FullVersion }. " +
                $"Game version { Toolkit.ConvertGameVersionToString(Toolkit.CurrentGameVersion()) }.");

            hasRun = true;
            string tempCsvCombinedFullPath = Path.Combine(ModSettings.WorkPath, ModSettings.TempCsvCombinedFileName);

            // Download the feedback form. Download might fail because of TLS 1.2, but that doesn't matter because we only want to make sure the URL stays active.
            Toolkit.Download(ModSettings.FeedbackFormUrl, tempCsvCombinedFullPath);
            Toolkit.DeleteFile(tempCsvCombinedFullPath);

            progressMonitor.PushMessage("Loading Catalog...");
            yield return new WaitForSeconds(1);
            
            Catalog catalog = Catalog.Load(updaterRun: true);

            yield return new WaitForSeconds(0.5f);
            
            progressMonitor.UpdateStage(currentStep++, maxSteps);
            if (catalog == null)
            {
                progressMonitor.PushMessage("<color #ffbf00>Catalog not found. Creating new one.</color>");
                yield return new WaitForSeconds(0.5f);
                maxSteps -= 5;
                progressMonitor.PushMessage("Updated Stage number.");
                
                FirstCatalog.Create();
                
                progressMonitor.UpdateStage(currentStep++, maxSteps);
                yield return new WaitForSeconds(0.5f);
            }
            else
            {
                Logger.UpdaterLog($"Current catalog version { catalog.VersionString() }, created on { catalog.Updated.ToLocalTime():D}, " +
                    $"{ catalog.Updated.ToLocalTime():t}.");

                catalog.NewVersion(DateTime.Now);

                progressMonitor.PushMessage($"Current catalog version <color #f39c12>{ catalog.VersionString() }</color>. Starting CSV file importer");
                yield return new WaitForSeconds(0.5f);
                progressMonitor.UpdateStage(currentStep++, maxSteps);
                
                yield return FileImporter.StartWithProgress(catalog, progressMonitor);

                progressMonitor.UpdateStage(currentStep++, maxSteps);
                if (!catalog.ChangeNotes.Any())
                {
                    progressMonitor.PushMessage("<color #00ff00>No changes or new additions found. No new catalog created.</color>");
                    maxSteps -= 3;
                    progressMonitor.PushMessage("Updated Stage number.");
                    
                    progressMonitor.UpdateStage(currentStep++, maxSteps);
                    yield return new WaitForSeconds(0.5f);
                    
                    Logger.UpdaterLog("No changes or new additions found. No new catalog created.");
                }
                else
                {
                    progressMonitor.PushMessage("Updating Author Retirement...");
                    yield return new WaitForSeconds(0.5f);
                    progressMonitor.UpdateStage(currentStep++, maxSteps);
                    
                    // Save the new catalog.
                    yield return UpdateAuthorRetirementWithProgress(catalog, progressMonitor);
                    
                    progressMonitor.PushMessage("Updating Compatibility Mod Names...");
                    yield return new WaitForSeconds(0.5f);
                    progressMonitor.UpdateStage(currentStep++, maxSteps);
                    
                    UpdateCompatibilityModNames(catalog);

                    if (catalog.Version == 2 && catalog.Note.Value == ModSettings.FirstCatalogNote)
                    {
                        progressMonitor.PushMessage("Setting new empty Catalog note...");
                        SetNote(catalog, "");
                    }

                    progressMonitor.PushMessage("Saving Catalog...");
                    yield return new WaitForSeconds(0.5f);
                    progressMonitor.UpdateStage(currentStep++, maxSteps);
                    
                    string potentialAssets = catalog.GetPotentialAssetsString();
                    if (!string.IsNullOrEmpty(potentialAssets))
                    {
                        Logger.UpdaterLog($"CSV action for adding assets to the catalog (after verification): Add_RequiredAssets { potentialAssets }");
                    }

                    SaveCatalog(catalog);
                }
            }

            progressMonitor.UpdateStage(currentStep++, maxSteps);
            progressMonitor.PushMessage("Reloading Catalog...");
            yield return new WaitForSeconds(0.5f);
            
            // Reload the catalog as a validity check of the newly created catalog. Also get the correct catalog version for the datadumper if no new catalog.
            Logger.Log("Reloading the catalog.");
            catalog = Catalog.Load(updaterRun: true);

            progressMonitor.UpdateStage(currentStep++, maxSteps);
            if (catalog == null)
            {
                progressMonitor.PushMessage("Catalog could not be reloaded.");
                Logger.Log("Catalog could not be reloaded.", Logger.Error);
            }
            else
            {
                progressMonitor.PushMessage("Starting DataDumper...");
                yield return new WaitForSeconds(0.5f);
                DataDumper.Start(catalog);
            }

            progressMonitor.PushMessage("Finalizing...");
            progressMonitor.UpdateStage(currentStep, maxSteps);
            yield return new WaitForSeconds(1f);
            
            Toolkit.DeleteFile(ModSettings.TempDownloadFullPath);
            Toolkit.DeleteFile(tempCsvCombinedFullPath);

            Logger.UpdaterLog("Catalog Updater has finished.");
            Logger.Log("Catalog Updater has finished. Closing the catalog.");

            Logger.CloseUpdaterLog();

            yield return new WaitForSeconds(3);
            progressMonitor.Dispose();
        }


        /// <summary>Saves the updated catalog to disk.</summary>
        /// <remarks>The change notes, a combined CSV file and the updater log file are saved next to the catalog. 
        ///          This will not upload the catalog to the download location.</remarks>
        /// <returns>True if a new catalog was saved, otherwise false.</returns>
        public static bool SaveCatalog(Catalog catalog)
        {
            catalog.ChangeNotes.ConvertUpdated(catalog);

            string partialPath = Path.Combine(ModSettings.UpdaterPath, $"{ ModSettings.InternalName }_Catalog_v{ catalog.VersionString() }");

            if (catalog.Save($"{ partialPath }.xml"))
            {
                Logger.UpdaterLog($"New catalog { catalog.VersionString() } created and change notes saved.");

                Toolkit.SaveToFile(catalog.ChangeNotes.Combined(catalog), $"{ partialPath }_ChangeNotes.txt");
                Toolkit.MoveFile(Path.Combine(ModSettings.WorkPath, ModSettings.TempCsvCombinedFileName), $"{ partialPath }_Imports.csv.txt");
                Toolkit.CopyFile(Path.Combine(ModSettings.UpdaterPath, ModSettings.UpdaterLogFileName), $"{ partialPath }_Updater.log");
                return true;
            }
            else
            {
                Logger.UpdaterLog("Could not save the new catalog. All updates were lost.", Logger.Error);
                return false;
            }
        }


        /// <summary>Sets the review date.</summary>
        /// <remarks>The review date is used for the mod ReviewDate and/or AutoReviewDate properties.</remarks>
        public static void SetReviewDate(DateTime newDate)
        {
            reviewDate = newDate == default ? reviewDate : Toolkit.CleanDateTime(newDate);
        }


        /// <summary>Sets a new catalog note.</summary>
        /// <remarks>Creates an entry for the change notes.</remarks>
        public static void SetNote(Catalog catalog, string newCatalogNote)
        {
            catalog.ChangeNotes.AppendCatalogChange($"Catalog note { Toolkit.GetChange(catalog.Note, newCatalogNote) }.");
            catalog.Update(note: new TextElement(){Value = newCatalogNote});
        }


        /// <summary>Sets a new report header text for the catalog.</summary>
        /// <remarks>Creates an entry for the change notes.</remarks>
        public static void SetHeaderText(Catalog catalog, string newText)
        {
            catalog.ChangeNotes.AppendCatalogChange($"Catalog header text { Toolkit.GetChange(catalog.ReportHeaderText, newText) }.");
            catalog.Update(reportHeaderText: newText);
        }


        /// <summary>Sets a new report footer text for the catalog.</summary>
        /// <remarks>Creates an entry for the change notes.</remarks>
        public static void SetFooterText(Catalog catalog, string newText)
        {
            catalog.ChangeNotes.AppendCatalogChange($"Catalog footer text { Toolkit.GetChange(catalog.ReportFooterText, newText) }.");
            catalog.Update(reportFooterText: newText);
        }


        /// <summary>Adds a mod to the catalog.</summary>
        /// <remarks>Creates an entry for the change notes.</remarks>
        /// <returns>A reference to the new mod.</returns>
        public static Mod AddMod(Catalog catalog, ulong steamID, string name, bool incompatible = false, bool unlisted = false, bool removed = false)
        {
            Mod newMod = catalog.AddMod(steamID);
            newMod.Update(name: name);
            newMod.AddChangeNote($"{ Toolkit.DateString(catalog.Updated) }: added{ (unlisted ? " as unlisted" : removed ? " as removed" : "") }");

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

            catalog.ChangeNotes.AppendNewMod($"Added { modType } { newMod.ToString() }");
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
                                     Enums.Stability stability = default,
                                     ElementWithId stabilityNote = null,
                                     ElementWithId note = null,
                                     string gameVersionString = null,
                                     string sourceUrl = null,
                                     bool updatedByImporter = false)
        {
            bool hideNote = catalogMod.AddedThisSession && !updatedByImporter;

            // Collect change notes for all changed values. For stability note only if stability itself is unchanged.
            string addedChangeNote =
                (string.IsNullOrEmpty(name) || name == catalogMod.Name ? "" : $", name { Toolkit.GetChange(catalogMod.Name, name) }") +
                // No change note for published
                (updated == default || updated == catalogMod.Updated || hideNote ? "" : ", new update") +
                (authorID == 0 || authorID == catalogMod.AuthorID || catalogMod.AuthorID != 0 || hideNote ? "" : ", author ID added") +
                (authorUrl == null || authorUrl == catalogMod.AuthorUrl || hideNote ? "" : 
                    $", author URL { Toolkit.GetChange(catalogMod.AuthorUrl, authorUrl) }") +
                (stability != default && stability != catalogMod.Stability ? $", stability { Toolkit.GetChange(catalogMod.Stability, stability) }" :
                    (stabilityNote == null || stabilityNote == catalogMod.StabilityNote ? "" : 
                        $", stability note { Toolkit.GetChange(catalogMod.StabilityNote, stabilityNote) }")) +
                (note == null || note == catalogMod.Note ? "" : $", note { Toolkit.GetChange(catalogMod.Note, note) }") +
                (gameVersionString == null || gameVersionString == catalogMod.GameVersionString ? "" : 
                    $", compatible game version { Toolkit.GetChange(catalogMod.GameVersion(), Toolkit.ConvertToVersion(gameVersionString)) }") +
                (sourceUrl == null || sourceUrl == catalogMod.SourceUrl ? "" : $", source URL { Toolkit.GetChange(catalogMod.SourceUrl, sourceUrl) }");

            catalog.ChangeNotes.AddUpdatedMod(catalogMod.SteamID, (string.IsNullOrEmpty(addedChangeNote) ? "" : addedChangeNote.Substring(2)));

            DateTime modReviewDate = updatedByImporter && reviewDate > catalogMod.ReviewDate ? reviewDate : default;
            DateTime modAutoReviewDate = !updatedByImporter && reviewDate > catalogMod.AutoReviewDate ? reviewDate : default;

            if (updatedByImporter && sourceUrl != null && sourceUrl != catalogMod.SourceUrl)
            {
                // Add exclusion on new or changed URL, and swap exclusion on removal.
                catalogMod.UpdateExclusions(exclusionForSourceUrl: sourceUrl != "" || !catalogMod.ExclusionForSourceUrl);
            }
            if (updatedByImporter && gameVersionString != null && gameVersionString != catalogMod.GameVersionString)
            {
                // Add exclusion on new or changed game version, and remove the exclusion on removal of the game version.
                catalogMod.UpdateExclusions(exclusionForGameVersion: gameVersionString != "");
            }

            catalogMod.Update(string.IsNullOrEmpty(name) ? null : name, published, updated, authorID, authorUrl, stability, stabilityNote, note, 
                gameVersionString, sourceUrl, reviewDate: modReviewDate, autoReviewDate: modAutoReviewDate);

            // Remove the Abandoned status if the mod had a new update.
            if (addedChangeNote.Contains("new update")) 
            {
                RemoveStatus(catalog, catalogMod, Enums.Status.Abandoned, updatedByImporter);
            }

            // Update the authors last seen date if the mod had a new update.
            Author modAuthor = catalog.GetAuthor(catalogMod.AuthorID, catalogMod.AuthorUrl);
            if (modAuthor != null && catalogMod.Updated > modAuthor.LastSeen)
            {
                UpdateAuthor(catalog, modAuthor, lastSeen: catalogMod.Updated, updatedByImporter: updatedByImporter);
            }
        }


        /// <summary>Adds a group to the catalog.</summary>
        /// <remarks>Creates an entries for the change notes, for the group and the group members.</remarks>
        public static void AddGroup(Catalog catalog, string groupName, List<ulong> groupMembers)
        {
            Group newGroup = catalog.AddGroup(groupName);
            catalog.ChangeNotes.AppendNewGroup($"Added { newGroup.ToString() }");

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
            if (!catalog.RemoveGroup(oldGroup))
            {
                return false;
            }

            catalog.ChangeNotes.AppendRemovedGroup($"Removed { oldGroup.ToString() }");

            foreach (ulong groupMember in oldGroup.GroupMembers)
            {
                catalog.ChangeNotes.AddUpdatedMod(groupMember, $"removed from { oldGroup.ToString() }");
            }

            return true;
        }


        /// <summary>Adds a group member.</summary>
        /// <remarks>Creates an entry for the change notes.</remarks>
        public static void AddGroupMember(Catalog catalog, Group catalogGroup, ulong groupMember)
        {
            catalogGroup.AddMember(groupMember);
            catalog.ChangeNotes.AddUpdatedMod(groupMember, $"added to { catalogGroup.ToString() }");
        }


        /// <summary>Removes a group member and removes the group if it was the last group member.</summary>
        /// <remarks>Creates an entry for the change notes.</remarks>
        /// <returns>True if removal succeeded, false if not.</returns>
        public static bool RemoveGroupMember(Catalog catalog, Group catalogGroup, ulong groupMember)
        {
            if (!catalogGroup.RemoveMember(groupMember))
            {
                return false;
            }

            catalog.ChangeNotes.AddUpdatedMod(groupMember, $"removed from { catalogGroup.ToString() }");

            if (!catalogGroup.GroupMembers.Any()) 
            {
                return RemoveGroup(catalog, catalogGroup);
            }

            return true;
        }


        /// <summary>Adds a compatibility to the catalog.</summary>
        /// <remarks>Creates an entry for the change notes.</remarks>
        public static void AddCompatibility(Catalog catalog, ulong firstModID, ulong secondModID, Enums.CompatibilityStatus status, ElementWithId note)
        {
            catalog.AddCompatibility(firstModID, secondModID, status, note);

            catalog.ChangeNotes.AppendNewCompatibility($"Added compatibility between { Toolkit.CutOff(catalog.GetMod(firstModID).ToString(), 45), -45 } and " +
                $"{ Toolkit.CutOff(catalog.GetMod(secondModID).ToString(), 45), -45 }: { status }" +
                (string.IsNullOrEmpty(note.Value) ? "" : $", { Toolkit.CutOff(note.Value, ModSettings.TextReportWidth) }"));
        }


        /// <summary>Removes a compatibility from the catalog.</summary>
        /// <remarks>Creates an entry for the change notes.</remarks>
        /// <returns>True if removal succeeded, false if not.</returns>
        public static bool RemoveCompatibility(Catalog catalog, Compatibility oldCompatibility)
        {
            if (!catalog.RemoveCompatibility(oldCompatibility))
            {
                return false;
            }

            catalog.ChangeNotes.AppendRemovedCompatibility("Removed compatibility between " +
                $"{ Toolkit.CutOff(catalog.GetMod(oldCompatibility.FirstModID).ToString(), 45), -45 } and " +
                $"{ Toolkit.CutOff(catalog.GetMod(oldCompatibility.SecondModID).ToString(), 45), -45 }: { oldCompatibility.Status }" +
                (oldCompatibility.Note == null || string.IsNullOrEmpty(oldCompatibility.Note.Value) ? "" : $", { oldCompatibility.Note.Value }"));

            return true;
        }


        /// <summary>Adds an author to the catalog.</summary>
        /// <remarks>Creates an entry for the change notes.</remarks>
        /// <returns>A reference to the new author.</returns>
        public static Author AddAuthor(Catalog catalog, ulong authorID, string authorUrl, string name, bool retired = false)
        {
            Author catalogAuthor = catalog.AddAuthor(authorID, authorUrl);

            CheckDuplicateName(catalog, catalogAuthor, name);
            catalogAuthor.Update(name: name, retired: retired);

            catalogAuthor.AddChangeNote($"{ Toolkit.DateString(catalog.Updated) }: added{ (retired ? " as retired" : "") }");
            catalog.ChangeNotes.AppendNewAuthor($"Added { (retired ? "retired " : "") }author { catalogAuthor.ToString() }");

            return catalogAuthor;
        }


        /// <summary>Updates one or more author properties.</summary>
        /// <remarks>Creates an entry for the change notes.</remarks>
        public static void UpdateAuthor(Catalog catalog, Author catalogAuthor, ulong authorID = 0, string authorUrl = null, string name = null,
            DateTime lastSeen = default, bool? retired = null, bool updatedByImporter = true)
        {
            if (authorID != 0 && authorID != catalogAuthor.SteamID && catalog.GetAuthor(authorID, "") != null)
            {
                Logger.UpdaterLog($"Duplicate author ID found for \"{ catalog.GetAuthor(authorID, "").ToString() }\" and \"{ catalogAuthor.ToString() }\". " +
                    "The author ID is not updated for the second author.", Logger.Error);

                authorID = 0;
            }

            if (authorUrl != null && authorUrl != catalogAuthor.CustomUrl && catalog.GetAuthor(0, authorUrl) != null)
            {
                Logger.UpdaterLog($"Duplicate author URL found for \"{ catalog.GetAuthor(0, authorUrl).ToString() }\" and \"{ catalogAuthor.ToString() }\". " +
                    "The author URL is not updated for the second author.", Logger.Error);

                authorUrl = null;
            }

            bool hideNote = catalogAuthor.AddedThisSession && !updatedByImporter;
            string oldUrl = catalogAuthor.CustomUrl;

            // Collect change notes for all changed values.
            string addedChangeNote =
                (authorID == 0 || authorID == catalogAuthor.SteamID || catalogAuthor.SteamID != 0 ? "" : ", Steam ID added") +
                (authorUrl == null || authorUrl == catalogAuthor.CustomUrl ? "" : $", custom URL { Toolkit.GetChange(catalogAuthor.CustomUrl, authorUrl) }") +
                (name == null || name == catalogAuthor.Name ? "" : $", name { Toolkit.GetChange(catalogAuthor.Name, name) }") +
                (lastSeen == default || lastSeen == catalogAuthor.LastSeen || hideNote ? "" : 
                    $", last seen date { Toolkit.GetChange(lastSeen, catalogAuthor.LastSeen) }") +
                (retired == null || retired == catalogAuthor.Retired ? "" : $", { (retired == true ? "now" : "no longer") } retired");

            // Set an exclusion if retired was set to true here or reset it if retired was set to false. Exclusion will be re-evaluated at UpdateAuthorRetirement().
            catalogAuthor.Update(catalogAuthor.SteamID == 0 ? authorID : 0, authorUrl, name: null, lastSeen, retired, exclusionForRetired: retired);
            catalog.UpdateAuthorIndexes(catalogAuthor, oldUrl);

            if (name != null && name != catalogAuthor.Name)
            {
                CheckDuplicateName(catalog, catalogAuthor, name);
                catalogAuthor.Update(name: name);
            }

            catalog.ChangeNotes.AddUpdatedAuthor(catalogAuthor, (string.IsNullOrEmpty(addedChangeNote) ? "" : addedChangeNote.Substring(2)));

            if (addedChangeNote.Contains("Steam ID") || addedChangeNote.Contains("custom URL"))
            {
                // Update the author ID and URL for all mods from this author, including additional changes notes.
                oldUrl = (oldUrl == catalogAuthor.CustomUrl) ? null : oldUrl;

                List<Mod> ModsFromSameAuthor = catalog.Mods.FindAll(x => 
                    (catalogAuthor.SteamID != 0 && x.AuthorID == catalogAuthor.SteamID) || 
                    (!string.IsNullOrEmpty(catalogAuthor.CustomUrl) && x.AuthorUrl == catalogAuthor.CustomUrl) || 
                    (!string.IsNullOrEmpty(oldUrl) && x.AuthorUrl == oldUrl));

                foreach (Mod ModFromSameAuthor in ModsFromSameAuthor)
                {
                    UpdateMod(catalog, ModFromSameAuthor, authorID: catalogAuthor.SteamID, authorUrl: catalogAuthor.CustomUrl, updatedByImporter: updatedByImporter);
                }
            }
        }


        /// <summary>Checks if an author exists with this name already. Check must be made before changing the author to the new name.</summary>
        /// <remarks>Two authors with the same name could be an existing author we missed.  There are Steam authors with the same name though.</remarks>
        private static void CheckDuplicateName(Catalog catalog, Author catalogAuthor, string newName)
        {
            Author namesake = catalog.Authors.Find(x => x.Name == newName);
            if (namesake != default && (!catalog.SuppressedWarnings.Contains(catalogAuthor.SteamID) || !catalog.SuppressedWarnings.Contains(namesake.SteamID)))
            {
                string authors = $"{ (catalogAuthor.SteamID == 0 ? catalogAuthor.CustomUrl : $"{ catalogAuthor.SteamID }") } and " +
                    $"{ (namesake.SteamID == 0 ? namesake.CustomUrl : $"{ namesake.SteamID }") }";
                Logger.UpdaterLog($"Found two authors with the name \"{ newName }\": { authors }. This could be a coincidence or an error.", Logger.Warning);
            }
        }


        /// <summary>Retires authors eligible due to last seen date, and authors without mods in the Steam Workshop. Un-retires others.</summary>
        /// <remarks>Resets exclusion when no longer needed. This creates change notes entries for authors that no longer have mods in the Steam Workshop.</remarks>
        [Obsolete("Replaced to support reporting progress to UI")]
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

            UpdaterConfig updaterConfig = GlobalConfig.Instance.UpdaterConfig;
            foreach (Author catalogAuthor in catalog.Authors)
            {
                bool oldEnoughForRetirement = catalogAuthor.LastSeen.AddDays(updaterConfig.DaysOfInactivityToRetireAuthor) < DateTime.Today;
                bool authorWithoutMods = !IDsOfAuthorsWithMods.Contains(catalogAuthor.SteamID) && !UrlsOfAuthorsWithMods.Contains(catalogAuthor.CustomUrl);

                if (authorWithoutMods)
                {
                    if (!catalogAuthor.Retired)
                    {
                        UpdateAuthor(catalog, catalogAuthor, retired: true);
                        catalog.ChangeNotes.AddUpdatedAuthor(catalogAuthor, "no longer has mods on the Steam Workshop");
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


        /// <summary>Retires authors eligible due to last seen date, and authors without mods in the Steam Workshop. Un-retires others.</summary>
        /// <remarks>Resets exclusion when no longer needed. This creates change notes entries for authors that no longer have mods in the Steam Workshop.</remarks>
        private static IEnumerator UpdateAuthorRetirementWithProgress(Catalog catalog, ProgressMonitor progressMonitor)
        {
            List<ulong> IDsOfAuthorsWithMods = new List<ulong>();
            List<string> UrlsOfAuthorsWithMods = new List<string>();

            int counter = 0;
            progressMonitor.StartProgress(catalog.Mods.Count, $"Preparing ids and urls. <color #00ff00>{catalog.Mods.Count}</color> mods");
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
                
                if (++counter % 100 == 0)
                {
                    progressMonitor.ReportProgress(counter, $"Scanned {counter} of {catalog.Mods.Count} mods");
                }
            }
            
            progressMonitor.ReportProgress(catalog.Mods.Count, $"Scan complete. Found {IDsOfAuthorsWithMods.Count} authors and {UrlsOfAuthorsWithMods} urls");
            yield return new WaitForSeconds(0.5f);

            counter = 0;
            int updated = 0;
            progressMonitor.PushMessage("Updating retirement...");
            progressMonitor.StartProgress(catalog.Authors.Count, $"Updating retirement of {catalog.Authors.Count} authors");
            
            UpdaterConfig updaterConfig = GlobalConfig.Instance.UpdaterConfig;
            foreach (Author catalogAuthor in catalog.Authors)
            {
                bool oldEnoughForRetirement = catalogAuthor.LastSeen.AddDays(updaterConfig.DaysOfInactivityToRetireAuthor) < DateTime.Today;
                bool authorWithoutMods = !IDsOfAuthorsWithMods.Contains(catalogAuthor.SteamID) && !UrlsOfAuthorsWithMods.Contains(catalogAuthor.CustomUrl);

                if (authorWithoutMods)
                {
                    if (!catalogAuthor.Retired)
                    {
                        UpdateAuthor(catalog, catalogAuthor, retired: true);
                        updated++;
                        catalog.ChangeNotes.AddUpdatedAuthor(catalogAuthor, "no longer has mods on the Steam Workshop");
                    }
                    catalogAuthor.Update(exclusionForRetired: false);
                }
                else if (oldEnoughForRetirement)
                {
                    if (!catalogAuthor.Retired)
                    {
                        UpdateAuthor(catalog, catalogAuthor, retired: true);
                        updated++;
                    }
                    catalogAuthor.Update(exclusionForRetired: false);
                }
                else if (!oldEnoughForRetirement && catalogAuthor.Retired && !catalogAuthor.ExclusionForRetired)
                {
                    UpdateAuthor(catalog, catalogAuthor, retired: false);
                    updated++;
                }
                else if (!oldEnoughForRetirement && !catalogAuthor.Retired)
                {
                    catalogAuthor.Update(exclusionForRetired: false);
                    updated++;
                }

                if (++counter % 100 == 0)
                {
                    progressMonitor.ReportProgress(counter, $"Updated {counter} of {catalog.Authors.Count} authors");
                }
            }
            
            progressMonitor.ReportProgress(catalog.Authors.Count, $"Update complete.");
            progressMonitor.PushMessage($"Update complete. Updated <color #00ff00>{updated}</color> of <color #16a085>{catalog.Authors.Count}</color> authors");
        }


        /// <summary>Updates the mod names in all compatilibities.</summary>
        /// <remarks>The mod names in compatibilities are only for catalog readability, and are not used anywhere.</remarks>
        private static void UpdateCompatibilityModNames(Catalog catalog)
        {
            foreach (Compatibility compatibility in catalog.Compatibilities)
            {
                compatibility.UpdateModNames(catalog.GetMod(compatibility.FirstModID).Name, catalog.GetMod(compatibility.SecondModID).Name);
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
            else if (status == Enums.Status.Obsolete || status == Enums.Status.Deprecated || status == Enums.Status.Abandoned)
            {
                catalogMod.Statuses.Remove(Enums.Status.Obsolete);
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
            
            if (updatedByImporter)
            {
                catalogMod.Update(reviewDate: reviewDate > catalogMod.ReviewDate ? reviewDate : default);
            }
            
            catalog.ChangeNotes.AddUpdatedMod(catalogMod.SteamID, $"{ status } added");
        }


        /// <summary>Removes a status from a mod.</summary>
        /// <remarks>This also (re)sets some exclusions when needed and creates an entry for the change notes.</remarks>
        /// <returns>True if removal succeeded, false if not.</returns>
        public static bool RemoveStatus(Catalog catalog, Mod catalogMod, Enums.Status status, bool updatedByImporter = false)
        {
            bool success = catalogMod.Statuses.Remove(status);

            if (success)
            {
                catalog.ChangeNotes.AddUpdatedMod(catalogMod.SteamID, $"{ status } removed");

                if (updatedByImporter)
                {
                    if (status == Enums.Status.NoDescription)
                    {
                        // If there was an exclusion, remove it, otherwise add it.
                        catalogMod.UpdateExclusions(exclusionForNoDescription: !catalogMod.ExclusionForNoDescription);
                    }

                    catalogMod.Update(reviewDate: reviewDate > catalogMod.ReviewDate ? reviewDate : default);
                }
            }

            return success;
        }


        /// <summary>Adds a required DLC.</summary>
        /// <remarks>This also sets an exclusion if needed and creates an entry for the change notes.</remarks>
        public static void AddRequiredDlc(Catalog catalog, Mod catalogMod, Enums.Dlc requiredDlc, bool updatedByImporter = false)
        {
            if (catalogMod.AddRequiredDlc(requiredDlc))
            {
                catalog.ChangeNotes.AddUpdatedMod(catalogMod.SteamID, $"required DLC { Toolkit.ConvertDlcToString(requiredDlc) } added");

                if (updatedByImporter)
                {
                    catalogMod.AddExclusion(requiredDlc);
                    catalogMod.Update(reviewDate: reviewDate > catalogMod.ReviewDate ? reviewDate : default);
                }
            }
        }


        /// <summary>Removes a required DLC.</summary>
        /// <remarks>This also removes the related exclusion and creates an entry for the change notes.</remarks>
        public static void RemoveRequiredDlc(Catalog catalog, Mod catalogMod, Enums.Dlc requiredDlc, bool updatedByImporter = false)
        {
            if (catalogMod.RemoveRequiredDlc(requiredDlc))
            {
                catalog.ChangeNotes.AddUpdatedMod(catalogMod.SteamID, $"required DLC { Toolkit.ConvertDlcToString(requiredDlc) } removed");
                catalogMod.RemoveExclusion(requiredDlc);

                if (updatedByImporter)
                {
                    catalogMod.Update(reviewDate: reviewDate > catalogMod.ReviewDate ? reviewDate : default);
                }
            }
        }


        /// <summary>Adds a required mod to a mod.</summary>
        /// <remarks>Sets an exclusion if needed and creates an entry for the change notes. If a unknown Steam ID is found, 
        ///          it is probably an asset and will be added to the PotentialAssets list for later evaluation.</remarks>
        public static void AddRequiredMod(Catalog catalog, Mod catalogMod, ulong requiredID, bool updatedByImporter = false)
        {
            if (catalog.IsValidID(requiredID) && !catalogMod.RequiredMods.Contains(requiredID))
            {
                catalogMod.AddRequiredMod(requiredID);

                catalog.ChangeNotes.AddUpdatedMod(catalogMod.SteamID, $"required mod { requiredID } added");

                catalogMod.RemoveSuccessor(requiredID);
                catalogMod.RemoveAlternative(requiredID);
                catalogMod.RemoveRecommendation(requiredID);

                if (updatedByImporter)
                {
                    catalogMod.AddExclusion(requiredID);
                    catalogMod.Update(reviewDate: reviewDate > catalogMod.ReviewDate ? reviewDate : default);
                }
            }
            else if (catalog.IsValidID(requiredID, shouldExist: false) && !catalog.RequiredAssets.Contains(requiredID) && catalog.AddPotentialAsset(requiredID))
            {
                Logger.UpdaterLog($"Required item not found, probably an asset: { Toolkit.GetWorkshopUrl(requiredID) } (for { catalogMod.ToString() }).");
            }
        }


        /// <summary>Removes a required mod from a mod.</summary>
        /// <remarks>(Re)sets the related exclusion and creates an entry for the change notes.</remarks>
        public static void RemoveRequiredMod(Catalog catalog, Mod catalogMod, ulong requiredID, bool updatedByImporter = false)
        {
            if (catalogMod.RemoveRequiredMod(requiredID))
            {
                catalog.ChangeNotes.AddUpdatedMod(catalogMod.SteamID, $"required Mod { requiredID } removed");

                // If an exclusion exists remove it, otherwise (if updated by the Importer) add it to prevent the required mod from returning.
                if (catalogMod.ExclusionForRequiredMods.Contains(requiredID))
                {
                    catalogMod.RemoveExclusion(requiredID);
                }
                else if (updatedByImporter)
                {
                    catalogMod.AddExclusion(requiredID);
                }

                if (updatedByImporter)
                {
                    catalogMod.Update(reviewDate: reviewDate > catalogMod.ReviewDate ? reviewDate : default);
                }
            }
        }


        /// <summary>Adds a successor to a mod.</summary>
        /// <remarks>Creates an entry for the change notes.</remarks>
        public static void AddSuccessor(Catalog catalog, Mod catalogMod, ulong successorID, bool updatedByImporter = false)
        {
            if (catalogMod.AddSuccessor(successorID))
            {
                catalog.ChangeNotes.AddUpdatedMod(catalogMod.SteamID, $"successor { successorID } added");

                catalogMod.RemoveRequiredMod(successorID);
                catalogMod.RemoveAlternative(successorID);
                catalogMod.RemoveRecommendation(successorID);

                if (updatedByImporter)
                {
                    catalogMod.Update(reviewDate: reviewDate > catalogMod.ReviewDate ? reviewDate : default);
                }
            }
        }


        /// <summary>Removes a successor from a mod.</summary>
        /// <remarks>Creates an entry for the change notes.</remarks>
        public static void RemoveSuccessor(Catalog catalog, Mod catalogMod, ulong successorID, bool updatedByImporter = false)
        {
            if (catalogMod.RemoveSuccessor(successorID))
            {
                catalog.ChangeNotes.AddUpdatedMod(catalogMod.SteamID, $"successor { successorID } removed");

                if (updatedByImporter)
                {
                    catalogMod.Update(reviewDate: reviewDate > catalogMod.ReviewDate ? reviewDate : default);
                }
            }
        }


        /// <summary>Adds an alternative to a mod.</summary>
        /// <remarks>Creates an entry for the change notes.</remarks>
        public static void AddAlternative(Catalog catalog, Mod catalogMod, ulong alternativeID, bool updatedByImporter = false)
        {
            if (catalogMod.AddAlternative(alternativeID))
            {
                catalog.ChangeNotes.AddUpdatedMod(catalogMod.SteamID, $"alternative { alternativeID } added");

                catalogMod.RemoveRequiredMod(alternativeID);
                catalogMod.RemoveSuccessor(alternativeID);
                catalogMod.RemoveRecommendation(alternativeID);

                if (updatedByImporter)
                {
                    catalogMod.Update(reviewDate: reviewDate > catalogMod.ReviewDate ? reviewDate : default);
                }
            }
        }


        /// <summary>Removes an alternative from a mod.</summary>
        /// <remarks>Creates an entry for the change notes.</remarks>
        public static void RemoveAlternative(Catalog catalog, Mod catalogMod, ulong alternativeID, bool updatedByImporter = false)
        {
            if (catalogMod.RemoveAlternative(alternativeID))
            {
                catalog.ChangeNotes.AddUpdatedMod(catalogMod.SteamID, $"alternative { alternativeID } removed");

                if (updatedByImporter)
                {
                    catalogMod.Update(reviewDate: reviewDate > catalogMod.ReviewDate ? reviewDate : default);
                }
            }
        }


        /// <summary>Adds a recommended mod.</summary>
        /// <remarks>Creates an entry for the change notes.</remarks>
        public static void AddRecommendation(Catalog catalog, Mod catalogMod, ulong recommendationID, bool updatedByImporter = false)
        {
            if (catalogMod.AddRecommendation(recommendationID))
            {
                catalog.ChangeNotes.AddUpdatedMod(catalogMod.SteamID, $"recommendation { recommendationID } added");

                catalogMod.RemoveRequiredMod(recommendationID);
                catalogMod.RemoveSuccessor(recommendationID);
                catalogMod.RemoveAlternative(recommendationID);

                if (updatedByImporter)
                {
                    catalogMod.Update(reviewDate: reviewDate > catalogMod.ReviewDate ? reviewDate : default);
                }
            }
        }


        /// <summary>Removes a recommended mod from a mod.</summary>
        /// <remarks>Creates an entry for the change notes.</remarks>
        public static void RemoveRecommendation(Catalog catalog, Mod catalogMod, ulong recommendationID, bool updatedByImporter = false)
        {
            if (catalogMod.RemoveRecommendation(recommendationID))
            {
                catalog.ChangeNotes.AddUpdatedMod(catalogMod.SteamID, $"recommendation { recommendationID } removed");

                if (updatedByImporter)
                {
                    catalogMod.Update(reviewDate: reviewDate > catalogMod.ReviewDate ? reviewDate : default);
                }
            }
        }
    }
}
