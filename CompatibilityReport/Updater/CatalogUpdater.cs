using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CompatibilityReport.DataTypes;
using CompatibilityReport.Util;


// CatalogUpdater uses information gathered by AutoUpdater and ManualUpdate to update the catalog and save this as a new version, with auto generated change notes.


namespace CompatibilityReport.Updater
{
    internal static class CatalogUpdater        // [Todo 0.3] move actions to AutoUpdater & ManualUpdater, updater directly into catalog, fill changenotes from there
    {
        // Did we run already this session (successful or not)
        private static bool hasRun;

        // Date and time of the catalog, which is always set to 'now'
        private static DateTime catalogDateTime;

        // Date of the review update for related mods and authors; this can be set in CSV file
        private static DateTime reviewDate;

        private static string reviewDateString;

        // Note for the new catalog
        private static string catalogNote;

        // Dictionaries to collect info from the Steam Workshop (AutoUpdater) and the CSV files (ManualUpdater)
        internal static Dictionary<ulong, Mod> CollectedModInfo { get; private set; } = new Dictionary<ulong, Mod>();
        internal static Dictionary<ulong, Author> CollectedAuthorIDs { get; private set; } = new Dictionary<ulong, Author>();
        internal static Dictionary<string, Author> CollectedAuthorURLs { get; private set; } = new Dictionary<string, Author>();
        internal static Dictionary<ulong, Group> CollectedGroupInfo { get; private set; } = new Dictionary<ulong, Group>();
        internal static List<Compatibility> CollectedCompatibilities { get; private set; } = new List<Compatibility>();
        internal static List<ulong> CollectedRemovals { get; private set; } = new List<ulong>();

        // Stringbuilder to gather the combined CSVs, to be saved with the new catalog
        internal static StringBuilder CSVCombined;

        // Change notes, separate parts and combined
        private static StringBuilder changeNotesNewMods = new StringBuilder();
        private static Dictionary<ulong, string> changeNotesUpdatedMods = new Dictionary<ulong, string>();
        private static StringBuilder changeNotesRemovedMods = new StringBuilder();
        private static StringBuilder changeNotesNewAuthors = new StringBuilder();
        private static StringBuilder changeNotesUpdatedAuthors = new StringBuilder();
        private static StringBuilder changeNotesRemovedAuthors = new StringBuilder();
        private static string changeNotes;

        // List of author custom URLs to remove from the catalog; these are collected first and removed later to avoid 'author not found' issues
        private static readonly List<string> authorURLsToRemove = new List<string>();


        // Update the active catalog with the found information; returns the partial path of the new catalog
        internal static void Start()
        {
            // Exit if we ran already, the updater is not enabled in settings, or if we don't have and can't get an active catalog
            if (hasRun || !ModSettings.UpdaterEnabled || !ActiveCatalog.Init())
            {
                return;
            }

            hasRun = true;

            Logger.Log("Catalog Updater started. See separate logfile for details.");

            Logger.UpdaterLog($"Catalog Updater started. { ModSettings.modName } version { ModSettings.fullVersion }. " + 
                $"Game version { GameVersion.Formatted(GameVersion.Current) }. Current catalog version { ActiveCatalog.Instance.VersionString() }.");

            // Initialize the dictionaries, change notes and other variables we need
            Init();

            // Run the AutoUpdater, if enabled
            if (ModSettings.AutoUpdaterEnabled)
            {
                AutoUpdater.Start();
            }
            
            // Run the ManualUpdater
            ManualUpdater.Start();

            // Add or update all found mods
            UpdateAndAddMods();

            // Add or update all found authors
            UpdateAndAddAuthors();

            if (ModSettings.AutoUpdaterEnabled)
            {
                // Retire authors that have no mods on the Steam Workshop anymore; only when running AutoUpdater, otherwise we won't have gathered all mods from Workshop
                RetireFormerAuthors();
            }

            // Add or update all collected groups; only filled by the ManualUpdater
            UpdateAndAddGroups();

            // Add or update all collected compatibilities; only filled by the ManualUpdater
            UpdateAndAddCompatibilities();

            // Remove all items from the collected removal list; only filled by the ManualUpdater
            Removals();

            // Only continue with catalog update if we found any changes to update the catalog
            if (changeNotesNewMods.Length + changeNotesUpdatedMods.Count + changeNotesRemovedMods.Length + 
                changeNotesNewAuthors.Length + changeNotesUpdatedAuthors.Length + changeNotesRemovedAuthors.Length == 0)
            {
                Logger.UpdaterLog("No changes or new additions found. No new catalog created.");
            }
            else
            {
                // Increase the catalog version and update date
                ActiveCatalog.Instance.NewVersion(catalogDateTime);

                // Set a new catalog note; not changed if null
                ActiveCatalog.Instance.Update(note: catalogNote);

                // Combine the change notes
                changeNotes = $"Change Notes for Catalog { ActiveCatalog.Instance.VersionString() }\n" +
                    "-------------------------------\n" +
                    $"{ catalogDateTime:D}, { catalogDateTime:t}\n" +
                    "These change notes were automatically created by the updater process.\n" +
                    "\n" +
                    "\n" +
                    (changeNotesNewMods.Length + changeNotesNewAuthors.Length == 0 ? "" :
                        "*** ADDED: ***\n" +
                        changeNotesNewMods.ToString() +
                        changeNotesNewAuthors.ToString() +
                        "\n") +
                    (changeNotesUpdatedMods.Count + changeNotesUpdatedAuthors.Length == 0 ? "" :
                        "*** UPDATED: ***\n" +
                        changeNotesUpdatedMods.ToString() +     // [Todo 0.3] Needs change because of dictionary
                        changeNotesUpdatedAuthors.ToString() +
                        "\n") +
                    (changeNotesRemovedMods.Length + changeNotesRemovedAuthors.Length == 0 ? "" :
                        "*** REMOVED: ***\n" +
                        changeNotesRemovedMods.ToString() +
                        changeNotesRemovedAuthors.ToString());

                // The filename for the new catalog and related files ('CompatibilityReportCatalog_v1.0001')
                string partialPath = Path.Combine(ModSettings.updaterPath, $"{ ModSettings.internalName }Catalog_v{ ActiveCatalog.Instance.VersionString() }");

                // Save the new catalog
                if (ActiveCatalog.Instance.Save(partialPath + ".xml"))
                {
                    // Save change notes, in the same folder as the new catalog
                    Toolkit.SaveToFile(changeNotes.ToString(), partialPath + "_ChangeNotes.txt");

                    // Save the combined CSVs, in the same folder as the new catalog
                    Toolkit.SaveToFile(CSVCombined.ToString(), partialPath + "_ManualUpdates.csv.txt");

                    Logger.UpdaterLog($"New catalog { ActiveCatalog.Instance.VersionString() } created and change notes saved.");

                    // Copy the updater logfile to the same folder as the new catalog
                    Toolkit.CopyFile(ModSettings.updaterLogfileFullPath, partialPath + "_Updater.log");
                }
                else
                {
                    Logger.UpdaterLog("Could not save the new catalog. All updates were lost.", Logger.error);
                }
            }

            // Empty the dictionaries and change notes to free memory
            Init();

            // Close and reopen the active catalog, because we made changes to it
            Logger.UpdaterLog("Closing and reopening the active catalog.", duplicateToRegularLog: true);

            ActiveCatalog.Close();

            ActiveCatalog.Init();

            // Run the DataDumper
            DataDumper.Start();

            Logger.UpdaterLog("Catalog Updater has finished.", extraLine: true, duplicateToRegularLog: true);
        }


        // Initialize all variables
        private static void Init()
        {
            CollectedModInfo.Clear();
            CollectedAuthorIDs.Clear();
            CollectedAuthorURLs.Clear();
            CollectedGroupInfo.Clear();
            CollectedCompatibilities.Clear();
            CollectedRemovals.Clear();

            authorURLsToRemove.Clear();

            // Setting the note to null instead of empty to avoid accidentally clearing the note when this field is never set
            catalogNote = null;

            changeNotesNewMods = new StringBuilder();
            changeNotesUpdatedMods = new Dictionary<ulong, string>();
            changeNotesRemovedMods = new StringBuilder();
            changeNotesNewAuthors = new StringBuilder();
            changeNotesUpdatedAuthors = new StringBuilder();
            changeNotesRemovedAuthors = new StringBuilder();
            changeNotes = "";

            CSVCombined = new StringBuilder();

            catalogDateTime = DateTime.Now;

            reviewDate = catalogDateTime.Date;

            reviewDateString = Toolkit.DateString(reviewDate);
        }


        // Update or add all found mods
        private static void UpdateAndAddMods()
        {
            // Collect all the required assets from all mods for later logging
            List<ulong> requiredAssetsForLogging = new List<ulong>();

            // Clean up the found information and then add or update the mod in the catalog
            foreach (ulong steamID in CollectedModInfo.Keys)
            {
                // Get the found mod
                Mod collectedMod = CollectedModInfo[steamID];

                // Collect assets from the required mods list
                List<ulong> removalList = new List<ulong>();

                foreach (ulong requiredID in collectedMod.RequiredMods)
                {
                    // Remove the required ID if it's not in the catalog and we didn't find it on the Workshop; ignore builtin required mods
                    if (!CollectedModInfo.ContainsKey(requiredID) && !ActiveCatalog.Instance.ModDictionary.ContainsKey(requiredID) 
                        && (requiredID > ModSettings.highestFakeID))
                    {
                        // We can't remove it here directly, because the RequiredMods list is used in the foreach loop, so we just collect here and remove below
                        removalList.Add(requiredID);

                        // Add the asset to the list for logging, if it's not already there or in the catalog
                        if (!requiredAssetsForLogging.Contains(requiredID) && !ActiveCatalog.Instance.RequiredAssets.Contains(steamID))
                        {
                            requiredAssetsForLogging.Add(requiredID);
                        }
                        
                        // Log if it's an unknown asset
                        if (!ActiveCatalog.Instance.RequiredAssets.Contains(requiredID))
                        {
                            Logger.UpdaterLog($"Required item not found, probably an asset: { Toolkit.GetWorkshopURL(requiredID) } " + 
                                $"(for { collectedMod.ToString(cutOff: false) }).");
                        }
                    }
                }

                // Now remove the found asset IDs
                foreach (ulong requiredID in removalList)
                {
                    collectedMod.RequiredMods.Remove(requiredID);
                }

                // Clean up compatible gameversion
                if (collectedMod.CompatibleGameVersionString == null)
                {
                    collectedMod.Update(compatibleGameVersionString: GameVersion.Formatted(GameVersion.Unknown));
                }

                // Clean up statuses
                if (collectedMod.Statuses.Any())
                {
                    if (collectedMod.Statuses.Contains(Enums.ModStatus.UnlistedInWorkshop) && collectedMod.Statuses.Contains(Enums.ModStatus.RemovedFromWorkshop))
                    {
                        collectedMod.Statuses.Remove(Enums.ModStatus.UnlistedInWorkshop);
                    }
                }

                // Add or update the mod in the catalog
                if (!ActiveCatalog.Instance.ModDictionary.ContainsKey(steamID))
                {
                    // New mod; add to the catalog
                    AddMod(steamID);
                }
                else
                {
                    // Known mod; update all info
                    UpdateMod(steamID);
                }
            }

            // Log the combined list of new required assets found, to ease adding a CSV action
            if (requiredAssetsForLogging.Any())
            {
                StringBuilder requiredAssetsCombined = new StringBuilder();

                foreach(ulong steamID in requiredAssetsForLogging)
                {
                    requiredAssetsCombined.Append(", ");
                    requiredAssetsCombined.Append(steamID.ToString());
                }

                Logger.UpdaterLog("CSV action for adding assets to the catalog (after verification): Add_RequiredAssets" + requiredAssetsCombined.ToString());
            }
        }


        // Update or add all collected groups
        private static void UpdateAndAddGroups()
        {
            foreach (Group collectedGroup in CollectedGroupInfo.Values)
            {
                if (!ActiveCatalog.Instance.GroupDictionary.ContainsKey(collectedGroup.GroupID))
                {
                    // Add new group, which will be automatically replace their group members as required mod.
                    Group newGroup = ActiveCatalog.Instance.AddGroup(collectedGroup.Name, collectedGroup.SteamIDs);

                    changeNotesNewMods.AppendLine($"Group { newGroup.ToString() }: added");
                }
                else
                {
                    // Update existing group
                    Group catalogGroup = ActiveCatalog.Instance.GroupDictionary[collectedGroup.GroupID];

                    // Add new group members. This will also spread existing exclusions to the new group members
                    foreach (ulong newGroupMember in collectedGroup.SteamIDs.Except(catalogGroup.SteamIDs))
                    {
                        ActiveCatalog.Instance.AddGroupMember(catalogGroup.GroupID, newGroupMember);

                        AddUpdatedModChangeNote(ActiveCatalog.Instance.ModDictionary[newGroupMember].SteamID, $": added to group { catalogGroup.ToString() }");

                        // [Todo 0.3] changeNotesUpdatedMods.AppendLine("Mod " + ActiveCatalog.Instance.ModDictionary[newGroupMember].ToString(cutOff: false) + $": added to group { catalogGroup.ToString() }");
                    }

                    // Remove group members. This does not clean up exclusions!
                    foreach (ulong formerGroupMember in catalogGroup.SteamIDs.Except(collectedGroup.SteamIDs))
                    {

                        catalogGroup.SteamIDs.Remove(formerGroupMember);

                        AddUpdatedModChangeNote(ActiveCatalog.Instance.ModDictionary[formerGroupMember].SteamID, $": removed from group { catalogGroup.ToString() }");

                        // [Todo 0.3] changeNotesUpdatedMods.AppendLine("Mod " + ActiveCatalog.Instance.ModDictionary[formerGroupMember].ToString(cutOff: false) + $": removed from group { catalogGroup.ToString() }");
                    }
                }
            }
        }


        // Update or add all found authors
        private static void UpdateAndAddAuthors()
        {
            // Add or update all found authors, by author ID
            foreach (ulong authorID in CollectedAuthorIDs.Keys)
            {
                Author collectedAuthor = CollectedAuthorIDs[authorID];

                if (!ActiveCatalog.Instance.AuthorIDDictionary.ContainsKey(authorID))
                {
                    // New author; add to the catalog
                    AddAuthor(collectedAuthor);
                }
                else
                {
                    // Known author
                    Author catalogAuthor = ActiveCatalog.Instance.AuthorIDDictionary[authorID];

                    // Update all info
                    UpdateAuthor(catalogAuthor, collectedAuthor);
                }
            }

            // Add or update all found authors, by author custom URL
            foreach (string authorURL in CollectedAuthorURLs.Keys)
            {
                Author collectedAuthor = CollectedAuthorURLs[authorURL];

                if (!ActiveCatalog.Instance.AuthorURLDictionary.ContainsKey(authorURL))
                {
                    // New author; add to the catalog
                    AddAuthor(collectedAuthor);
                }
                else
                {
                    // Known author
                    Author catalogAuthor = ActiveCatalog.Instance.AuthorURLDictionary[authorURL];

                    // Update all info
                    UpdateAuthor(catalogAuthor, collectedAuthor);
                }
            }

            // Remove all old author custom URLs that no longer exist
            foreach (string oldURL in authorURLsToRemove)
            {
                ActiveCatalog.Instance.AuthorURLDictionary.Remove(oldURL);
            }
        }


        // Set retired status for authors in the catalog that we didn't find any mods for (anymore) in the Steam Workshop
        private static void RetireFormerAuthors()
        {
            // Find authors by author ID
            foreach (ulong authorID in ActiveCatalog.Instance.AuthorIDDictionary.Keys)
            {
                Author catalogAuthor = ActiveCatalog.Instance.AuthorIDDictionary[authorID];

                // Ignore authors that we found by either ID or URL, and authors that already have the 'retired' status
                if (!CollectedAuthorIDs.ContainsKey(authorID) && !CollectedAuthorURLs.ContainsKey(catalogAuthor.CustomURL) && !catalogAuthor.Retired)
                {
                    catalogAuthor.Update(retired: true, extraChangeNote: $"{ reviewDateString }: updated as retired");

                    changeNotesRemovedAuthors.AppendLine($"Author { ActiveCatalog.Instance.AuthorIDDictionary[authorID].ToString() }: no longer has mods on the workshop");
                }
            }

            // Find authors by custom URL
            foreach (string authorURL in ActiveCatalog.Instance.AuthorURLDictionary.Keys)
            {
                Author catalogAuthor = ActiveCatalog.Instance.AuthorURLDictionary[authorURL];

                // Ignore authors that we found by either ID or URL, and authors that already have the 'retired' status
                if (!CollectedAuthorURLs.ContainsKey(authorURL) && !CollectedAuthorIDs.ContainsKey(catalogAuthor.ProfileID) && !catalogAuthor.Retired)
                {
                    catalogAuthor.Update(retired: true, extraChangeNote: $"{ reviewDateString }: updated as retired");

                    changeNotesRemovedAuthors.AppendLine($"Author { ActiveCatalog.Instance.AuthorURLDictionary[authorURL].ToString() }: no longer has mods on the workshop");
                }
            }
        }


        // Update or add all collected groups
        private static void UpdateAndAddCompatibilities()
        {
            // foreach (Compatibility compatibility in CollectedCompatibilities)
            {
                // [Todo 0.3] Create new compatibility or add to existing one
            }
        }


        // Remove all items from the collected removal list
        private static void Removals()
        {
            foreach (ulong id in CollectedRemovals)
            {
                // Mod
                if (id > ModSettings.highestFakeID)
                {
                    // Get the mod
                    Mod mod = ActiveCatalog.Instance.ModDictionary[id];

                    // This has already been checked for existence in other lists; can remove immediately
                    ActiveCatalog.Instance.ModDictionary.Remove(id);

                    ActiveCatalog.Instance.Mods.Remove(mod);

                    changeNotesRemovedMods.AppendLine($"Mod { mod.ToString(cutOff: false) }: removed");
                }

                // Group
                else if (id >= ModSettings.lowestGroupID && id <= ModSettings.highestGroupID)
                {
                    // Get the group
                    Group group = ActiveCatalog.Instance.GroupDictionary[id];

                    // This has already been checked for existence and has been replaced in all required mods lists; can remove immediately
                    ActiveCatalog.Instance.GroupDictionary.Remove(id);

                    ActiveCatalog.Instance.Groups.Remove(group);

                    // Add to the 'authors' change notes, so it will go to the end of the changes notes
                    changeNotesRemovedMods.AppendLine($"Group { group.Name }: removed");
                }

                // Catalog compatible game version
                else if (id == 1)
                {
                    // Update the change notes; the update itself was already done by the ManualUpdater
                    changeNotesUpdatedAuthors.Insert(0, "Catalog was updated to a new game version: " + ActiveCatalog.Instance.CompatibleGameVersionString);
                }
            }
        }


        // Add a new mod to the active catalog
        private static void AddMod(ulong steamID)
        {
            // Get the collected mod
            Mod collectedMod = CollectedModInfo[steamID];

            // Create a new mod in the catalog
            Mod catalogMod = ActiveCatalog.Instance.AddOrUpdateMod(steamID);

            // If the ReviewUpdated fields were filled, set the date to the local variable reviewUpdateDate, so the datetime will be the same for all mods in this update
            DateTime? modReviewUpdated = null;
            modReviewUpdated = collectedMod.ReviewUpdated == default ? modReviewUpdated: reviewDate;

            DateTime? modAutoReviewUpdated = null;
            modAutoReviewUpdated = collectedMod.AutoReviewUpdated == default ? modAutoReviewUpdated : reviewDate;

            // Update the new catalog mod with all the info, including a review update date 
            catalogMod.Update(collectedMod.Name, collectedMod.Published, collectedMod.Updated, collectedMod.AuthorID, collectedMod.AuthorURL, 
                archiveURL: null, collectedMod.SourceURL, collectedMod.CompatibleGameVersionString, collectedMod.RequiredDLC, collectedMod.RequiredMods, 
                collectedMod.Successors, collectedMod.Alternatives, collectedMod.Recommendations, collectedMod.Statuses, collectedMod.Note, 
                collectedMod.ExclusionForSourceURL, collectedMod.ExclusionForGameVersion, collectedMod.ExclusionForNoDescription, collectedMod.ExclusionForRequiredDLC, 
                collectedMod.ExclusionForRequiredMods, modReviewUpdated, modAutoReviewUpdated, extraChangeNote: $"{ reviewDateString }: added");

            // Change notes
            string modType = catalogMod.Statuses.Contains(Enums.ModStatus.UnlistedInWorkshop) ? " as unlisted in workshop" : 
                (catalogMod.Statuses.Contains(Enums.ModStatus.RemovedFromWorkshop) ? " as removed from workshop" : "");

            changeNotesNewMods.AppendLine($"Mod { catalogMod.ToString(cutOff: false) }: added{ modType }");
        }


        // Update a mod in the catalog with new info
        private static void UpdateMod(ulong steamID)
        {
            // Get a reference to the mod in the catalog and to the mod with the collected info
            Mod catalogMod = ActiveCatalog.Instance.ModDictionary[steamID];

            Mod collectedMod = CollectedModInfo[catalogMod.SteamID];

            // Did we check details for this mod?
            bool detailedUpdate = collectedMod.AutoReviewUpdated != default || collectedMod.ReviewUpdated != default;

            // Keep track of changes
            string changes = "";

            // Name
            if ((catalogMod.Name != collectedMod.Name) && !string.IsNullOrEmpty(collectedMod.Name))
            {
                catalogMod.Update(name: collectedMod.Name);

                changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "name changed";
            }

            // Author ID; only update if it was unknown; author ID can never changed and a mod can't change primary owner, so don't remove if we didn't find it anymore
            if ((catalogMod.AuthorID == 0) && (collectedMod.AuthorID != 0))
            {
                // Add author ID to the mod
                catalogMod.Update(authorID: collectedMod.AuthorID);

                changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "profile ID added";

                // Update the author ID for the author; this ensures that when adding/updating authors later, the author is recognized and not mistakenly seen as new
                if (!string.IsNullOrEmpty(catalogMod.AuthorURL) && ActiveCatalog.Instance.AuthorURLDictionary.ContainsKey(catalogMod.AuthorURL))
                {
                    // Only update the ID if it wasn't updated already from a previous mod
                    if (!ActiveCatalog.Instance.AuthorIDDictionary.ContainsKey(catalogMod.AuthorID))
                    {
                        Author catalogAuthor = ActiveCatalog.Instance.AuthorURLDictionary[catalogMod.AuthorURL];

                        // Add author ID to author
                        catalogAuthor.Update(profileID: catalogMod.AuthorID, extraChangeNote: $"{ reviewDateString }: profile ID added");

                        // Add author to author ID dictionary in the active catalog
                        ActiveCatalog.Instance.AuthorIDDictionary.Add(catalogAuthor.ProfileID, catalogAuthor);

                        // Change notes
                        changeNotesUpdatedAuthors.AppendLine($"Author { catalogAuthor.ToString() }: profile ID added");

                        Logger.UpdaterLog($"Author { catalogAuthor.ToString() }: profile ID { catalogAuthor.ProfileID } linked to custom URL \"{ catalogAuthor.CustomURL }\".");
                    }
                }
                else
                {
                    Logger.UpdaterLog($"Could not add author profile ID { catalogMod.AuthorID } to author with custom URL \"{ catalogMod.AuthorURL }\", " + 
                        "because the URL can't be found anymore.");
                }
            }


            // Author URL. If it was no longer found, it could just be Steam acting up, but safer to remove it anyway before someone else starts using it
            // [Todo 0.3] manual updates to authors might go wrong because of changed or missing url's
            if (catalogMod.AuthorURL != collectedMod.AuthorURL && (!string.IsNullOrEmpty(catalogMod.AuthorURL) || !string.IsNullOrEmpty(collectedMod.AuthorURL)))
            {
                // Added, removed (not used currently) or changed
                string change = string.IsNullOrEmpty(catalogMod.AuthorURL)   ? "added" :
                                string.IsNullOrEmpty(collectedMod.AuthorURL) ? "removed" :
                                "changed";

                // Collect the old URL before we change it
                string oldURL = catalogMod.AuthorURL ?? "";

                // Add author URL to the mod, with null changed into an empty string
                catalogMod.Update(authorURL: collectedMod.AuthorURL ?? "");

                changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "author custom url " + change;

                // Update the author URL for the author; this ensures that when adding/updating authors later, the author is recognized and not mistakenly seen as new
                Author catalogAuthor = null;

                // Get the author by ID
                if (catalogMod.AuthorID != 0 && ActiveCatalog.Instance.AuthorIDDictionary.ContainsKey(catalogMod.AuthorID))
                {
                    catalogAuthor = ActiveCatalog.Instance.AuthorIDDictionary[catalogMod.AuthorID];
                }
                // Or get the author by old URL
                else if (!string.IsNullOrEmpty(oldURL) && ActiveCatalog.Instance.AuthorURLDictionary.ContainsKey(oldURL))
                {
                    catalogAuthor = ActiveCatalog.Instance.AuthorURLDictionary[oldURL];
                }

                // Update the custom URL for the author, but only if it wasn't updated already from a previous mod
                if (catalogAuthor != null && !ActiveCatalog.Instance.AuthorURLDictionary.ContainsKey(catalogMod.AuthorURL))
                {
                    // Add/update URL for author
                    catalogAuthor.Update(customURL: catalogMod.AuthorURL, extraChangeNote: $"{ reviewDateString }: custom URL { change }");

                    // Add author to author URL dictionary in the active catalog
                    ActiveCatalog.Instance.AuthorURLDictionary.Add(catalogAuthor.CustomURL, catalogAuthor);

                    // Mark the old URL for removal
                    if (!string.IsNullOrEmpty(oldURL) && !authorURLsToRemove.Contains(oldURL))
                    {
                        authorURLsToRemove.Add(oldURL);
                    }

                    // Change notes
                    changeNotesUpdatedAuthors.AppendLine($"Author { catalogAuthor.ToString() }: custom URL { change }");

                    Logger.UpdaterLog($"Author { catalogAuthor.ToString() }: new custom URL \"{ catalogAuthor.CustomURL }\"" +
                        (catalogAuthor.ProfileID == 0 ? "." : $" linked to profile ID { catalogAuthor.ProfileID }.") +
                        (string.IsNullOrEmpty(oldURL) ? "" : $" Old URL: { oldURL }."));
                }
                // If the catalog contains the new URL, then the author was already updated from an earlier mod; otherwise log an error
                else if (!ActiveCatalog.Instance.AuthorURLDictionary.ContainsKey(catalogMod.AuthorURL))
                {
                    Logger.UpdaterLog($"Could not update author custom URL \"{ catalogMod.AuthorURL }\" to author with profile ID { catalogMod.AuthorID }, " +
                        "because the ID or the old URL can't be found.", Logger.error);
                }
            }

            // Published (only if details for this mod were checked)
            if (catalogMod.Published < collectedMod.Published && detailedUpdate)
            {
                // No mention in the change notes, but log if the publish date was already a valid date
                if (catalogMod.Published != DateTime.MinValue)
                {
                    Logger.UpdaterLog($"Published date changed from { Toolkit.DateString(catalogMod.Published) } to { Toolkit.DateString(collectedMod.Published) }. " +
                        $"This should not happen. Mod { catalogMod.ToString(cutOff: false) }", Logger.warning);
                }

                catalogMod.Update(published: collectedMod.Published);
            }

            // Updated (only if details for this mod were checked)
            if (catalogMod.Updated < collectedMod.Updated && detailedUpdate)
            {
                catalogMod.Update(updated: collectedMod.Updated);

                // Only mention in the change notes if it was really an update (and not a copy of the published date)
                if (catalogMod.Updated != catalogMod.Published)
                {
                    changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "update found";
                }
            }

            // Source URL (only if details for this mod were checked or ManualUpdater is running)
            if (catalogMod.SourceURL != collectedMod.SourceURL && detailedUpdate)
            {
                // Added, removed or changed
                string change = string.IsNullOrEmpty(catalogMod.SourceURL) && !string.IsNullOrEmpty(collectedMod.SourceURL) ? "added" :
                                !string.IsNullOrEmpty(catalogMod.SourceURL) && string.IsNullOrEmpty(collectedMod.SourceURL) ? "removed" :
                                "changed";

                changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "source url " + change;

                // Remove 'source unavailable' status if a source url was added
                if (change == "added" && catalogMod.Statuses.Contains(Enums.ModStatus.SourceUnavailable))
                {
                    catalogMod.Statuses.Remove(Enums.ModStatus.SourceUnavailable);

                    changes += " ('source unavailable' status removed)";
                }

                catalogMod.Update(sourceURL: collectedMod.SourceURL);
            }

            // Compatible game version (only if details for this mod were checked)
            if (catalogMod.CompatibleGameVersionString != collectedMod.CompatibleGameVersionString && detailedUpdate)
            {
                string unknown = GameVersion.Unknown.ToString();

                // Added, removed or changed
                string change = catalogMod.CompatibleGameVersionString == unknown && collectedMod.CompatibleGameVersionString != unknown ? "added" :
                                catalogMod.CompatibleGameVersionString != unknown && collectedMod.CompatibleGameVersionString == unknown ? "removed" :
                                "changed";

                changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "compatible game version tag " + change;

                catalogMod.Update(compatibleGameVersionString: collectedMod.CompatibleGameVersionString);
            }

            // Required DLC (only if details for this mod were checked)
            if (catalogMod.RequiredDLC.Count + collectedMod.RequiredDLC.Count != 0 && detailedUpdate)
            {
                // Add new required dlc
                foreach (Enums.DLC dlc in collectedMod.RequiredDLC)
                {
                    if (!catalogMod.RequiredDLC.Contains(dlc))
                    {
                        catalogMod.RequiredDLC.Add(dlc);

                        changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + $"required dlc { dlc } added";
                    }
                }

                // We need to collect the required dlcs because we can't remove them inside the foreach loop
                List<Enums.DLC> removals = new List<Enums.DLC>();

                // Find no longer required dlc
                foreach (Enums.DLC dlc in catalogMod.RequiredDLC)
                {
                    if (!collectedMod.RequiredDLC.Contains(dlc))
                    {
                        // Add the dlc to the removals list
                        removals.Add(dlc);

                        changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + $"required dlc { dlc } removed";
                    }
                }

                // Remove the no longer required dlc
                foreach (Enums.DLC dlc in removals)
                {
                    catalogMod.RequiredDLC.Remove(dlc);
                }
            }

            // Required mods (only if details for this mod were checked)  [Todo 0.5] simplify (or split) this
            if (catalogMod.RequiredMods.Count + collectedMod.RequiredMods.Count != 0 && detailedUpdate)
            {
                // We need to collect the required mods and groups because we can't remove them inside the foreach loop
                List<ulong> removals = new List<ulong>();

                // Find no longer needed mods and groups from the required list
                foreach (ulong requiredID in catalogMod.RequiredMods)
                {
                    // Check if it's a mod or a group
                    if (requiredID >= ModSettings.lowestGroupID && requiredID <= ModSettings.highestGroupID)
                    {
                        // ID is a group; check if this is still required
                        bool stillRequired = false;

                        foreach (ulong modID in ActiveCatalog.Instance.GroupDictionary[requiredID].SteamIDs)
                        {
                            if (collectedMod.RequiredMods.Contains(modID))
                            {
                                // A group member is still required, so the group is still required
                                stillRequired = true;

                                break;
                            }
                        }

                        if (!stillRequired)
                        {
                            // No longer required; remove the group
                            removals.Add(requiredID);

                            changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + $"required group { requiredID } removed";
                        }
                    }
                    else if (ActiveCatalog.Instance.Groups.Find(x => x.SteamIDs.Contains(requiredID)) != null)
                    {
                        // ID is a mod that is a group member, so remove it; the group will be added below if still needed
                        removals.Add(requiredID);

                        changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + $"required mod { requiredID } removed";
                    }
                    else if (!collectedMod.RequiredMods.Contains(requiredID))
                    {
                        // ID is a mod that is not in any group, and it's not required anymore, so remove it
                        removals.Add(requiredID);

                        changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + $"required mod { requiredID } removed";
                    }
                }

                // Remove the no longer required mods and groups
                foreach (ulong requiredID in removals)
                {
                    catalogMod.RequiredMods.Remove(requiredID);
                }

                // Add new required mods
                foreach (ulong requiredModID in collectedMod.RequiredMods)
                {
                    // Add the required mod to the catalog mod's required list, if it isn't there already
                    if (!catalogMod.RequiredMods.Contains(requiredModID))
                    {
                        catalogMod.RequiredMods.Add(requiredModID);

                        // Replace the required mod by its group, if it is a group member
                        if (ActiveCatalog.Instance.IsGroupMember(requiredModID))
                        {
                            ActiveCatalog.Instance.ReplaceRequiredModByGroup(requiredModID);

                            changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + $"required group { ActiveCatalog.Instance.GetGroup(requiredModID).GroupID } added";
                        }
                        else
                        {
                            changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + $"required mod { requiredModID } added";
                        }
                    }
                }
            }

            // Add new Statuses: incompatible, no description*, unlisted in workshop, removed from workshop (* = only if details for this mod were checked)
            if (collectedMod.Statuses.Count > 0)
            {
                if (collectedMod.Statuses.Contains(Enums.ModStatus.IncompatibleAccordingToWorkshop) &&
                    !catalogMod.Statuses.Contains(Enums.ModStatus.IncompatibleAccordingToWorkshop))
                {
                    catalogMod.Statuses.Add(Enums.ModStatus.IncompatibleAccordingToWorkshop);

                    changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "IncompatibleAccordingToWorkshop status added";
                }

                if (collectedMod.Statuses.Contains(Enums.ModStatus.NoDescription) && 
                    !catalogMod.Statuses.Contains(Enums.ModStatus.NoDescription) && detailedUpdate)
                {
                    catalogMod.Statuses.Add(Enums.ModStatus.NoDescription);

                    changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "NoDescription status added";
                }

                if (collectedMod.Statuses.Contains(Enums.ModStatus.UnlistedInWorkshop) && 
                    !catalogMod.Statuses.Contains(Enums.ModStatus.UnlistedInWorkshop))
                {
                    catalogMod.Statuses.Add(Enums.ModStatus.UnlistedInWorkshop);

                    // Remove removed status, if needed
                    catalogMod.Statuses.Remove(Enums.ModStatus.RemovedFromWorkshop);

                    changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "UnlistedInWorkshop status added";
                }

                if (collectedMod.Statuses.Contains(Enums.ModStatus.RemovedFromWorkshop) &&
                    !catalogMod.Statuses.Contains(Enums.ModStatus.RemovedFromWorkshop))
                {
                    catalogMod.Statuses.Add(Enums.ModStatus.RemovedFromWorkshop);

                    // Remove unlisted status, if needed
                    catalogMod.Statuses.Remove(Enums.ModStatus.UnlistedInWorkshop);

                    // Gives this its own line in the change notes
                    changeNotesRemovedMods.AppendLine($"Mod { catalogMod.ToString(cutOff: false) }: removed from the workshop");
                }
            }

            // Remove Statuses: incompatible, no description*, unlisted in workshop, removed from workshop (* = only if details for this mod were checked)
            if (catalogMod.Statuses.Count > 0)
            {
                if (catalogMod.Statuses.Contains(Enums.ModStatus.IncompatibleAccordingToWorkshop) &&
                    !collectedMod.Statuses.Contains(Enums.ModStatus.IncompatibleAccordingToWorkshop))
                {
                    catalogMod.Statuses.Remove(Enums.ModStatus.IncompatibleAccordingToWorkshop);

                    changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "IncompatibleAccordingToWorkshop status removed";
                }

                if (catalogMod.Statuses.Contains(Enums.ModStatus.NoDescription) && 
                    !collectedMod.Statuses.Contains(Enums.ModStatus.NoDescription) && detailedUpdate)
                {
                    catalogMod.Statuses.Remove(Enums.ModStatus.NoDescription);

                    changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "NoDescription status removed";
                }

                if (catalogMod.Statuses.Contains(Enums.ModStatus.UnlistedInWorkshop) &&
                    !collectedMod.Statuses.Contains(Enums.ModStatus.UnlistedInWorkshop))
                {
                    catalogMod.Statuses.Remove(Enums.ModStatus.UnlistedInWorkshop);

                    changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "UnlistedInWorkshop status removed";
                }

                if (catalogMod.Statuses.Contains(Enums.ModStatus.RemovedFromWorkshop) &&
                    !collectedMod.Statuses.Contains(Enums.ModStatus.RemovedFromWorkshop))
                {
                    catalogMod.Statuses.Remove(Enums.ModStatus.RemovedFromWorkshop);

                    // Gives this its own line in the change notes
                    changeNotesNewMods.AppendLine($"Mod { catalogMod.ToString(cutOff: false) }: reappeared on the workshop after being removed previously");
                }
            }

            // Review update dates and change notes
            if (!string.IsNullOrEmpty(changes))
            {
                // If the ReviewUpdated fields were filled, set the date to the local variable reviewUpdateDate, so the datetime will be the same for all mods in this update
                DateTime? modReviewUpdated = null;
                modReviewUpdated = collectedMod.ReviewUpdated == default ? modReviewUpdated : reviewDate;

                DateTime? modAutoReviewUpdated = null;
                modAutoReviewUpdated = collectedMod.AutoReviewUpdated == default ? modAutoReviewUpdated : reviewDate;

                catalogMod.Update(reviewUpdated: modReviewUpdated, autoReviewUpdated: modAutoReviewUpdated, extraChangeNote: $"{ reviewDateString }: { changes }");

                AddUpdatedModChangeNote(catalogMod.SteamID, changes);

                // [Todo 0.3] changeNotesUpdatedMods.AppendLine($"Mod { catalogMod.ToString(cutOff: false) }: { changes }");
            }
        }


        // Add a new author to the catalog
        private static void AddAuthor(Author collectedAuthor)
        {
            ActiveCatalog.Instance.AddAuthor(collectedAuthor.ProfileID, collectedAuthor.CustomURL, collectedAuthor.Name, collectedAuthor.LastSeen, 
                retired: collectedAuthor.LastSeen.AddYears(1) < DateTime.Today, changeNoteString: $"{ reviewDateString }: added");

            // Change notes
            changeNotesNewAuthors.AppendLine($"New author { collectedAuthor.ToString() }");
        }


        // Update changed info for an author (profile ID and custom URL changes are updated together with mod updates)
        // [Todo 0.3] Update retired for all authors based on last seen date
        private static void UpdateAuthor(Author catalogAuthor, Author collectedAuthor)
        {
            // Keep track of changes
            string changes = "";

            // Name
            if ((catalogAuthor.Name != collectedAuthor.Name) && !string.IsNullOrEmpty(collectedAuthor.Name))
            {
                catalogAuthor.Update(name: collectedAuthor.Name);

                changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "name changed";
            }

            // Last seen. Only updated if last seen is newer, or if last seen was manually updated
            if (catalogAuthor.LastSeen < collectedAuthor.LastSeen || (catalogAuthor.LastSeen != collectedAuthor.LastSeen && collectedAuthor.ManuallyUpdated))
            {
                catalogAuthor.Update(lastSeen: collectedAuthor.LastSeen);

                changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "last seen date updated";
            }

            // Update the collected author's retired status if no exclusion exists or if the last seen date was over a year ago
            if (!collectedAuthor.ExclusionForRetired || collectedAuthor.LastSeen.AddYears(1) < DateTime.Today)
            {
                collectedAuthor.Update(retired: collectedAuthor.LastSeen.AddYears(1) < DateTime.Today);
            }

            // Retired
            if (!catalogAuthor.Retired && collectedAuthor.Retired)
            {
                catalogAuthor.Update(retired: true, exclusionForRetired: collectedAuthor.ExclusionForRetired);

                changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "set as retired";
            }
            // No longer retired, only on manual updates and when no exclusion existed
            else if (catalogAuthor.Retired && !catalogAuthor.ExclusionForRetired && !collectedAuthor.Retired && collectedAuthor.ManuallyUpdated)
            {
                catalogAuthor.Update(retired: false);

                changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "no longer retired";
            }

            // Change notes
            if (!string.IsNullOrEmpty(changes))
            {
                catalogAuthor.Update(extraChangeNote: $"{ reviewDateString }: { changes }");

                changeNotesUpdatedAuthors.AppendLine($"Author { catalogAuthor.ToString() }: " + changes);
            }
        }


        // Set a new note for the catalog
        internal static void SetNote(string newCatalogNote) => catalogNote = newCatalogNote ?? "";


        // Set an update date
        internal static bool SetReviewDate(string dateString)
        {
            DateTime convertedDate = Toolkit.Date(dateString);
            
            if (convertedDate == default)
            {
                return false;
            }

            reviewDate = convertedDate;

            reviewDateString = Toolkit.DateString(reviewDate);

            return true;
        }










        // Add or get a mod. When adding, an unlisted, removed or incompatible status can be supplied
        internal static Mod AddOrGetMod(ulong steamID,
                                        Enums.ModStatus workshopStatus = Enums.ModStatus.Unknown)
        {
            Mod catalogMod;

            // Get the mod from the catalog, or add a new one
            if (ActiveCatalog.Instance.ModDictionary.ContainsKey(steamID))
            {
                // Get the catalog mod
                catalogMod = ActiveCatalog.Instance.ModDictionary[steamID];
            }
            else
            {
                // Add a new mod
                catalogMod = ActiveCatalog.Instance.AddOrUpdateMod(steamID);

                // Add unlisted or removed status if needed, ignore all other statuses
                if (workshopStatus == Enums.ModStatus.UnlistedInWorkshop || workshopStatus == Enums.ModStatus.RemovedFromWorkshop ||
                    workshopStatus == Enums.ModStatus.IncompatibleAccordingToWorkshop)
                {
                    catalogMod.Statuses.Add(workshopStatus);
                }

                // Set change notes directly
                string modType = workshopStatus == Enums.ModStatus.UnlistedInWorkshop ? "(unlisted) " :
                    (workshopStatus == Enums.ModStatus.RemovedFromWorkshop ? "(removed) " : "");

                catalogMod.Update(extraChangeNote: $"{ reviewDateString }: added");

                changeNotesNewMods.AppendLine($"New mod { modType }{ catalogMod.ToString(cutOff: false) }");
            }

            return catalogMod;
        }


        // Update a mod with newly found information
        internal static void UpdateMod(Mod catalogMod,
                                           string name = null,
                                           DateTime? published = null,
                                           DateTime? updated = null,
                                           ulong authorID = 0,
                                           string authorURL = null,
                                           string archiveURL = null,
                                           string sourceURL = null,
                                           string compatibleGameVersionString = null,
                                           List<Enums.DLC> requiredDLC = null,
                                           List<ulong> requiredMods = null,
                                           List<ulong> successors = null,
                                           List<ulong> alternatives = null,
                                           List<ulong> recommendations = null,
                                           List<Enums.ModStatus> statuses = null,
                                           string note = null,
                                           bool ManualUpdate = true)
        {
            // Set the change note for all changed values  [Todo 0.3] Needs more logic for authorID/authorURL, all lists, ... (combine with ManualUpdater)
            string addedChangeNote =
                (name == null || name == catalogMod.Name ? "" : ", mod name") +
                (updated == null || updated == catalogMod.Updated ? "" : ", update") +
                (authorID == 0 || authorID == catalogMod.AuthorID ? "" : ", author ID") +
                (authorURL == null || authorURL == catalogMod.AuthorURL ? "" : ", author URL") +
                (archiveURL == null || archiveURL == catalogMod.ArchiveURL ? "" : ", archive URL") +
                (sourceURL == null || sourceURL == catalogMod.SourceURL ? "" : ", source URL") +
                (compatibleGameVersionString == null || compatibleGameVersionString == catalogMod.CompatibleGameVersionString ? "" : ", compatible game version") +
                (requiredDLC == null || requiredDLC == catalogMod.RequiredDLC ? "" : ", required DLC") +
                (requiredMods == null || requiredMods == catalogMod.RequiredMods ? "" : ", required mod") +
                (successors == null || successors == catalogMod.Successors ? "" : ", successor mod") +
                (alternatives == null || alternatives == catalogMod.Alternatives ? "" : ", alternative mod") +
                (recommendations == null || recommendations == catalogMod.Recommendations ? "" : ", recommended mod") +
                (statuses == null || statuses == catalogMod.Statuses ? "" : ", status") +
                (note == null || note == catalogMod.Note ? "" : ", mod note");

            AddUpdatedModChangeNote(catalogMod.SteamID, addedChangeNote);

            // Set the update date
            DateTime? modReviewUpdated = null;
            modReviewUpdated = ManualUpdate == true ? reviewDate : modReviewUpdated;

            DateTime? modAutoReviewUpdated = null;
            modAutoReviewUpdated = ManualUpdate == false ? reviewDate : modAutoReviewUpdated;

            // Update the mod  [Todo 0.3] Needs more logic for authorID/authorURL, all lists, ...
            catalogMod.Update(name, published, updated, authorID, authorURL, archiveURL, sourceURL, compatibleGameVersionString, requiredDLC, requiredMods,
                successors, alternatives, recommendations, statuses, note, reviewUpdated: modReviewUpdated, autoReviewUpdated: modAutoReviewUpdated);
        }


        // Add a change note for an updated mod. A date will be added later.
        internal static void AddUpdatedModChangeNote(ulong steamID, string extraChangeNote)
        {
            // Add a separator if needed. The separator at the start of the final change note will be stripped before it is written.
            if (extraChangeNote[0] != ',')
            {
                extraChangeNote = ", " + extraChangeNote;
            }

            // Add the new change note to the dictionary
            if (changeNotesUpdatedMods.ContainsKey(steamID))
            {
                changeNotesUpdatedMods[steamID] += extraChangeNote;
            }
            else
            {
                changeNotesUpdatedMods.Add(steamID, extraChangeNote);
            }
        }
    }
}
